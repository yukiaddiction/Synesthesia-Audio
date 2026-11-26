using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Services;

public interface IAudioPlatformService
{
    void OnPlayStarted(MediaElement mediaElement);
    void OnAlbumChanged(string albumFolderPath);
    void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath);
    void OnMediaOpened(string? trackTitle, string albumFolderPath);
    void OnPlaybackStopped();

    /// <summary>
    /// Asks the OS for audio focus before playback starts. False means playback must not start — another
    /// app (a call, another player) currently owns the audio output.
    /// </summary>
    bool RequestAudioFocus();

    /// <summary>Gives audio focus back when playback stops.</summary>
    void AbandonAudioFocus();

    /// <summary>
    /// Makes <paramref name="coverPath"/> available to whatever the platform shows outside the app (the lock
    /// screen, the media notification) and returns the value to publish as the artwork URL. Android has to
    /// hand a content URI and a read grant to the system UI; the other platforms just use the file URI.
    /// </summary>
    string PublishArtwork(string coverPath);

    /// <summary>
    /// Releases everything the platform still holds for this window: media sessions and any URI grants. Android
    /// outlives the activity, so leaving these behind leaves the next window with dead transport buttons.
    /// </summary>
    void OnMediaDetached();
}
