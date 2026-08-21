using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Services;

public interface IAudioPlatformService
{
    void OnPlayStarted(MediaElement mediaElement);
    void OnAlbumChanged(string albumFolderPath);
    void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath);
    void OnMediaOpened(string? trackTitle, string albumFolderPath);
    void OnPlaybackStopped();
}
