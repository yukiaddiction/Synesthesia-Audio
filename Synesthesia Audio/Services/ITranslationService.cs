namespace Synesthesia_Audio.Services;

public interface ITranslationService
{
    /// <summary>
    /// A non-null <paramref name="config"/> overrides the saved settings for this call only, so the Test button can
    /// probe unsaved values without mutating shared state.
    /// </summary>
    Task<string> TranslateJaToEnAsync(string japaneseText, TranslationConfig? config = null);
    Task<IReadOnlyList<string>> TranslateJaToEnBatchAsync(IReadOnlyList<string> japaneseTexts, TranslationConfig? config = null);
    string ToRomaji(string japaneseText);
    bool ContainsJapanese(string text);
    string? LastError { get; }
    void ClearError();

    string ActiveProviderName { get; }

    /// <summary>
    /// Non-null during the shared rate-limit cooldown. Kept apart from <see cref="LastError"/> because it is a
    /// countdown state, not an error.
    /// </summary>
    TimeSpan? RateLimitRemaining { get; }
}

/// <summary>Accepted values for <c>GeneralSettings.TranslationProvider</c>.</summary>
public static class TranslationProviders
{
    public const string GTranslate = "GTranslate";
    public const string DeepL = "DeepL";
    public const string Custom = "Custom";

    // An array so it can go straight into a Picker's ItemsSource.
    public static readonly string[] All = { GTranslate, DeepL, Custom };
}
/// <summary>
/// Provider settings passed explicitly to one call, so probing unsaved values never swaps the live settings where
/// a concurrent reader could see them.
/// </summary>
public sealed record TranslationConfig(
    string Provider,
    string ApiKey,
    string CustomBaseUrl,
    string CustomAuthHeader,
    string CustomAuthScheme)
{
    public static TranslationConfig FromSettings(Models.GeneralSettings settings) => new(
        settings.TranslationProvider,
        settings.TranslationApiKey,
        settings.TranslationCustomBaseUrl,
        settings.TranslationCustomAuthHeader,
        settings.TranslationCustomAuthScheme);
}
