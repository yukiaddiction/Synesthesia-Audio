namespace Synesthesia_Audio.Core;

/// <summary>
/// One place that owns "we are being rate limited": the cooldown clock, how many limits arrived in a row, and
/// the delay each one earns.
///
/// This replaced two separate pieces of state — a <c>RateLimitCooldown</c> for the clock and a bare counter for
/// the backoff step — which had to be updated together and could drift apart (OVR-04). It is deliberately not
/// translation-specific so a second remote service (DLsite) can use the same policy instead of growing its own.
/// </summary>
public sealed class RateLimiter
{
    private readonly object _lock = new();
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;

    private DateTimeOffset? _until;
    private int _consecutiveLimits;

    public RateLimiter(TimeSpan baseDelay, TimeSpan maxDelay)
    {
        if (baseDelay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(baseDelay));
        if (maxDelay < baseDelay) throw new ArgumentOutOfRangeException(nameof(maxDelay));

        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
    }

    /// <summary>How many rate limits have arrived since the last success.</summary>
    public int ConsecutiveLimits
    {
        get { lock (_lock) return _consecutiveLimits; }
    }

    public DateTimeOffset? Until
    {
        get { lock (_lock) return _until; }
    }

    public bool IsBlocked(DateTimeOffset now)
    {
        lock (_lock) return _until.HasValue && _until.Value > now;
    }

    public TimeSpan Remaining(DateTimeOffset now)
    {
        lock (_lock)
        {
            return _until.HasValue && _until.Value > now ? _until.Value - now : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Records a rate limit and returns the cooldown it earned: the server's own Retry-After when there is one,
    /// otherwise exponential backoff from the base delay, capped at the maximum.
    ///
    /// A later expiry always wins; an earlier one never shortens a cooldown another request already paid for.
    /// </summary>
    public TimeSpan RecordRateLimit(DateTimeOffset now, TimeSpan? serverRetryAfter)
    {
        TimeSpan delay;

        lock (_lock)
        {
            // Counted inside the lock: two concurrent limits used to read the same count and so advanced to the
            // same backoff step.
            _consecutiveLimits++;

            delay = TranslationBackoff.Next(_consecutiveLimits, serverRetryAfter, _baseDelay, _maxDelay);

            if (delay > TimeSpan.Zero)
            {
                var until = now + delay;
                if (_until is not { } existing || until > existing) _until = until;
            }
        }

        return delay;
    }

    /// <summary>Resets the count. A success means the next limit earns the shortest backoff again.</summary>
    public void RecordSuccess()
    {
        lock (_lock) _consecutiveLimits = 0;
    }
}
