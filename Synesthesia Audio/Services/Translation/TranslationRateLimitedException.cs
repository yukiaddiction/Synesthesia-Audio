namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// Raised when a provider reports that it is being rate limited. This is a normal state with a
/// countdown, not a failure, so it is kept distinct from an ordinary translation error.
/// </summary>
public sealed class TranslationRateLimitedException : Exception
{
    public TranslationRateLimitedException(string providerName, TimeSpan? retryAfter)
        : base($"{providerName} is rate limiting translation requests.")
    {
        ProviderName = providerName;
        RetryAfter = retryAfter;
    }

    public string ProviderName { get; }

    /// <summary>The server's Retry-After, when we managed to capture it from the pipeline.</summary>
    public TimeSpan? RetryAfter { get; }
}
