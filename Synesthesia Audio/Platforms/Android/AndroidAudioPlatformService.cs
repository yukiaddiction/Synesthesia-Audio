using CommunityToolkit.Maui.Views;

using Microsoft.Extensions.Logging;
namespace Synesthesia_Audio.Services;


/// <summary>
/// One resolved logger for this file. These classes are created by the framework, so they cannot take a
/// constructor dependency; the audit asks for a single resolved field instead of a lookup per site (PAT-01).
/// It lives at namespace scope because the file holds more than one class.
/// </summary>
internal static class FileLog12933
{
    internal static readonly ILogger<AndroidAudioPlatformService> Logger = Synesthesia_Audio.Services.Logging.AppLog.Get<AndroidAudioPlatformService>();
}
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
        // Our own media session carries the transport state now: without this the notification would show
        // play/pause incorrectly and the headset buttons would act on a session that thinks it is stopped
        // (OVR-03).
        Platforms.Android.MediaSessionHelper.UpdatePlaybackState(isPlaying, 0);

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
            FileLog12933.Logger
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
            FileLog12933.Logger
                .LogDebug(ex, "Revoking a previous artwork grant failed");
        }
    }
    public bool RequestAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.RequestFocus();

    public void AbandonAudioFocus() => Platforms.Android.AudioFocusHelper.Instance.AbandonFocus();
}
