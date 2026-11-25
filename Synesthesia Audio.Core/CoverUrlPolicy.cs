namespace Synesthesia_Audio.Core;

/// <summary>
/// Rules for cover images fetched during a scan.
///
/// The URL comes out of a scraped page, so it is attacker-influenced input: a page could point the app at any
/// host, any scheme, or a body far larger than an image. Downloads are therefore restricted to DLsite's own
/// image hosts over https, capped in size, and required to be an image content type.
/// </summary>
public static class CoverUrlPolicy
{
    /// <summary>Ten megabytes; a DLsite cover is tens of kilobytes.</summary>
    public const long MaxCoverBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedDomains = { "dlsite.com", "dlsite.jp", "format.co.jp" };

    /// <summary>
    /// True when <paramref name="url"/> is https and served from a DLsite domain (or a subdomain of one).
    /// </summary>
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

    /// <summary>A cover response must actually be an image.</summary>
    public static bool IsAcceptableContentType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType)) return false;
        return mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Null length is allowed (chunked responses); a declared over-large length is not.</summary>
    public static bool IsAcceptableLength(long? contentLength) =>
        contentLength is null or (> 0 and <= MaxCoverBytes);
}
