using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace Synesthesia_Audio.Models;

public class WorkMetadata
{
    public string ProductId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TranslatedTitle { get; set; } = string.Empty;
    public string Circle { get; set; } = string.Empty;
    public string VA { get; set; } = string.Empty;
    public string CoverImagePath { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public string AgeRating { get; set; } = "SFW";
    public bool IsDlsiteWork => !string.IsNullOrEmpty(ProductId);
    public bool IsNsfw => AgeRating.Equals("NSFW", StringComparison.OrdinalIgnoreCase);
    public string FolderPath { get; set; } = string.Empty;
    public ObservableCollection<SubAlbum> SubAlbums { get; set; } = new();

    [JsonIgnore]
    public string CoverUrl { get; set; } = string.Empty;
}
