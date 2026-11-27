namespace Synesthesia_Audio.Core;

/// <summary>
/// The cover URL comes from a scraped page, so it is untrusted: only https DLsite image hosts, image content types
/// and bounded sizes are accepted.
/// </summary>
public static class CoverUrlPolicy
{
    /// <summary>A DLsite cover is tens of kilobytes; this is a generous ceiling.</summary>
    public const long MaxCoverBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedDomains = { "dlsite.com", "dlsite.jp", "format.co.jp" };

    public static bool IsAllowed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps) return false;

        var host = uri.Host;
        foreach (var domain in AllowedDomains)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)) return true;
            if (host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    public static bool IsAcceptableContentType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType)) return false;
        return mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Null length is allowed (chunked responses); a declared over-large length is not.</summary>
    public static bool IsAcceptableLength(long? contentLength) =>
        contentLength is null or (> 0 and <= MaxCoverBytes);
}
