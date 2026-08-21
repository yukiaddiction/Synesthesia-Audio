using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Services;

public class DefaultAudioPlatformService : IAudioPlatformService
{
    public void OnPlayStarted(MediaElement mediaElement) { }
    public void OnAlbumChanged(string albumFolderPath) { }
    public void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath) { }
    public void OnMediaOpened(string? trackTitle, string albumFolderPath) { }
    public void OnPlaybackStopped() { }
}
