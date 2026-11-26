namespace Synesthesia_Audio.Services;

public interface ITranslationService
{
    /// <summary>
    /// Translates one string. When <paramref name="config"/> is given it is used instead of the saved
    /// settings, which is how the Test button verifies unsaved values without mutating shared state (DUP-03).
    /// </summary>
    Task<string> TranslateJaToEnAsync(string japaneseText, TranslationConfig? config = null);
    /// <summary>Translates a batch. <paramref name="config"/> overrides the saved settings for this call only.</summary>
    Task<IReadOnlyList<string>> TranslateJaToEnBatchAsync(IReadOnlyList<string> japaneseTexts, TranslationConfig? config = null);
    string ToRomaji(string japaneseText);
    bool ContainsJapanese(string text);
    string? LastError { get; }
    void ClearError();

    /// <summary>The provider that is currently configured, for display in settings.</summary>
    string ActiveProviderName { get; }

    /// <summary>
    /// Non-null while the shared rate-limit cooldown is running. Rate limiting is a state with a
    /// countdown, not an error, so it is reported separately from <see cref="LastError"/>.
    /// </summary>
    TimeSpan? RateLimitRemaining { get; }
}

/// <summary>Accepted values for <c>GeneralSettings.TranslationProvider</c>.</summary>
public static class TranslationProviders
{
    public const string GTranslate = "GTranslate";
    public const string DeepL = "DeepL";
    public const string Custom = "Custom";

    // string[] rather than IReadOnlyList so it can be handed straight to a Picker's ItemsSource.
    public static readonly string[] All = { GTranslate, DeepL, Custom };
}
/// <summary>
/// An explicit provider configuration for one call.
///
/// The settings form used to verify what the user had typed by swapping the live settings, running one probe,
/// then swapping them back — so a concurrent reader could observe a configuration that was never saved, and the
/// restore had to name every field or leak one (DUP-03). Passing the values in removes both problems.
/// </summary>
public sealed record TranslationConfig(
    string Provider,
    string ApiKey,
    string CustomBaseUrl,
    string CustomAuthHeader,
    string CustomAuthScheme)
{
    /// <summary>The currently saved configuration.</summary>
    public static TranslationConfig FromSettings(Models.GeneralSettings settings) => new(
        settings.TranslationProvider,
        settings.TranslationApiKey,
        settings.TranslationCustomBaseUrl,
        settings.TranslationCustomAuthHeader,
        settings.TranslationCustomAuthScheme);
}
