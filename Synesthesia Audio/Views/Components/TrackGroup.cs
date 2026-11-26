using System.Collections.ObjectModel;
using System.ComponentModel;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// One sub-album as a CollectionView group.
///
/// A grouped <see cref="CollectionView"/> renders the items of the group it is given, which is what makes the track
/// list virtualised - the page used to nest a BindableLayout per sub-album inside a ScrollView, so every track in
/// the album became a view up front (PERF-07).
///
/// Collapsing is expressed as emptying and refilling this collection rather than hiding a nested panel: the
/// collection is observable, so the view follows along and only the visible tracks are ever materialised.
/// </summary>
public sealed class TrackGroup : ObservableCollection<Track>, INotifyPropertyChanged
{
    private readonly IReadOnlyList<Track> _allTracks;

    public TrackGroup(SubAlbum subAlbum)
    {
        Title = subAlbum.Title;
        FolderPath = subAlbum.FolderPath;
        SubAlbum = subAlbum;
        _allTracks = subAlbum.Tracks.ToList();

        if (subAlbum.IsExpanded) AddAll();
    }

    public SubAlbum SubAlbum { get; }

    public string Title { get; }

    public string FolderPath { get; }

    /// <summary>True while this sub-album's tracks are shown.</summary>
    public bool IsExpanded => Count > 0;

    /// <summary>
    /// Shows or hides this sub-album's tracks. The group's own items change, so nothing above it has to be rebuilt
    /// and the rest of the album keeps its scroll position.
    /// </summary>
    public void SetExpanded(bool expanded)
    {
        SubAlbum.IsExpanded = expanded;

        if (expanded) AddAll();
        else Clear();

        // The arrow binds to IsExpanded, which is computed from Count: without this the group would collapse but
        // the chevron would keep pointing the wrong way.
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsExpanded)));
    }

    private void AddAll()
    {
        foreach (var track in _allTracks) Add(track);
    }
}
