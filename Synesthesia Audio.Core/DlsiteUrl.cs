namespace Synesthesia_Audio.Core;

public static class DlsiteUrl
{
    private const string HttpsPrefix = "https:";

    /// <summary>
    /// DLsite writes some image sources protocol-relative ("//img.dlsite.jp/..."), which neither <c>Uri</c> nor an
    /// image loader treats as absolute. Blank input stays empty rather than becoming "https:".
    /// </summary>
    public static string EnsureAbsolute(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        var trimmed = url.Trim();

        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return HttpsPrefix + trimmed;

        return trimmed;
    }

    /// <summary>Rejects junk scraping picks up, such as a bare attribute name.</summary>
    public static bool IsUsableImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
