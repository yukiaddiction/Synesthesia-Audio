using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// One row of the album's track list: either a sub-album header or a track.
///
/// The list is flat rather than a grouped CollectionView on purpose. Grouping on Android measured the
/// first item of a group as a tall blank row and dropped a track (observed on device: track 01 missing with an
/// empty gap where it should be), so the rows carry their own kind and two templates render them (PERF-07).
/// </summary>
public sealed class AlbumRow
{
    private AlbumRow() { }

    public bool IsHeader { get; private init; }

    /// <summary>Header text, or the track title.</summary>
    public string Title { get; private init; } = string.Empty;

    public string TranslatedTitle { get; private init; } = string.Empty;

    public bool HasTranslation => !string.IsNullOrEmpty(TranslatedTitle);

    public Track? Track { get; private init; }

    public SubAlbum? SubAlbum { get; private init; }

    public bool IsExpanded { get; private init; }

    /// <summary>The chevron shown on an expandable header.</summary>
    public string Arrow => IsExpanded ? "\u25BE" : "\u25B8";

    /// <summary>A header is shown only when it names a real sub-album (the synthetic "Main" group is hidden).</summary>
    public bool IsHeaderVisible => IsHeader && !string.Equals(SubAlbum?.Title, MainGroupName, StringComparison.OrdinalIgnoreCase);

    public const string MainGroupName = "Main";

    public static AlbumRow ForHeader(SubAlbum subAlbum) => new()
    {
        IsHeader = true,
        SubAlbum = subAlbum,
        Title = subAlbum.Title,
        IsExpanded = subAlbum.IsExpanded
    };

    public static AlbumRow ForTrack(Track track) => new()
    {
        IsHeader = false,
        Track = track,
        Title = track.Title,
        TranslatedTitle = track.TranslatedTitle
    };
}
