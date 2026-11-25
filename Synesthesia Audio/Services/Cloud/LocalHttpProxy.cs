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
/// Loopback HTTP bridge used to stream WebDAV files into the media element.
///
/// Any process on the machine can reach a loopback port, so the proxy is not a public service:
/// <list type="bullet">
///   <item>it only starts when a WebDAV track actually needs streaming, so a Local library never opens a port;</item>
///   <item>every request must carry a 32-byte token generated for this run, compared in constant time;</item>
///   <item>callers reference files by an opaque id this class maps to a path, so a request can never name
///   an arbitrary file on the WebDAV account.</item>
/// </list>
/// </summary>
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

    /// <summary>Per-run secret. A request without it is refused, so another local process cannot borrow
    /// the user's WebDAV credentials.</summary>
    private readonly ProxyAccessToken _token = ProxyAccessToken.Create();

    /// <summary>Opaque id to remote path. Callers pick the id we gave them, never the path.</summary>
    private readonly ConcurrentDictionary<string, string> _pathById = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _idByPath = new(StringComparer.Ordinal);

    public LocalHttpProxy(ICloudProvider cloudProvider, ILogger<LocalHttpProxy> logger)
    {
        _cloudProvider = cloudProvider;
        _logger = logger;
        _staticLogger = logger;

        // Deliberately not started here: constructing the singleton at app start used to open a port for
        // every user, including the ones on a purely local library.
    }

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

    /// <summary>
    /// Starts the proxy if needed and returns a URL the media element can play. The path is registered
    /// behind an opaque id; the caller never passes a path to the HTTP surface.
    /// </summary>
    public string CreateStreamUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A remote path is required.", nameof(path));

        if (!EnsureRunning())
            throw new InvalidOperationException("The cloud streaming proxy could not be started.");

        var id = _idByPath.GetOrAdd(path, _ => ToUrlSafeBase64(RandomNumberGenerator.GetBytes(18)));
        _pathById[id] = path;

        var url = $"http://127.0.0.1:{_port}/stream/{_token.Value}/{id}";
        // Only the file name: the remote path discloses the user's cloud layout (SEC-m-02).
        AddLog($"CreateStreamUrl: {LogRedactor.FileName(path)} -> {url}");
        return url;
    }

    private static string ToUrlSafeBase64(byte[] bytes) =>
        Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>Constant-time token check so a local attacker cannot probe it byte by byte.</summary>
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
                    // An accept failure concerns one connection, not the whole proxy. Exiting the loop on
                    // the first one left the proxy permanently dead while IsRunning still looked true, so
                    // every later stream failed with no way to recover except a restart.
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
                // Only clear state this loop owns: a restart may already have installed a new listener, and
                // nulling it blindly would make the next EnsureRunning start a second one on the old port.
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
                    // No token, wrong token, stale token from a previous run: never forward credentials.
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

                // A 200 whose body is HTML almost always means the server answered with a login page or an
                // error page. Forwarding it as audio leaves the player silently stuck with no clue why.
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
