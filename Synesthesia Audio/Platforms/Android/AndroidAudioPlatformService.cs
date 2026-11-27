using CommunityToolkit.Maui.Views;

using Microsoft.Extensions.Logging;
namespace Synesthesia_Audio.Services;


// Framework-created classes cannot take an injected logger, so the file shares one resolved instance.
internal static class FileLog12933
{
    internal static readonly ILogger<AndroidAudioPlatformService> Logger = Synesthesia_Audio.Services.Logging.AppLog.Get<AndroidAudioPlatformService>();
}
public class AndroidAudioPlatformService : IAudioPlatformService
{
    public void OnPlayStarted(MediaElement mediaElement)
    {
        // Audio focus is not requested here: AudioService takes it before playback starts, so nothing plays over the
        // previous focus holder.
        Platforms.Android.MediaSessionHelper.TryWrapSessionPlayer(mediaElement);
    }

    public void OnAlbumChanged(string albumFolderPath)
    {
        Platforms.Android.MediaSessionHelper.UpdateSessionActivityIntent(albumFolderPath);
    }

    public void OnPlaybackStateChanged(bool isPlaying, string? trackTitle, string albumFolderPath)
    {
        // Our session carries the transport state; otherwise the notification shows the wrong play/pause and headset
        // buttons act on a session that thinks it is stopped.
        Platforms.Android.MediaSessionHelper.UpdatePlaybackState(isPlaying, 0);

        if (isPlaying)
        {
            Platforms.Android.WakeLockHelper.Acquire();
            Platforms.Android.MediaPlaybackForegroundService.UpdateNotification();
            // Resuming re-promotes a notification the pause made dismissible.
            Platforms.Android.MediaPlaybackForegroundService.PromoteToForeground();
        }
        else
        {
            Platforms.Android.WakeLockHelper.Release();

            // A paused player should not hold an unremovable notification.
            Platforms.Android.MediaPlaybackForegroundService.DemoteToBackground();
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

    // Remembered so the system UI's read grant can be revoked when the cover changes.
    private string? _grantedArtworkUri;

    public string PublishArtwork(string coverPath)
    {
        var context = global::Android.App.Application.Context;
        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            context,
            context.PackageName + ".fileprovider",
            new Java.IO.File(coverPath));

        // Otherwise the process accumulates one grant per cover.
        RevokeGrantedArtwork(context);

        context.GrantUriPermission(
            "com.android.systemui", uri, global::Android.Content.ActivityFlags.GrantReadUriPermission);

        _grantedArtworkUri = uri.ToString();
        return _grantedArtworkUri;
    }

    public void OnMediaDetached()
    {
        try
        {
            // The process outlives the activity, so the session statics must be cleared or the next window's session is
            // never wrapped and its next/previous buttons are dead.
            Platforms.Android.MediaSessionHelper.ResetSession();
        }
        catch (System.Exception ex)
        {
            FileLog12933.Logger
                .LogWarning(ex, "Resetting the Android media session during detach failed");
        }

        RevokeGrantedArtwork(global::Android.App.Application.Context);
    }

    private void RevokeGrantedArtwork(global::Android.Content.Context context)
    {
        var previous = _grantedArtworkUri;
        _grantedArtworkUri = null;
        if (previous == null) return;

        try
        {
            context.RevokeUriPermission(
                global::Android.Net.Uri.Parse(previous),
                global::Android.Content.ActivityFlags.GrantReadUriPermission);
        }
        catch (System.Exception ex)
        {
            FileLog12933.Logger
                .LogDebug(ex, "Revoking a previous artwork grant failed");
        }
    }
    public bool RequestAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.RequestFocus();

    public void AbandonAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.AbandonFocus();
}
