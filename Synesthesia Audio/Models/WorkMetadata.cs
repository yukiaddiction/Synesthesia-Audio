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
    public string AgeRating { get; set; } = Synesthesia_Audio.Core.AgeRatings.Sfw;
    public bool IsDlsiteWork => !string.IsNullOrEmpty(ProductId);

    // Goes through AgeRatings because older cache files can hold a null AgeRating.
    public bool IsNsfw => Synesthesia_Audio.Core.AgeRatings.IsNsfw(AgeRating);
    public bool IsSfw => Synesthesia_Audio.Core.AgeRatings.IsSfw(AgeRating);

    public bool IsAgeRatingUnknown => Synesthesia_Audio.Core.AgeRatings.IsUnknown(AgeRating);
    public string FolderPath { get; set; } = string.Empty;
    public ObservableCollection<SubAlbum> SubAlbums { get; set; } = new();

    /// <summary>Set when the last fetch failed, so the next scan retries instead of keeping a folder-name-only work forever.</summary>
    public bool NeedsMetadataRefresh { get; set; }

    /// <summary>Persisted so a lost cover can be re-downloaded without scraping the page again.</summary>
    public string CoverUrl { get; set; } = string.Empty;

    /// <summary>
    /// The folder's cloud ETag at the last scan; if unchanged, the next scan reuses <see cref="SourceFiles"/> instead of listing again.
    /// </summary>
    public string SourceETag { get; set; } = string.Empty;

    /// <summary>
    /// Every audio file at the last scan, including other-format duplicates, which the deduplicated tracks can't stand in for.
    /// </summary>
    public List<string> SourceFiles { get; set; } = new();
}
