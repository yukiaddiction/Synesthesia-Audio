namespace Synesthesia_Audio.Models;

public class PlaybackSessionState
{
    public string AlbumFolderPath { get; set; } = string.Empty;
    public List<PlaybackSessionTrack> Playlist { get; set; } = new();
    public int CurrentTrackIndex { get; set; } = -1;
    public double PositionSeconds { get; set; }
    public LoopMode LoopMode { get; set; }
}

public class PlaybackSessionTrack
{
    public string Title { get; set; } = string.Empty;
    public string TranslatedTitle { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string CoverImagePath { get; set; } = string.Empty;
    public string VoiceActors { get; set; } = string.Empty;
}
