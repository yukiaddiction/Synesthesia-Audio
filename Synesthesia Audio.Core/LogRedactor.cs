namespace Synesthesia_Audio.Core;

/// <summary>
/// Reduces paths and URLs to something safe to write to the persistent log.
///
/// The app log is a file on disk that the user may share when reporting a problem, and it records cloud paths,
/// local library paths and server URLs. At Information level only the file name and a credential-free host are
/// worth recording; the full path stays available at Debug for a deliberate investigation.
/// </summary>
public static class LogRedactor
{
    /// <summary>
    /// The URL without userinfo or query string. A WebDAV URL can carry <c>https://user:pass@host/…</c>, and a
    /// query string can carry a token; neither belongs in a log file.
    /// </summary>
    public static string Url(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "(none)";
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return "(unparseable url)";

        var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty, Query = string.Empty };
        return builder.Uri.GetLeftPart(UriPartial.Path);
    }

    /// <summary>Only the last segment of a path, so a log line does not disclose the library layout.</summary>
    public static string FileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "(none)";

        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        var name = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;

        return name.Length == 0 ? "(none)" : name;
    }
}
