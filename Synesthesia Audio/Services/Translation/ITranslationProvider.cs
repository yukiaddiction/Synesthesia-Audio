namespace Synesthesia_Audio.Services.Translation;

/// <summary>
/// One translation backend. Batch is the only entry point so DeepL's native text array and
/// GTranslate's one-request-per-string loop share a single surface.
/// </summary>
public interface ITranslationProvider
{
    string Name { get; }

    /// <summary>
    /// Translates Japanese to English. Returns null when the provider is simply unavailable, and
    /// throws <see cref="TranslationRateLimitedException"/> when it reports a rate limit.
    /// </summary>
    Task<IReadOnlyList<string>?> TranslateBatchAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
