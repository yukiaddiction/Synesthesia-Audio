namespace Synesthesia_Audio.Core;

/// <summary>
/// Kept distinct from an empty listing so a wrong password is never mistaken for a deleted library and cached as empty.
/// </summary>
public sealed class CloudAccessDeniedException : Exception
{
    public CloudAccessDeniedException(string message) : base(message) { }
    public CloudAccessDeniedException(string message, Exception inner) : base(message, inner) { }
}

public sealed class CloudRequestFailedException : Exception
{
    public CloudRequestFailedException(string message) : base(message) { }
    public CloudRequestFailedException(string message, Exception inner) : base(message, inner) { }
}

public static class CloudStatusClassifier
{
    /// <summary>401, 403 and 407 mean the request was refused, which must never be reported as an empty listing.</summary>
    public static bool IsAccessDenied(int statusCode) => statusCode is 401 or 403 or 407;

    /// <summary>Takes only the status, never the URL, so credentials embedded in it cannot reach the log.</summary>
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
