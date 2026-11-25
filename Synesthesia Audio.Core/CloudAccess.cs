namespace Synesthesia_Audio.Core;

/// <summary>
/// The WebDAV server rejected the credentials, or refused the request outright.
///
/// This is deliberately a distinct exception: an empty folder and "you are not allowed in" used to be the same
/// answer (<c>ListFilesAsync</c> returned an empty list for both), so a wrong password looked exactly like a
/// deleted library and the scan happily cached an empty result (PAT-05).
/// </summary>
public sealed class CloudAccessDeniedException : Exception
{
    public CloudAccessDeniedException(string message) : base(message) { }
    public CloudAccessDeniedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>The server answered, but not with something usable.</summary>
public sealed class CloudRequestFailedException : Exception
{
    public CloudRequestFailedException(string message) : base(message) { }
    public CloudRequestFailedException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Classifies a WebDAV HTTP status so callers can tell refusal from emptiness.</summary>
public static class CloudStatusClassifier
{
    /// <summary>
    /// 401 Unauthorized, 403 Forbidden and 407 Proxy Authentication Required all mean "the request was
    /// understood and refused", which must never be reported as an empty listing.
    /// </summary>
    public static bool IsAccessDenied(int statusCode) => statusCode is 401 or 403 or 407;

    /// <summary>A human-readable diagnosis for the log, without echoing the URL's credentials.</summary>
    public static string Describe(int statusCode, string? reasonPhrase)
    {
        var reason = string.IsNullOrWhiteSpace(reasonPhrase) ? "no reason phrase" : reasonPhrase.Trim();

        return statusCode switch
        {
            401 => $"The server rejected the credentials (401 {reason}). Check the user name and password.",
            403 => $"The server refused access (403 {reason}). The account may not have permission for this folder.",
            404 => $"The folder was not found (404 {reason}). Check the server URL and the library folder path.",
            407 => $"A proxy is demanding authentication (407 {reason}).",
            500 or 502 or 503 or 504 => $"The server reported a problem ({statusCode} {reason}).",
            _ => $"The server answered {statusCode} {reason}."
        };
    }
}
