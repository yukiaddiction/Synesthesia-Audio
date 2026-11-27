using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Services;

public interface IAudioPlatformService
{
    void OnPlayStarted(MediaElement mediaElement);
    void OnAlbumChanged(string albumFolderPath);
    void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath);
    void OnMediaOpened(string? trackTitle, string albumFolderPath);
    void OnPlaybackStopped();

    /// <summary>False means playback must not start: another app (a call, another player) owns the audio output.</summary>
    bool RequestAudioFocus();

    void AbandonAudioFocus();

    /// <summary>
    /// Returns the artwork URL for the lock screen / media notification. Android needs a content URI plus a read
    /// grant for the system UI; other platforms use the file URI.
    /// </summary>
    string PublishArtwork(string coverPath);

    /// <summary>
    /// Releases media sessions and URI grants. Android outlives the activity, so leftovers would leave the next
    /// window with dead transport buttons.
    /// </summary>
    void OnMediaDetached();
}
