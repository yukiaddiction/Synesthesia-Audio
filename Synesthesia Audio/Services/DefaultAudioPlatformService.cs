using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Services;

public class DefaultAudioPlatformService : IAudioPlatformService
{
    public void OnPlayStarted(MediaElement mediaElement) { }
    public void OnAlbumChanged(string albumFolderPath) { }
    public void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath) { }
    public void OnMediaOpened(string? trackTitle, string albumFolderPath) { }
    public void OnPlaybackStopped() { }

    // Desktop and Apple platforms grant focus implicitly with the audio session.
    public bool RequestAudioFocus() => true;
    public void AbandonAudioFocus() { }

    // These OSes read artwork straight from the file system, so a file URI is enough.
    public string PublishArtwork(string coverPath) => new Uri(System.IO.Path.GetFullPath(coverPath)).AbsoluteUri;

    public void OnMediaDetached() { }
}
