namespace Synesthesia_Audio.Core;

/// <summary>
/// Turns a <c>Retry-After</c> answer into how long to wait.
///
/// The same six lines lived in two places — the DeepL provider and the capture handler — so one of them could
/// drift from the other without anything failing to compile (DUP-08). Kept here as pure arithmetic on values, so
/// it can be tested without an <c>HttpResponseMessage</c>.
/// </summary>
public static class RetryAfterParser
{
    /// <summary>
    /// Prefers the delta form (<c>Retry-After: 30</c>); falls back to the date form (<c>Retry-After: Wed, 21 Oct
    /// 2015 07:28:00 GMT</c>) as a wait from <paramref name="now"/>. A date already in the past means "no wait",
    /// which is reported as null rather than a negative span.
    /// </summary>
    public static TimeSpan? TryParse(TimeSpan? delta, DateTimeOffset? date, DateTimeOffset now)
    {
        if (delta is { } explicitDelta)
        {
            // A server may answer 0 or negative; there is nothing to wait for in that case.
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
