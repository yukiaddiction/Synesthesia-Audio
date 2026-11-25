namespace Synesthesia_Audio.Core;

/// <summary>
/// Checks whether an endpoint URL is safe to send credentials to.
///
/// A user-supplied translation base URL is used with an API key in a request header. Allowing plain
/// <c>http://</c> there meant a typo — or a typo'd hostname — silently shipped the key in the clear. Only the
/// local machine may be addressed over http, which keeps a local proxy or mock server usable.
/// </summary>
public static class EndpointUrl
{
    public static bool IsSecureOrLocal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return false;

        if (uri.Scheme == Uri.UriSchemeHttps) return true;
        if (uri.Scheme != Uri.UriSchemeHttp) return false;

        return uri.IsLoopback;
    }

    /// <summary>
    /// Returns <paramref name="candidate"/> when it is acceptable, otherwise <paramref name="fallback"/>.
    /// The fallback is used rather than throwing so a bad setting degrades to the official endpoint instead of
    /// breaking translation entirely.
    /// </summary>
    public static string UseOrFallback(string? candidate, string fallback) =>
        IsSecureOrLocal(candidate) ? candidate!.Trim().TrimEnd('/') : fallback;
}
