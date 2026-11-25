using System.Net;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// GTranslate calls EnsureSuccessStatusCode(), which throws the response away before our code can
/// read it — so Retry-After is recovered from the HTTP pipeline instead of the exception.
/// </summary>
public sealed class RetryAfterCaptureHandler : DelegatingHandler
{
    public RetryAfterCaptureHandler() : base(new HttpClientHandler())
    {
    }

    private readonly object _lock = new();
    private TimeSpan? _lastRetryAfter;

    /// <summary>
    /// Best-effort: the translator owns the request, so there is no per-call channel to carry this back.
    /// Access is locked because translation calls run concurrently (BUG-m-22).
    /// </summary>
    public TimeSpan? LastRetryAfter
    {
        get { lock (_lock) return _lastRetryAfter; }
        private set { lock (_lock) _lastRetryAfter = value; }
    }

    public void Reset()
    {
        lock (_lock) _lastRetryAfter = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)529)
        {
            var captured = ReadRetryAfter(response);
            lock (_lock)
            {
                // Never let a response without Retry-After erase one that arrived with it.
                _lastRetryAfter = captured ?? _lastRetryAfter;
            }
        }

        return response;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta) return delta;

        if (retryAfter?.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero) return wait;
        }

        return null;
    }
}
