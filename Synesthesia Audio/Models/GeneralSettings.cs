using System.Text.Json.Serialization;

namespace Synesthesia_Audio.Models;

public class GeneralSettings
{
    public string BaseAudioFolderPath { get; set; } = string.Empty;
    public bool IsDarkMode { get; set; } = true;
    public bool TranslateDlsiteTags { get; set; } = true;

    public string StorageType { get; set; } = "Local";
    public string CloudServerUrl { get; set; } = string.Empty;
    public string CloudUsername { get; set; } = string.Empty;

    [JsonIgnore]
    public string CloudPassword { get; set; } = string.Empty;
}
