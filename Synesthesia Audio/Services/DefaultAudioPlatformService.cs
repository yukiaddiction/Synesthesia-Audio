using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Services;

public class DefaultAudioPlatformService : IAudioPlatformService
{
    public void OnPlayStarted(MediaElement mediaElement) { }
    public void OnAlbumChanged(string albumFolderPath) { }
    public void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath) { }
    public void OnMediaOpened(string? trackTitle, string albumFolderPath) { }
    public void OnPlaybackStopped() { }

    // Desktop and Apple platforms hand focus out implicitly with the audio session, so nothing to ask.
    public bool RequestAudioFocus() => true;
    public void AbandonAudioFocus() { }

    // A plain file URI is enough where the OS reads artwork directly from the file system.
    public string PublishArtwork(string coverPath) => new Uri(System.IO.Path.GetFullPath(coverPath)).AbsoluteUri;

    public void OnMediaDetached() { }
}
