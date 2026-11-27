using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// One row of the album's track list: a folder header, or a track.
///
/// The list is flat rather than a grouped CollectionView: grouping rendered a single group and dropped folders on
/// Android. Each row says what it is and a template selector picks the header or track template, so the list still
/// virtualises - a row is only materialised when it is on screen (PERF-07).
/// </summary>
public sealed class AlbumRow
{
    private AlbumRow() { }

    public bool IsHeader { get; private init; }

    public string Title { get; private init; } = string.Empty;

    public string TranslatedTitle { get; private init; } = string.Empty;

    public bool HasTranslation => !string.IsNullOrEmpty(TranslatedTitle);

    public Track? Track { get; private init; }

    public SubAlbum? SubAlbum { get; private init; }

    public bool IsExpanded { get; private init; }

    /// <summary>The chevron on a folder header.</summary>
    public string Arrow => IsExpanded ? "\u25BE" : "\u25B8";

    /// <summary>How many tracks the folder holds, so a collapsed folder still says what it contains.</summary>
    public string TrackCount { get; private init; } = string.Empty;

    public static AlbumRow ForHeader(SubAlbum subAlbum)
    {
        var count = subAlbum.Tracks.Count;
        return new AlbumRow
        {
            IsHeader = true,
            SubAlbum = subAlbum,
            Title = subAlbum.Title,
            IsExpanded = subAlbum.IsExpanded,
            TrackCount = count == 1 ? "1 track" : $"{count} tracks"
        };
    }

    public static AlbumRow ForTrack(Track track) => new()
    {
        IsHeader = false,
        Track = track,
        Title = track.Title,
        TranslatedTitle = track.TranslatedTitle
    };
}
