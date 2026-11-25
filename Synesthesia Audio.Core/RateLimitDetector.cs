using System.Net;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Recognises a rate limit inside whatever a provider threw. GTranslate's AggregateTranslator wraps
/// every translator's failure in an AggregateException, so the 429 sits one level down and has to be
/// unwrapped; the bundled JSON parse error on the 429 body must be ignored, not treated as a fault.
/// </summary>
public static class RateLimitDetector
{
    public static bool IsRateLimit(Exception? exception)
    {
        switch (exception)
        {
            case null:
                return false;
            case HttpRequestException http:
                return IsRateLimitStatus(http.StatusCode);
            case AggregateException aggregate:
                return aggregate.Flatten().InnerExceptions.Any(IsRateLimit);
            default:
                return false;
        }
    }

    private static bool IsRateLimitStatus(HttpStatusCode? statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)529;
}
