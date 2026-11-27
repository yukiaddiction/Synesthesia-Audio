namespace Synesthesia_Audio.Core;

/// <summary>
/// A user-supplied translation endpoint receives the API key, so plain http is allowed only for loopback
/// (a local proxy or mock); anything else would ship the key in the clear.
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

    /// <summary>Falls back instead of throwing so a bad setting degrades to the official endpoint rather than breaking translation.</summary>
    public static string UseOrFallback(string? candidate, string fallback) =>
        IsSecureOrLocal(candidate) ? candidate!.Trim().TrimEnd('/') : fallback;
}
