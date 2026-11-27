using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Cloud;

/// <summary>
/// Loopback HTTP bridge that streams WebDAV files into the media element. Any local process can reach a loopback
/// port, so it only starts when a WebDAV track needs it, demands a per-run secret token, and serves files by
/// opaque id so a request can never name an arbitrary path on the account.
/// </summary>
public class LocalHttpProxy : IDisposable
{
    private volatile TcpListener? _listener;
    private readonly ICloudProvider _cloudProvider;
    private readonly ILogger<LocalHttpProxy> _logger;
    private readonly object _startLock = new();
    private volatile int _port;
    private volatile bool _isRunning;
    private int _requestCount;

    // Without it another local process could borrow the user's WebDAV credentials.
    private readonly ProxyAccessToken _token = ProxyAccessToken.Create();

    // Callers only ever hold the id we issued, never the remote path.
    private readonly ConcurrentDictionary<string, string> _pathById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _idByPath = new(StringComparer.Ordinal);

    public LocalHttpProxy(ICloudProvider cloudProvider, ILogger<LocalHttpProxy> logger)
    {
        _cloudProvider = cloudProvider;
        _logger = logger;
    }

    public bool IsRunning => _isRunning;
    public int Port => _port;

    private void AddLog(string msg) => _logger.LogDebug("[LocalHttpProxy] {Message}", msg);

    public void Start()
    {
        lock (_startLock)
        {
            if (_isRunning && _listener != null) return;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _isRunning = true;
                AddLog($"Proxy STARTED on http://127.0.0.1:{_port}/");
                Task.Run(ListenLoopAsync).FireAndForget(ex => AddLog($"Listen loop failed: {ex.GetType().Name}: {ex.Message}"));
            }
            catch (Exception ex)
            {
                _isRunning = false;
                _listener = null;
                AddLog($"FAILED to start: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    public bool EnsureRunning()
    {
        if (_isRunning && _listener != null) return true;

        lock (_startLock)
        {
            if (_isRunning && _listener != null) return true;
            AddLog("EnsureRunning: proxy not running, attempting restart...");
            Stop();
            Start();
            return _isRunning;
        }
    }

    public string CreateStreamUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A remote path is required.", nameof(path));

        if (!EnsureRunning())
            throw new InvalidOperationException("The cloud streaming proxy could not be started.");

        var id = _idByPath.GetOrAdd(path, _ => ToUrlSafeBase64(RandomNumberGenerator.GetBytes(18)));
        _pathById[id] = path;

        var url = $"http://127.0.0.1:{_port}/stream/{_token.Value}/{id}";
        // Log only the file name: the full remote path discloses the user's cloud layout.
        AddLog($"CreateStreamUrl: {LogRedactor.FileName(path)} -> {url}");
        return url;
    }

    // Base64url without padding, so the id fits in a path segment.
    private static string ToUrlSafeBase64(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    // Constant-time comparison, so the token cannot be probed byte by byte.
    private bool IsValidToken(string? candidate) => _token.Matches(candidate);

    private async Task ListenLoopAsync()
    {
        var listener = _listener;
        if (listener == null) return;

        AddLog("Listen loop started, waiting for connections...");
        try
        {
            while (ReferenceEquals(_listener, listener))
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync();
                    Interlocked.Increment(ref _requestCount);
                    var reqNum = _requestCount;
                    var remoteEp = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                    AddLog($"#{reqNum} TCP accepted from {remoteEp}");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await HandleClientAsync(client, reqNum);
                        }
                        catch (Exception ex)
                        {
                            AddLog($"#{reqNum} Handler error: {ex.GetType().Name}: {ex.Message}");
                        }
                    });
                }
                catch (ObjectDisposedException)
                {
                    AddLog("Listener disposed, exiting loop.");
                    break;
                }
                catch (SocketException ex)
                {
                    // One failed accept concerns one connection; leaving the loop would kill the proxy while IsRunning still
                    // reported true.
                    AddLog($"Accept socket error: {ex.SocketErrorCode}: {ex.Message} — continuing");
                    await Task.Delay(50);
                }
                catch (Exception ex)
                {
                    AddLog($"Accept error: {ex.GetType().Name}: {ex.Message} — continuing");
                    await Task.Delay(50);
                }
            }
        }
        finally
        {
            lock (_startLock)
            {
                if (ReferenceEquals(_listener, listener))
                {
                    _listener = null;
                    _isRunning = false;
                }
            }
            AddLog("Listen loop ended.");
        }
    }

    private async Task HandleClientAsync(TcpClient client, int reqNum)
    {
        using (client)
        {
            try
            {
                using var networkStream = client.GetStream();
                networkStream.ReadTimeout = 30000;
                networkStream.WriteTimeout = 30000;

                using var reader = new StreamReader(networkStream, Encoding.UTF8, false, 4096, true);

                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(requestLine))
                {
                    AddLog($"#{reqNum} Empty request line, closing.");
                    return;
                }

                var requestParts = requestLine.Split(' ');
                if (requestParts.Length < 2 || requestParts[0] != "GET")
                {
                    AddLog($"#{reqNum} Not a GET request: {requestLine}");
                    return;
                }

                var url = requestParts[1];
                var pathPart = url.Split('?')[0];

                if (!pathPart.StartsWith("/stream/", StringComparison.Ordinal))
                {
                    AddLog($"#{reqNum} Unknown path {pathPart} — refusing");
                    await WriteErrorAsync(networkStream, 404, "Not Found");
                    return;
                }

                var segments = pathPart.Substring("/stream/".Length).Split('/');
                if (segments.Length != 2 || segments[0].Length == 0 || segments[1].Length == 0)
                {
                    AddLog($"#{reqNum} Malformed stream URL — refusing");
                    await WriteErrorAsync(networkStream, 404, "Not Found");
                    return;
                }

                if (!IsValidToken(segments[0]))
                {
                    AddLog($"#{reqNum} Invalid or missing proxy token — 403");
                    await WriteErrorAsync(networkStream, 403, "Forbidden");
                    return;
                }

                if (!_pathById.TryGetValue(segments[1], out var path))
                {
                    AddLog($"#{reqNum} Unknown stream id — 404");
                    await WriteErrorAsync(networkStream, 404, "Not Found");
                    return;
                }

                string? rangeHeader = null;
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
                {
                    if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                    {
                        rangeHeader = line.Substring(6).Trim();
                        AddLog($"#{reqNum} Range header: {rangeHeader}");
                    }
                }

                AddLog($"#{reqNum} Authorised request for a registered file");
                AddLog($"#{reqNum} Requesting from WebDAV...");

                using var webDavResponse = await _cloudProvider.GetFileResponseAsync(path, rangeHeader);

                var cl = webDavResponse.Content.Headers.ContentLength;
                AddLog($"#{reqNum} WebDAV response: {(int)webDavResponse.StatusCode} {webDavResponse.ReasonPhrase}, Content-Length: {(cl.HasValue ? cl.Value.ToString() : "N/A")}");

                // A 200 with an HTML body is almost always a login or error page; forwarding it as audio would leave the
                // player silently stuck.
                var serverType = webDavResponse.Content.Headers.ContentType?.MediaType;
                if (webDavResponse.IsSuccessStatusCode &&
                    serverType != null &&
                    serverType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"#{reqNum} WebDAV returned HTML ({serverType}) instead of audio — responding 502");
                    await WriteErrorAsync(networkStream, 502, "Bad Gateway");
                    return;
                }

                await WriteStatusAndHeadersAsync(networkStream, webDavResponse, path, reqNum);

                if (webDavResponse.IsSuccessStatusCode)
                {
                    AddLog($"#{reqNum} Streaming body to client...");
                    using var remoteStream = await webDavResponse.Content.ReadAsStreamAsync();
                    await remoteStream.CopyToAsync(networkStream);
                    await networkStream.FlushAsync();
                    AddLog($"#{reqNum} Stream complete, connection closed.");
                }
                else
                {
                    AddLog($"#{reqNum} WebDAV returned error status, closing without body.");
                }
            }
            catch (Exception ex)
            {
                AddLog($"#{reqNum} ERROR: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static async Task WriteErrorAsync(NetworkStream stream, int status, string reason)
    {
        var body = Encoding.UTF8.GetBytes($"{status} {reason}");
        var header = $"HTTP/1.1 {status} {reason}\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), 0, header.Length);
        await stream.WriteAsync(body, 0, body.Length);
        await stream.FlushAsync();
    }

    private async Task WriteStatusAndHeadersAsync(NetworkStream stream, HttpResponseMessage webDavResponse, string path, int reqNum)
    {
        var statusCode = (int)webDavResponse.StatusCode;
        var reason = webDavResponse.ReasonPhrase ?? string.Empty;
        // Use the server's own reason phrase so a non-success status is never mislabelled "OK".
        var statusText = reason.Length > 0 ? reason : "Unknown";

        var serverContentType = webDavResponse.Content.Headers.ContentType?.MediaType;
        var contentType = serverContentType ?? "audio/mpeg";
        if (string.IsNullOrEmpty(serverContentType))
        {
            if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) contentType = "audio/wav";
            else if (path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)) contentType = "audio/flac";
            else if (path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)) contentType = "audio/mp4";
            else if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) contentType = "video/mp4";
            else if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) contentType = "audio/ogg";
            else if (path.EndsWith(".opus", StringComparison.OrdinalIgnoreCase)) contentType = "audio/ogg";
        }

        if (serverContentType != null && serverContentType.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            AddLog($"#{reqNum} WARNING: WebDAV returned HTML ({serverContentType}) — likely auth failure or wrong path");
        }

        var sb = new StringBuilder();
        sb.Append("HTTP/1.1 ").Append(statusCode).Append(' ').Append(statusText).Append("\r\n");
        sb.Append("Content-Type: ").Append(contentType).Append("\r\n");
        sb.Append("Connection: close\r\n");

        if (webDavResponse.Content.Headers.ContentLength.HasValue)
        {
            sb.Append("Content-Length: ").Append(webDavResponse.Content.Headers.ContentLength.Value).Append("\r\n");
        }

        if (webDavResponse.Headers.AcceptRanges.Any())
        {
            sb.Append("Accept-Ranges: bytes\r\n");
        }

        var contentRange = webDavResponse.Content.Headers.ContentRange;
        if (contentRange != null)
        {
            sb.Append("Content-Range: ").Append(contentRange).Append("\r\n");
        }

        sb.Append("\r\n");

        var headerBytes = Encoding.ASCII.GetBytes(sb.ToString());
        AddLog($"#{reqNum} Sending response headers ({headerBytes.Length} bytes): {sb.ToString().Replace("\r\n", " | ").TrimEnd(' ', '|')}");
        await stream.WriteAsync(headerBytes, 0, headerBytes.Length);
        await stream.FlushAsync();
    }

    public void Stop()
    {
        lock (_startLock)
        {
            try
            {
                _isRunning = false;
                _listener?.Stop();
                _listener = null;

                // Ids are only meaningful for the run that issued them.
                _pathById.Clear();
                _idByPath.Clear();

                AddLog("Proxy STOPPED.");
            }
            catch (Exception ex) { AddLog($"Stop error: {ex.Message}"); }
        }
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
