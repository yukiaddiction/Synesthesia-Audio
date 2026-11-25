namespace Synesthesia_Audio.Core;

/// <summary>
/// Shared "translation services are busy" state. Kept pure so the rate-limit policy can be tested
/// without any HTTP, and so both the album button and the settings test button read one clock.
/// </summary>
public sealed class RateLimitCooldown
{
    public DateTimeOffset? Until { get; private set; }

    public bool IsActive(DateTimeOffset now) => Until.HasValue && Until.Value > now;

    public TimeSpan Remaining(DateTimeOffset now) =>
        IsActive(now) ? Until!.Value - now : TimeSpan.Zero;

    /// <summary>
    /// Extends the cooldown. A later expiry always wins, an earlier one never shortens a cooldown
    /// another request already paid for.
    /// </summary>
    public void Trip(DateTimeOffset now, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero) return;

        var expiry = now + duration;
        if (!Until.HasValue || expiry > Until.Value)
            Until = expiry;
    }

    public void Clear() => Until = null;
}
