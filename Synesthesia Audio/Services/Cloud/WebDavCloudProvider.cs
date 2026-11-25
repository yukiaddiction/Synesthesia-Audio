using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Cloud;

public class WebDavCloudProvider : ICloudProvider
{
    private readonly HttpClient _httpClient;
    private readonly SettingsService _settingsService;
    private readonly ILogger<WebDavCloudProvider> _logger;

    public WebDavCloudProvider(SettingsService settingsService, ILogger<WebDavCloudProvider> logger)
    {
        _settingsService = settingsService;
        _logger = logger;
        
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(DLsiteService.DefaultUserAgent);
    }

    public bool IsConfigured
    {
        get
        {
            var settings = _settingsService.GetSettingsSnapshot();
            if (settings.StorageType != "WebDAV" ||
                string.IsNullOrWhiteSpace(settings.CloudServerUrl) ||
                string.IsNullOrWhiteSpace(settings.CloudUsername))
            {
                return false;
            }

            WarnIfInsecure(settings.CloudServerUrl);

            return true;
        }
    }

    private void WarnIfInsecure(string serverUrl)
    {
        if (!serverUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("WebDAV cloud server URL is not using HTTPS — credentials will be sent in plain text");
        }
    }

    /// <summary>
    /// The connection an operation should use: the caller's explicit one when supplied (form values the
    /// user has not saved yet), otherwise the saved settings if WebDAV is the active storage.
    /// </summary>
    private WebDavConnection? ResolveConnection(WebDavConnection? explicitConnection)
    {
        if (explicitConnection != null)
        {
            return explicitConnection.IsComplete ? explicitConnection : null;
        }

        if (!IsConfigured) return null;

        var settings = _settingsService.GetSettingsSnapshot();
        return WebDavConnection.FromSettings(settings);
    }

    private async Task<AuthenticationHeaderValue> BuildAuthHeaderAsync(WebDavConnection? connection)
    {
        var resolved = connection;
        if (resolved == null)
        {
            await _settingsService.EnsurePasswordLoadedAsync().ConfigureAwait(false);
            resolved = WebDavConnection.FromSettings(_settingsService.GetSettingsSnapshot());
        }

        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{resolved.Username}:{resolved.Password}"));
        return new AuthenticationHeaderValue("Basic", authString);
    }

    /// <summary>
    /// Converts a library-relative path into the path portion of a URL. The rule itself lives in
    /// <see cref="RemotePath"/> so it can be unit-tested; this only assembles the URL and re-checks that
    /// the resolved address really is still under the configured folder.
    /// </summary>
    private static string BuildRelativeUrlPath(string? path) => RemotePath.NormalizeRelative(path);

    private static string BuildFileUrl(string baseUrl, string path)
    {
        var normalizedBase = baseUrl.TrimEnd('/') + "/";
        var relative = BuildRelativeUrlPath(path);
        var url = normalizedBase + Uri.EscapeDataString(relative).Replace("%2F", "/");

        // Belt and braces: after resolution the URL must still live under the configured folder.
        if (Uri.TryCreate(url, UriKind.Absolute, out var resolved) &&
            Uri.TryCreate(normalizedBase, UriKind.Absolute, out var baseUri))
        {
            if (!resolved.AbsolutePath.StartsWith(baseUri.AbsolutePath.TrimEnd('/') + "/", StringComparison.Ordinal))
                throw new ArgumentException($"WebDAV path escapes the configured base folder: {path}", nameof(path));
        }

        return url;
    }

    public async Task<(bool Success, string ErrorMessage)> TestConnectionAsync(WebDavConnection? connection = null)
    {
        var conn = ResolveConnection(connection);
        if (conn == null) return (false, "Storage credentials not fully configured in settings.");
        try
        {
            WarnIfInsecure(conn.ServerUrl);

            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), conn.NormalizedServerUrl)
            {
                Content = new StringContent(GetPropFindRequestBody(), Encoding.UTF8, "text/xml")
            };
            request.Headers.Authorization = await BuildAuthHeaderAsync(conn);
            request.Headers.Add("Depth", "0");
            
            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return (true, string.Empty);
            }

            string details = "";
            try
            {
                details = await response.Content.ReadAsStringAsync();
                if (details.Length > 200) details = details.Substring(0, 200);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TestConnectionAsync read error");
            }

            return (false, $"HTTP Error {(int)response.StatusCode} ({response.ReasonPhrase}). Response snippet: {details}");
        }
        catch (Exception ex)
        {
            return (false, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    public async Task<List<CloudFileInfo>> ListFilesAsync(string path, WebDavConnection? connection = null)
    {
        var fileList = new List<CloudFileInfo>();
        var conn = ResolveConnection(connection);
        if (conn == null) return fileList;

        try
        {
            var baseUrl = conn.NormalizedServerUrl;
            
            string targetUrl = baseUrl;
            if (!string.IsNullOrEmpty(path))
            {
                // Same guard as file access: a listing must not be able to walk above the base folder.
                var relative = BuildRelativeUrlPath(path);
                if (relative.Length > 0)
                {
                    targetUrl = baseUrl + Uri.EscapeDataString(relative).Replace("%2F", "/");
                }
            }
            
            if (!targetUrl.EndsWith("/")) targetUrl += "/";
            
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), targetUrl)
            {
                Content = new StringContent(GetPropFindRequestBody(), Encoding.UTF8, "text/xml")
            };
            request.Headers.Authorization = await BuildAuthHeaderAsync(conn);
            request.Headers.Add("Depth", "1");
            
            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            var statusCode = (int)response.StatusCode;

            if (CloudStatusClassifier.IsAccessDenied(statusCode))
            {
                // Refusal is not emptiness: returning an empty list here made a wrong password look like an
                // empty library, and the scan then cached that (PAT-05).
                var message = CloudStatusClassifier.Describe(statusCode, response.ReasonPhrase);
                _logger.LogWarning("WebDAV listing refused: {Message}", message);
                throw new CloudAccessDeniedException(message);
            }

            if (!response.IsSuccessStatusCode)
            {
                var message = CloudStatusClassifier.Describe(statusCode, response.ReasonPhrase);
                _logger.LogWarning("WebDAV listing failed: {Message}", message);
                throw new CloudRequestFailedException(message);
            }

            var xmlContent = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(xmlContent);
            
            var responses = doc.Descendants().Where(e => e.Name.LocalName == "response");
            bool isFirst = true;

            foreach (var resp in responses)
            {
                var hrefNode = resp.Elements().FirstOrDefault(e => e.Name.LocalName == "href");
                if (hrefNode == null) continue;

                var href = Uri.UnescapeDataString(hrefNode.Value);
                
                var propstatNode = resp.Elements().FirstOrDefault(e => e.Name.LocalName == "propstat");
                var propNode = propstatNode?.Elements().FirstOrDefault(e => e.Name.LocalName == "prop");
                if (propNode == null) continue;

                var displayNameNode = propNode.Elements().FirstOrDefault(e => e.Name.LocalName == "displayname");
                var resourceTypeNode = propNode.Elements().FirstOrDefault(e => e.Name.LocalName == "resourcetype");
                var contentLengthNode = propNode.Elements().FirstOrDefault(e => e.Name.LocalName == "getcontentlength");

                string displayName = displayNameNode?.Value ?? Path.GetFileName(href.TrimEnd('/'));
                bool isDirectory = resourceTypeNode?.Elements().Any(e => e.Name.LocalName == "collection") ?? false;
                long size = 0;
                if (contentLengthNode != null && long.TryParse(contentLengthNode.Value, out long parsedSize))
                {
                    size = parsedSize;
                }

                var baseUri = new Uri(baseUrl);
                var itemUri = new Uri(baseUri, href);
                var relativePath = baseUri.MakeRelativeUri(itemUri).ToString();
                relativePath = Uri.UnescapeDataString(relativePath).TrimEnd('/');

                if (isFirst)
                {
                    isFirst = false;
                    if (string.Equals(relativePath, path?.Replace('\\', '/').TrimEnd('/'), StringComparison.OrdinalIgnoreCase) || 
                        string.IsNullOrEmpty(relativePath))
                    {
                        continue;
                    }
                }

                fileList.Add(new CloudFileInfo
                {
                    Path = relativePath.Replace('\\', '/'),
                    Name = displayName,
                    IsDirectory = isDirectory,
                    Size = size
                });
            }
        }
        catch (CloudAccessDeniedException) { throw; }
        catch (CloudRequestFailedException) { throw; }
        catch (Exception ex)
        {
            // A parse or transport failure must also reach the caller, otherwise it looks like an empty folder.
            _logger.LogWarning(ex, "ListFilesAsync failed for path: {Path}", path);
            throw new CloudRequestFailedException($"The folder could not be read: {ex.Message}", ex);
        }

        return fileList;
    }

    public async Task<Stream> GetFileStreamAsync(string path, WebDavConnection? connection = null)
    {
        var conn = ResolveConnection(connection);
        if (conn == null) throw new InvalidOperationException("WebDAV is not configured.");

        var targetUrl = BuildFileUrl(conn.NormalizedServerUrl, path);

        using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Authorization = await BuildAuthHeaderAsync(conn);

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        try
        {
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync();
            return new ResponseWrappingStream(stream, response);
        }
        catch
        {
            // The caller receives an exception, so nothing downstream will dispose this response — it used
            // to be leaked along with its connection whenever the status was not a success (BUG-m-23).
            response.Dispose();
            throw;
        }
    }

    private sealed class ResponseWrappingStream : Stream
    {
        private readonly Stream _inner;
        private readonly IDisposable _owner;
        private bool _disposed;

        public ResponseWrappingStream(Stream inner, IDisposable owner)
        {
            _inner = inner;
            _owner = owner;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _inner.Dispose();
                    _owner.Dispose();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }

    public async Task<HttpResponseMessage> GetFileResponseAsync(string path, string? rangeHeader, WebDavConnection? connection = null)
    {
        var conn = ResolveConnection(connection);
        if (conn == null) throw new InvalidOperationException("WebDAV is not configured.");

        var targetUrl = BuildFileUrl(conn.NormalizedServerUrl, path);

        var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Authorization = await BuildAuthHeaderAsync(conn);
        if (!string.IsNullOrEmpty(rangeHeader))
        {
            request.Headers.Add("Range", rangeHeader);
        }

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    public async Task<bool> FileExistsAsync(string path, WebDavConnection? connection = null)
    {
        var conn = ResolveConnection(connection);
        if (conn == null) return false;

        try
        {
            var targetUrl = BuildFileUrl(conn.NormalizedServerUrl, path);

            var request = new HttpRequestMessage(HttpMethod.Head, targetUrl);
            request.Headers.Authorization = await BuildAuthHeaderAsync(conn);
            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FileExistsAsync failed for path: {Path}", path);
            return false;
        }
    }

    private string GetPropFindRequestBody()
    {
        return "<?xml version=\"1.0\" encoding=\"utf-8\" ?><propfind xmlns=\"DAV:\"><prop><displayname/><resourcetype/><getcontentlength/></prop></propfind>";
    }
}
