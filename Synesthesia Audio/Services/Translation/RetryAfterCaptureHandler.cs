using System.Net;

using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// GTranslate's EnsureSuccessStatusCode() discards the response, so Retry-After is captured here in the pipeline.
/// Must be registered transient: the HTTP client factory rejects a reused handler, which crashed startup when it was
/// a singleton. The shared <see cref="RetryAfterState"/> keeps the transient instances in agreement.
/// </summary>
public sealed class RetryAfterCaptureHandler : DelegatingHandler
{
    private readonly RetryAfterState _state;

    public RetryAfterCaptureHandler(RetryAfterState state) => _state = state;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)529)
        {
            _state.Record(ReadRetryAfter(response));
        }

        return response;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        return RetryAfterParser.TryParse(retryAfter?.Delta, retryAfter?.Date, DateTimeOffset.UtcNow);
    }
}
