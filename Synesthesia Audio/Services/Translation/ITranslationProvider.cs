namespace Synesthesia_Audio.Services.Translation;

public interface ITranslationProvider
{
    string Name { get; }

    /// <summary>
    /// Translates Japanese to English. Returns null when the provider is unavailable and throws
    /// <see cref="TranslationRateLimitedException"/> on a rate limit.
    /// </summary>
    Task<IReadOnlyList<string>?> TranslateBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
