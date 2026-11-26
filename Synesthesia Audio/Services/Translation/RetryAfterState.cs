namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// The Retry-After value captured from the last rate-limited response.
///
/// This state used to live on <see cref="RetryAfterCaptureHandler"/>, which was wrong in both directions: the
/// handler has to be transient for the HTTP factory to build its pipeline, so the instance holding the value was
/// not the instance the provider read from - while the service kept a third copy of its own. Splitting the state
/// out gives one shared object, and lets the handler stay transient as the factory requires.
/// </summary>
public sealed class RetryAfterState
{
    private readonly object _lock = new();
    private TimeSpan? _last;

    /// <summary>Best effort: the translator owns the request, so there is no per-call channel.</summary>
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
