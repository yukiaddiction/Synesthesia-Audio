namespace Synesthesia_Audio.Core;

public static class TranslationBackoff
{
    private const int MaxDoublings = 5;

    /// <summary>
    /// The server's Retry-After wins when captured (GTranslate discards the response, so the HTTP pipeline captures
    /// it); otherwise <c>base * 2^(n-1)</c>, with the exponent clamped to 0..5 and the result capped at maxDelay.
    /// </summary>
    public static TimeSpan Next(
        int consecutiveRateLimits,
        TimeSpan? serverRetryAfter,
        TimeSpan baseDelay,
        TimeSpan maxDelay)
    {
        var delay = serverRetryAfter is { } server && server > TimeSpan.Zero
            ? server
            : TimeSpan.FromMilliseconds(
                baseDelay.TotalMilliseconds * Math.Pow(2, Math.Clamp(consecutiveRateLimits - 1, 0, MaxDoublings)));

        return delay > maxDelay ? maxDelay : delay;
    }
}
