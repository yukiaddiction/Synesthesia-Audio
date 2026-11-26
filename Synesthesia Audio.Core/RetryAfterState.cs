namespace Synesthesia_Audio.Core;

/// <summary>
/// The Retry-After value captured from the last rate-limited response.
///
/// This is deliberately pure state with no HTTP types: the handler that writes it and the provider that reads it
/// both have to agree on one instance, and keeping the storage testable is what proves they do.
///
/// It lives in Core because its rules are subtle enough to deserve tests: a response that arrives without
/// Retry-After must never erase a value a previous response supplied, and a zero or negative value is not a
/// cooldown.
/// </summary>
public sealed class RetryAfterState
{
    private readonly object _lock = new();
    private TimeSpan? _last;

    /// <summary>Best effort: the translator owns the request, so there is no per-call channel to carry this back.</summary>
    public TimeSpan? Last
    {
        get { lock (_lock) return _last; }
    }

    /// <summary>
    /// Records a captured value. A response without Retry-After must never erase one that arrived with it.
    /// </summary>
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
