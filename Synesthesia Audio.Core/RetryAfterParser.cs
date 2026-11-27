namespace Synesthesia_Audio.Core;

public static class RetryAfterParser
{
    /// <summary>
    /// Prefers the delta form (<c>Retry-After: 30</c>) over the HTTP-date form. A past date or non-positive delta
    /// means no wait and yields null.
    /// </summary>
    public static TimeSpan? TryParse(TimeSpan? delta, DateTimeOffset? date, DateTimeOffset now)
    {
        if (delta is { } explicitDelta)
        {
            return explicitDelta > TimeSpan.Zero ? explicitDelta : null;
        }

        if (date is { } until)
        {
            var wait = until - now;
            return wait > TimeSpan.Zero ? wait : null;
        }

        return null;
    }
}
