using System.Text.Json.Serialization;

namespace Synesthesia_Audio.Models;

public class GeneralSettings
{
    public string BaseAudioFolderPath { get; set; } = string.Empty;
    public bool IsDarkMode { get; set; } = true;

    /// <summary>
    /// Opt-in: when enabled, Japanese tags that are not in the local mapping are machine-translated during
    /// a scan, which sends them to a third-party translation service. Off by default so a fresh install
    /// uploads nothing from the user's library until they ask for it.
    /// </summary>
    public bool TranslateDlsiteTags { get; set; }

    public string StorageType { get; set; } = "Local";
    public string CloudServerUrl { get; set; } = string.Empty;
    public string CloudUsername { get; set; } = string.Empty;

    // Translation provider. "GTranslate" is the free chain; "DeepL" and "Custom" use the user's own
    // paid key. The key itself is never serialized here — it lives in ISecureStore.
    public string TranslationProvider { get; set; } = "GTranslate";
    public string TranslationCustomBaseUrl { get; set; } = string.Empty;
    public string TranslationCustomAuthHeader { get; set; } = "Authorization";

    [JsonIgnore]
    public string TranslationApiKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string CloudPassword { get; set; } = string.Empty;
}
