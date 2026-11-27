using System.Net;

namespace Synesthesia_Audio.Core;

/// <summary>
/// GTranslate's AggregateTranslator wraps every translator failure in an AggregateException, so the 429 sits a
/// level down; the JSON parse error bundled alongside it must not count as a separate fault.
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
