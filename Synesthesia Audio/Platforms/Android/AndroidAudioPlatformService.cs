using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Services;

public class AndroidAudioPlatformService : IAudioPlatformService
{
    public void OnPlayStarted(MediaElement mediaElement)
    {
        Platforms.Android.MediaSessionHelper.TryWrapSessionPlayer(mediaElement);
        // Focus is requested by AudioService *before* playback starts, not 1.5 s afterwards: by then the
        // first fraction of a second had already played over whatever held focus.
    }

    public void OnAlbumChanged(string albumFolderPath)
    {
        Platforms.Android.MediaSessionHelper.UpdateSessionActivityIntent(albumFolderPath);
    }

    public void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath)
    {
        if (isPlaying)
        {
            Platforms.Android.WakeLockHelper.Acquire();
            Platforms.Android.MediaPlaybackForegroundService.UpdateNotification();
        }
        else
        {
            Platforms.Android.WakeLockHelper.Release();
        }
    }

    public void OnMediaOpened(string? trackTitle, string albumFolderPath)
    {
        if (trackTitle != null)
            Platforms.Android.MediaPlaybackForegroundService.StartPlaybackService(trackTitle, "Synesthesia Audio", albumFolderPath);
    }

    public void OnPlaybackStopped()
    {
        Platforms.Android.AudioFocusHelper.Instance.AbandonFocus();
        Platforms.Android.MediaPlaybackForegroundService.StopPlaybackService();
    }

    public bool RequestAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.RequestFocus();

    public void AbandonAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.AbandonFocus();
}
