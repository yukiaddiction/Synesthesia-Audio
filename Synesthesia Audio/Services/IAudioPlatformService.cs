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
}
