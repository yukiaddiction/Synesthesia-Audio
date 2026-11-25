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

    public TimeSpan? LastRetryAfter { get; private set; }

    public void Reset() => LastRetryAfter = null;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)529)
            LastRetryAfter = ReadRetryAfter(response) ?? LastRetryAfter;

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
