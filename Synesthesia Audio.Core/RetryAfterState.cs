namespace Synesthesia_Audio.Core;

/// <summary>
/// Shared between the handler that captures Retry-After and the provider that reads it, since the translator owns
/// the request and there is no per-call channel to carry it back.
/// </summary>
public sealed class RetryAfterState
{
    private readonly object _lock = new();
    private TimeSpan? _last;

    public TimeSpan? Last
    {
        get { lock (_lock) return _last; }
    }

    /// <summary>A response without Retry-After must never erase an earlier value, and non-positive values are not cooldowns.</summary>
    public void Record(TimeSpan? captured)
    {
        if (captured is not { } value || value <= TimeSpan.Zero) return;

        lock (_lock) _last = value;
    }

    public void Reset()
    {
        lock (_lock) _last = null;
    }
}
