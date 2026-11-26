namespace Synesthesia_Audio.Core;

/// <summary>
/// The one rule for turning a user-typed server address into a WebDAV base URL: trimmed of trailing slashes, with
/// exactly one added.
///
/// This was written twice - once on the connection object and once where file URLs are built (DUP-08) - so the two
/// could have disagreed about a URL typed with a trailing slash, and a base of "https://host/dav" versus
/// "https://host/dav/" produced different request paths.
/// </summary>
public static class WebDavUrl
{
    /// <summary>Adds the single trailing slash a base URL needs. Empty input stays empty.</summary>
    public static string NormalizeBase(string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl)) return string.Empty;

        return serverUrl.Trim().TrimEnd('/') + "/";
    }
}