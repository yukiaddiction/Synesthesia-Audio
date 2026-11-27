using System.Text.Json.Serialization;

namespace Synesthesia_Audio.Models;

public class GeneralSettings
{
    public string BaseAudioFolderPath { get; set; } = string.Empty;
    public bool IsDarkMode { get; set; } = true;

    /// <summary>
    /// Off by default: translating unmapped tags sends them to a third-party service, which must be the user's choice.
    /// </summary>
    public bool TranslateDlsiteTags { get; set; }

    public string StorageType { get; set; } = Synesthesia_Audio.Core.StorageTypes.Local;
    public string CloudServerUrl { get; set; } = string.Empty;
    public string CloudUsername { get; set; } = string.Empty;

    // "GTranslate" is the free chain; "DeepL" and "Custom" need the user's own key.
    public string TranslationProvider { get; set; } = "GTranslate";
    public string TranslationCustomBaseUrl { get; set; } = string.Empty;
    public string TranslationCustomAuthHeader { get; set; } = "Authorization";

    // Editable because providers disagree on the scheme.
    public string TranslationCustomAuthScheme { get; set; } = "Bearer";

    // Secrets are never serialized here; they live in ISecureStore.
    [JsonIgnore]
    public string TranslationApiKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string CloudPassword { get; set; } = string.Empty;
}
