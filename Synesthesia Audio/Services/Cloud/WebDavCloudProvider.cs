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

            if (!settings.CloudServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("WebDAV cloud server URL is not using HTTPS — credentials will be sent in plain text");
            }

            return true;
        }
    }

    private async Task<AuthenticationHeaderValue> BuildAuthHeaderAsync()
    {
        await _settingsService.EnsurePasswordLoadedAsync().ConfigureAwait(false);
        var settings = _settingsService.GetSettingsSnapshot();
        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.CloudUsername}:{settings.CloudPassword}"));
        return new AuthenticationHeaderValue("Basic", authString);
    }

    private static string BuildFileUrl(string baseUrl, string path)
    {
        var normalizedBase = baseUrl.TrimEnd('/');
        if (!normalizedBase.EndsWith("/")) normalizedBase += "/";
        var cleanPath = path.Replace('\\', '/').TrimStart('/');
        return normalizedBase + Uri.EscapeDataString(cleanPath).Replace("%2F", "/");
    }

    public async Task<(bool Success, string ErrorMessage)> TestConnectionAsync()
    {
        if (!IsConfigured) return (false, "Storage credentials not fully configured in settings.");
        try
        {
            var settings = _settingsService.CurrentSettings;
            var url = settings.CloudServerUrl.TrimEnd('/');
            if (!url.EndsWith("/")) url += "/";
            
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), url)
            {
                Content = new StringContent(GetPropFindRequestBody(), Encoding.UTF8, "text/xml")
            };
            request.Headers.Authorization = await BuildAuthHeaderAsync();
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

    public async Task<List<CloudFileInfo>> ListFilesAsync(string path)
    {
        var fileList = new List<CloudFileInfo>();
        if (!IsConfigured) return fileList;

        try
        {
            var settings = _settingsService.CurrentSettings;
            
            var baseUrl = settings.CloudServerUrl.TrimEnd('/');
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            
            string targetUrl = baseUrl;
            if (!string.IsNullOrEmpty(path))
            {
                targetUrl = baseUrl + Uri.EscapeDataString(path.Replace('\\', '/')).Replace("%2F", "/");
            }
            
            if (!targetUrl.EndsWith("/")) targetUrl += "/";
            
            var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), targetUrl)
            {
                Content = new StringContent(GetPropFindRequestBody(), Encoding.UTF8, "text/xml")
            };
            request.Headers.Authorization = await BuildAuthHeaderAsync();
            request.Headers.Add("Depth", "1");
            
            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return fileList;

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

                var baseUri = new Uri(baseUrl + "/");
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
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ListFilesAsync failed for path: {Path}", path);
        }

        return fileList;
    }

    public async Task<Stream> GetFileStreamAsync(string path)
    {
        var settings = _settingsService.GetSettingsSnapshot();
        var targetUrl = BuildFileUrl(settings.CloudServerUrl, path);

        var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Authorization = await BuildAuthHeaderAsync();

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync();
        return new ResponseWrappingStream(stream, response);
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

    public async Task<HttpResponseMessage> GetFileResponseAsync(string path, string? rangeHeader)
    {
        var settings = _settingsService.CurrentSettings;
        var targetUrl = BuildFileUrl(settings.CloudServerUrl, path);

        var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Authorization = await BuildAuthHeaderAsync();
        if (!string.IsNullOrEmpty(rangeHeader))
        {
            request.Headers.Add("Range", rangeHeader);
        }

        return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        if (!IsConfigured) return false;
        try
        {
            var settings = _settingsService.CurrentSettings;
            var targetUrl = BuildFileUrl(settings.CloudServerUrl, path);

            var request = new HttpRequestMessage(HttpMethod.Head, targetUrl);
            request.Headers.Authorization = await BuildAuthHeaderAsync();
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
