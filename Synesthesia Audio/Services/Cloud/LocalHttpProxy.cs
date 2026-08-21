using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Cloud;

public class LocalHttpProxy : IDisposable
{
    private static readonly ConcurrentQueue<string> _appLog = new();
    private const int MaxLogEntries = 500;
    private static ILogger? _staticLogger;

    public static void Log(string msg)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        _appLog.Enqueue(entry);
        while (_appLog.Count > MaxLogEntries) _appLog.TryDequeue(out _);
        _staticLogger?.LogDebug("[LocalHttpProxy] {Message}", msg);
    }

    private volatile TcpListener? _listener;
    private readonly ICloudProvider _cloudProvider;
    private readonly ILogger<LocalHttpProxy> _logger;
    private readonly object _startLock = new();
    private volatile int _port;
    private volatile bool _isRunning;
    private int _requestCount;

    public bool IsRunning => _isRunning;
    public int Port => _port;

    public IReadOnlyList<string> Entries
    {
        get
        {
            var entries = _appLog.ToList();
            entries.Reverse();
            return entries;
        }
    }

    public string LogText => string.Join("\n", Entries);

    public LocalHttpProxy(ICloudProvider cloudProvider, ILogger<LocalHttpProxy> logger)
    {
        _cloudProvider = cloudProvider;
        _logger = logger;
        _staticLogger = logger;
        Start();
    }

    private void AddLog(string msg)
    {
        Log(msg);
    }

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
                AddLog($"Loopback IPs: {string.Join(", ", Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))}");
                _ = Task.Run(ListenLoopAsync);
            }
            catch (Exception ex)
            {
                _isRunning = false;
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

    public string GetProxyUrl(string path)
    {
        var base64Path = Convert.ToBase64String(Encoding.UTF8.GetBytes(path));
        var url = $"http://127.0.0.1:{_port}/stream?path={Uri.EscapeDataString(base64Path)}";
        AddLog($"GetProxyUrl: {path.Substring(0, Math.Min(path.Length, 60))}... -> {url}");
        return url;
    }

    private async Task ListenLoopAsync()
    {
        AddLog("Listen loop started, waiting for connections...");
        try
        {
            while (_listener != null)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();
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
                    AddLog($"Accept socket error: {ex.SocketErrorCode}: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    AddLog($"Accept error: {ex.GetType().Name}: {ex.Message}");
                    break;
                }
            }
        }
        finally
        {
            lock (_startLock)
            {
                _isRunning = false;
                _listener = null;
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

                AddLog($"#{reqNum} > {requestLine}");

                var requestParts = requestLine.Split(' ');
                if (requestParts.Length < 2 || requestParts[0] != "GET")
                {
                    AddLog($"#{reqNum} Not a GET request: {requestLine}");
                    return;
                }

                var url = requestParts[1];
                var queryIndex = url.IndexOf('?');
                if (queryIndex < 0)
                {
                    AddLog($"#{reqNum} No query string in URL: {url}");
                    return;
                }

                var pathPart = url.Substring(0, queryIndex);
                var queryPart = url.Substring(queryIndex + 1);

                if (!pathPart.StartsWith("/stream"))
                {
                    AddLog($"#{reqNum} Not /stream path: {pathPart}");
                    return;
                }

                var base64Path = ExtractQueryParam(queryPart, "path");
                if (string.IsNullOrEmpty(base64Path))
                {
                    AddLog($"#{reqNum} No 'path' param in query: {queryPart}");
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

                var path = Encoding.UTF8.GetString(Convert.FromBase64String(
                    Uri.UnescapeDataString(base64Path)));

                AddLog($"#{reqNum} Decoded path: {path}");
                AddLog($"#{reqNum} Requesting from WebDAV...");

                using var webDavResponse = await _cloudProvider.GetFileResponseAsync(path, rangeHeader);

                var cl = webDavResponse.Content.Headers.ContentLength;
                AddLog($"#{reqNum} WebDAV response: {(int)webDavResponse.StatusCode} {webDavResponse.ReasonPhrase}, Content-Length: {(cl.HasValue ? cl.Value.ToString() : "N/A")}");

                await WriteStatusAndHeadersAsync(networkStream, webDavResponse, path, reqNum);

                if (webDavResponse.IsSuccessStatusCode)
                {
                    var ct = webDavResponse.Content.Headers.ContentType?.MediaType ?? "";
                    if (ct.Contains("html", StringComparison.OrdinalIgnoreCase))
                    {
                        AddLog($"#{reqNum} WebDAV returned HTML instead of audio — check credentials and path!");
                    }
                    else
                    {
                        AddLog($"#{reqNum} Streaming body to client...");
                        using var remoteStream = await webDavResponse.Content.ReadAsStreamAsync();
                        await remoteStream.CopyToAsync(networkStream);
                        await networkStream.FlushAsync();
                        AddLog($"#{reqNum} Stream complete, connection closed.");
                    }
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

    private async Task WriteStatusAndHeadersAsync(NetworkStream stream, HttpResponseMessage webDavResponse, string path, int reqNum)
    {
        var statusCode = (int)webDavResponse.StatusCode;
        var reason = webDavResponse.ReasonPhrase ?? string.Empty;
        var statusText = statusCode switch
        {
            200 => "OK",
            206 => "Partial Content",
            301 => "Moved Permanently",
            302 => "Found",
            304 => "Not Modified",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            405 => "Method Not Allowed",
            416 => "Range Not Satisfiable",
            500 => "Internal Server Error",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            _ => reason.Length > 0 ? reason : "OK"
        };

        var serverContentType = webDavResponse.Content.Headers.ContentType?.MediaType;
        var contentType = serverContentType ?? "audio/mpeg";
        if (string.IsNullOrEmpty(serverContentType))
        {
            if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) contentType = "audio/wav";
            else if (path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase)) contentType = "audio/flac";
            else if (path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase)) contentType = "audio/mp4";
            else if (path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)) contentType = "video/mp4";
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

    private static string? ExtractQueryParam(string queryString, string paramName)
    {
        var pairs = queryString.Split('&');
        foreach (var pair in pairs)
        {
            var eqIndex = pair.IndexOf('=');
            if (eqIndex >= 0)
            {
                var key = pair.Substring(0, eqIndex);
                var value = pair.Substring(eqIndex + 1);
                if (string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
                {
                    return value;
                }
            }
        }
        return null;
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
