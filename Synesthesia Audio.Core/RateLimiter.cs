namespace Synesthesia_Audio.Core;

/// <summary>
/// Owns the cooldown clock and the consecutive-limit count together so they cannot drift apart. Not
/// translation-specific, so other remote services can share the policy.
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
    /// Uses the server's Retry-After when given, else capped exponential backoff. A later expiry always wins, so an
    /// earlier one never shortens a cooldown another request already paid for.
    /// </summary>
    public TimeSpan RecordRateLimit(DateTimeOffset now, TimeSpan? serverRetryAfter)
    {
        TimeSpan delay;

        lock (_lock)
        {
            // Incremented under the lock so two concurrent limits cannot read the same count and share a backoff step.
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

    public void RecordSuccess()
    {
        lock (_lock) _consecutiveLimits = 0;
    }
}
