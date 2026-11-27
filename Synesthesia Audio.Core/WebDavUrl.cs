namespace Synesthesia_Audio.Core;

/// <summary>
/// One rule for the base URL so "https://host/dav" and "https://host/dav/" always yield the same request paths.
/// </summary>
public static class WebDavUrl
{
    public static string NormalizeBase(string? serverUrl)
    {
        if (string.IsNullOrWhiteSpace(serverUrl)) return string.Empty;

        return serverUrl.Trim().TrimEnd('/') + "/";
    }
}