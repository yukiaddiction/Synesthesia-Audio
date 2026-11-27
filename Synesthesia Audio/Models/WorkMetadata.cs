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

    // A null AgeRating in an older cache file used to throw here, which broke the library filter.
    public bool IsNsfw => Synesthesia_Audio.Core.AgeRatings.IsNsfw(AgeRating);
    public bool IsSfw => Synesthesia_Audio.Core.AgeRatings.IsSfw(AgeRating);

    /// <summary>True when the rating could not be determined, so the work must not be shown as all-ages.</summary>
    public bool IsAgeRatingUnknown => Synesthesia_Audio.Core.AgeRatings.IsUnknown(AgeRating);
    public string FolderPath { get; set; } = string.Empty;
    public ObservableCollection<SubAlbum> SubAlbums { get; set; } = new();

    /// <summary>
    /// Set when the last metadata fetch failed, so the next scan retries instead of keeping a
    /// folder-name-only work forever.
    /// </summary>
    public bool NeedsMetadataRefresh { get; set; }

    /// <summary>
    /// The remote cover URL, persisted so a deleted or cache-cleared cover can be downloaded again without
    /// scraping the page.
    /// </summary>
    public string CoverUrl { get; set; } = string.Empty;

    /// <summary>
    /// The cloud server's change tag for <see cref="FolderPath"/> at the last scan. On a server that changes a folder's
    /// tag whenever anything beneath it changes, an unchanged tag lets the next scan reuse
    /// <see cref="SourceFiles"/> instead of listing the folder again.
    /// </summary>
    public string SourceETag { get; set; } = string.Empty;

    /// <summary>
    /// Every audio file beneath <see cref="FolderPath"/> at the last scan, duplicates in other formats included
    /// (the tracks keep only the best copy, so they cannot stand in for this).
    /// </summary>
    public List<string> SourceFiles { get; set; } = new();
}
