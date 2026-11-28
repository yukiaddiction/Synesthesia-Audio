using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// A folder header or a track in the album's flat list. Flat rather than a grouped CollectionView, which dropped
/// folders on Android; a template selector picks per row so the list still virtualises.
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

    public string Arrow => IsExpanded ? "\u25BE" : "\u25B8";

    /// <summary>Scene number inside its folder, printed on the row's chapter badge.</summary>
    public string SceneNumber { get; private init; } = string.Empty;

    /// <summary>Shown so a collapsed folder still says what it contains.</summary>
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
            TrackCount = count == 1 ? "1 scene" : $"{count} scenes"
        };
    }

    public static AlbumRow ForTrack(Track track, int sceneNumber) => new()
    {
        IsHeader = false,
        Track = track,
        Title = track.Title,
        TranslatedTitle = track.TranslatedTitle,
        SceneNumber = sceneNumber.ToString("00")
    };
}
