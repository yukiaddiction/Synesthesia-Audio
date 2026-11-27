namespace Synesthesia_Audio.Core;

/// <summary>
/// The log file may be shared in bug reports, so Information-level entries record only file names and
/// credential-free URLs; full paths belong at Debug.
/// </summary>
public static class LogRedactor
{
    /// <summary>Strips userinfo and query: a WebDAV URL can embed <c>user:pass@</c> and a query can carry a token.</summary>
    public static string Url(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "(none)";
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return "(unparseable url)";

        var builder = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty, Query = string.Empty };
        return builder.Uri.GetLeftPart(UriPartial.Path);
    }

    public static string FileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "(none)";

        var normalized = path.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        var name = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;

        return name.Length == 0 ? "(none)" : name;
    }
}
