namespace Synesthesia_Audio.Core;

public static class TranslationBackoff
{
    private const int MaxDoublings = 5;

    /// <summary>
    /// Resolves the cooldown after a rate-limited translation request. The server's own Retry-After
    /// wins when we managed to capture it (GTranslate discards the response, so it is captured in the
    /// HTTP pipeline instead); otherwise exponential backoff from the configured base delay, capped.
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
