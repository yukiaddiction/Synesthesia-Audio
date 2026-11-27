namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// A rate limit is a normal state with a countdown, not a failure, so it is kept apart from ordinary errors.
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

    public TimeSpan? RetryAfter { get; }
}
