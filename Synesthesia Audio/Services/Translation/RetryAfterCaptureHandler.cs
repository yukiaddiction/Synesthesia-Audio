using System.Net;

using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// GTranslate calls EnsureSuccessStatusCode(), which throws the response away before our code can read it — so
/// Retry-After is recovered from the HTTP pipeline instead of the exception.
///
/// This must be registered <c>transient</c>: the HTTP client factory builds one pipeline per handler lifetime and
/// rejects a reused handler ("The 'InnerHandler' property must be null"). Registering it as a singleton made
/// <c>CreateClient("translation")</c> throw, which took the whole app down at startup because the translation
/// service is constructed there. Its captured value lives in <see cref="RetryAfterState"/> so the transient
/// instances still share one answer.
/// </summary>
public sealed class RetryAfterCaptureHandler : DelegatingHandler
{
    private readonly RetryAfterState _state;

    // No base handler of our own: the factory supplies the primary handler and sets InnerHandler.
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
