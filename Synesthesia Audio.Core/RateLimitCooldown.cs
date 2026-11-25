namespace Synesthesia_Audio.Core;

/// <summary>
/// Shared "translation services are busy" state. Kept pure so the rate-limit policy can be tested
/// without any HTTP, and so both the album button and the settings test button read one clock.
///
/// It is read and written from translation calls that run concurrently, so every access is under a lock:
/// an unsynchronised read-modify-write in <see cref="Trip"/> could keep a shorter expiry than one another
/// request had already paid for (BUG-m-22).
/// </summary>
public sealed class RateLimitCooldown
{
    private readonly object _lock = new();
    private DateTimeOffset? _until;

    public DateTimeOffset? Until
    {
        get { lock (_lock) return _until; }
    }

    public bool IsActive(DateTimeOffset now)
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
    /// Extends the cooldown. A later expiry always wins, an earlier one never shortens a cooldown
    /// another request already paid for.
    /// </summary>
    public void Trip(DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;

        var expiry = now + duration;
        lock (_lock)
        {
            if (!_until.HasValue || expiry > _until.Value) _until = expiry;
        }
    }

    public void Clear()
    {
        lock (_lock) _until = null;
    }
}
