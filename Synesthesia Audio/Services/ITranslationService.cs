namespace Synesthesia_Audio.Services;

public interface ITranslationService
{
    Task<string> TranslateJaToEnAsync(string japaneseText);
    Task<IReadOnlyList<string>> TranslateJaToEnBatchAsync(IReadOnlyList<string> japaneseTexts);
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
