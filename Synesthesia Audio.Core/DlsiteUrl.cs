namespace Synesthesia_Audio.Core;

/// <summary>
/// URL fix-ups for values scraped out of DLsite markup.
///
/// DLsite writes some image sources as protocol-relative ("//img.dlsite.jp/..."), which no <c>Uri</c> will
/// treat as absolute and no image loader will fetch. The same two-line fix appeared four times inside the parser,
/// which is four places to miss when a fifth form turns up (DUP-08).
/// </summary>
public static class DlsiteUrl
{
    private const string HttpsPrefix = "https:";

    /// <summary>
    /// Makes a protocol-relative URL absolute, and leaves anything else untouched. An empty or whitespace value
    /// stays empty rather than becoming "https:".
    /// </summary>
    public static string EnsureAbsolute(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var trimmed = url.Trim();

        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return HttpsPrefix + trimmed;

        return trimmed;
    }

    /// <summary>
    /// True when the value is something that can be handed to an image loader: an absolute http(s) URL. Used to
    /// reject junk that scraping picks up, such as a bare attribute name.
    /// </summary>
    public static bool IsUsableImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
