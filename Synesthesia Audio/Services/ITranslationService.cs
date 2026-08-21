namespace Synesthesia_Audio.Services;

public interface ITranslationService
{
    Task<string> TranslateJaToEnAsync(string japaneseText);
    Task<IReadOnlyList<string>> TranslateJaToEnBatchAsync(IReadOnlyList<string> japaneseTexts);
    string ToRomaji(string japaneseText);
    bool ContainsJapanese(string text);
    string? LastError { get; }
    void ClearError();
}
