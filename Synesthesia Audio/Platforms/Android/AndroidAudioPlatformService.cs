using CommunityToolkit.Maui.Views;

using Microsoft.Extensions.Logging;
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
            // Resuming re-promotes a notification the pause had made dismissible (UX-06).
            Platforms.Android.MediaPlaybackForegroundService.PromoteToForeground();
        }
        else
        {
            Platforms.Android.WakeLockHelper.Release();

            // Keep the notification but let it be swiped away: a paused player has no business holding an
            // unremovable entry in the shade (UX-06).
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

    /// <summary>The cover URI currently shared with the system UI, so its grant can be revoked (SEC-m-06).</summary>
    private string? _grantedArtworkUri;

    public string PublishArtwork(string coverPath)
    {
        var context = global::Android.App.Application.Context;
        var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
            context,
            context.PackageName + ".fileprovider",
            new Java.IO.File(coverPath));

        // The previous track's grant is no longer needed; without this the process accumulated one per cover.
        RevokeGrantedArtwork(context);

        context.GrantUriPermission(
            "com.android.systemui", uri, global::Android.Content.ActivityFlags.GrantReadUriPermission);

        _grantedArtworkUri = uri.ToString();
        return _grantedArtworkUri;
    }

    public void OnMediaDetached()
    {
        // The process outlives the activity, so the session statics have to be cleared here; otherwise the next
        // window's session is never wrapped and its next/previous buttons are dead.
        try
        {
            Platforms.Android.MediaSessionHelper.ResetSession();
        }
        catch (System.Exception ex)
        {
            Synesthesia_Audio.Services.Logging.AppLog.Get<AndroidAudioPlatformService>()
                .LogWarning(ex, "Resetting the Android media session during detach failed");
        }

        // Nothing should keep read access to a cover once this window is gone.
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
            Synesthesia_Audio.Services.Logging.AppLog.Get<AndroidAudioPlatformService>()
                .LogDebug(ex, "Revoking a previous artwork grant failed");
        }
    }
    public bool RequestAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.RequestFocus();

    public void AbandonAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.AbandonFocus();
}
