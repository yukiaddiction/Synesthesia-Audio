#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Platforms.Android;


/// <summary>One resolved logger for this file (framework-created classes cannot take a dependency) — PAT-01.</summary>
internal static class FileLog31288
{
    internal static readonly ILogger<MediaPlaybackForegroundService> Logger = Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>();
}
[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeMediaPlayback)]
public class MediaPlaybackForegroundService : Service
{
    private const string CHANNEL_ID = "synesthesia_media_channel";
    private const int NOTIFICATION_ID = 1001;

    private static string _lastTitle = "Synesthesia Audio";
    private static string _lastArtist = "Audio Player";
    private static string _lastAlbumFolderPath = string.Empty;

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        try
        {
            CreateNotificationChannel();
        }
        catch (System.Exception ex)
        {
            FileLog31288.Logger.LogError(ex, "ForegroundService: OnCreate channel creation failed");
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        try
        {
            CreateNotificationChannel();

            if (intent != null)
            {
                if (intent.GetBooleanExtra("UpdateSessionOnly", false))
                {
                    // Just update the session using cached title/artist
                }
                else
                {
                    _lastTitle = intent.GetStringExtra("TrackTitle") ?? "Synesthesia Audio";
                    _lastArtist = intent.GetStringExtra("TrackArtist") ?? "Audio Player";
                    _lastAlbumFolderPath = intent.GetStringExtra("AlbumFolderPath") ?? string.Empty;
                }
            }

            Current = this;

            var context = global::Android.App.Application.Context;
            var pendingIntent = AndroidNotificationHelper.BuildContentIntent(context, _lastAlbumFolderPath);

            var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle(_lastTitle)
                .SetContentText(_lastArtist)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentIntent(pendingIntent)
                .SetOngoing(true)
                .SetPriority(NotificationCompat.PriorityDefault);

            try
            {
                var audioService = IPlatformApplication.Current?.Services?.GetService<Synesthesia_Audio.Services.AudioService>();
                var coverPath = audioService?.CurrentTrack?.CoverImagePath;
                var bitmap = LoadArtworkBitmap(coverPath);
                if (bitmap != null)
                {
                    builder.SetLargeIcon(bitmap);
                }
            }
            catch (System.Exception ex)
            {
                FileLog31288.Logger.LogWarning(ex, "Notification artwork load failed");
            }

            var session = MediaSessionHelper.CurrentSession;
            if (session != null)
            {
                try
                {
                    var mediaStyle = new global::AndroidX.Media.App.NotificationCompat.MediaStyle()
                        .SetMediaSession(session.SessionToken);
                    builder.SetStyle(mediaStyle);
                }
                catch (System.Exception ex)
                {
                    FileLog31288.Logger.LogWarning(ex, "ForegroundService: media session token unavailable, clearing stale session");
                    MediaSessionHelper.CurrentSession = null;
                }
            }

            StartForeground(NOTIFICATION_ID, builder.Build());
        }
        catch (System.Exception ex)
        {
            FileLog31288.Logger.LogError(ex, "ForegroundService: OnStartCommand failed");
            try
            {
                // This service is started with startForegroundService, so Android requires a startForeground()
                // call even on the failure path. Stopping without it raises RemoteServiceException — an app
                // crash instead of a quiet stop (BUG-m-23).
                StartForeground(NOTIFICATION_ID, BuildFallbackNotification());
                StopSelf();
            }
            catch (System.Exception stopEx)
            {
                FileLog31288.Logger.LogError(stopEx, "ForegroundService: StopSelf failed");
            }
        }

        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
            {
                StopForeground(StopForegroundFlags.Remove);
            }
            else
            {
#pragma warning disable CS0618
                StopForeground(true);
#pragma warning restore CS0618
            }
        }
        catch (System.Exception ex)
        {
            FileLog31288.Logger.LogWarning(ex, "ForegroundService: StopForeground failed in OnDestroy");
        }
        base.OnDestroy();
    }

    /// <summary>
    /// The bare minimum notification Android requires when a startForegroundService() service has to stop
    /// from its own failure path.
    /// </summary>
    private Notification BuildFallbackNotification() =>
        new NotificationCompat.Builder(this, CHANNEL_ID)
            .SetContentTitle("Synesthesia Audio")
            .SetContentText("Playback service stopped")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetOngoing(false)
            .Build();

    private void CreateNotificationChannel() =>
        AndroidNotificationHelper.CreateChannel(
            this, CHANNEL_ID, "Media Playback", "Ongoing playback service to keep audio playing in background");

    /// <summary>
    /// Drops the service out of the foreground while keeping its notification, which makes the notification
    /// dismissible.
    ///
    /// Android marks a foreground notification as ongoing, so a paused player kept an unremovable entry in the
    /// shade for as long as the window lived (UX-06). Detaching leaves it visible but swipeable; resuming
    /// promotes it again.
    /// </summary>
    /// <summary>Puts a demoted notification back in the foreground, with its content intact.</summary>
    private void PromoteFromBackground()
    {
        var context = global::Android.App.Application.Context;
        var pendingIntent = AndroidNotificationHelper.BuildContentIntent(context, _lastAlbumFolderPath);

        var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
            .SetContentTitle(_lastTitle)
            .SetContentText(_lastArtist)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityDefault);

        var bitmap = LoadArtworkBitmap(_cachedArtworkPath);
        if (bitmap != null) builder.SetLargeIcon(bitmap);

        var session = MediaSessionHelper.CurrentSession;
        if (session != null)
        {
            try
            {
                builder.SetStyle(new global::AndroidX.Media.App.NotificationCompat.MediaStyle()
                    .SetMediaSession(session.SessionToken));
            }
            catch (System.Exception ex)
            {
                FileLog31288.Logger
                    .LogDebug(ex, "ForegroundService: media session unavailable while promoting");
            }
        }

        StartForeground(NOTIFICATION_ID, builder.Build());
    }

    public static void DemoteToBackground()
    {
        try
        {
            var service = Current;
            if (service == null) return;

            if (Build.VERSION.SdkInt >= BuildVersionCodes.N)
                service.StopForeground(StopForegroundFlags.Detach);
            else
#pragma warning disable CA1422 // Deprecated only on API 33+, where the flags overload above is used.
                service.StopForeground(false);
#pragma warning restore CA1422
        }
        catch (System.Exception ex)
        {
            FileLog31288.Logger
                .LogDebug(ex, "ForegroundService: demote to background failed");
        }
    }

    /// <summary>Puts a demoted service back in the foreground when playback actually resumes.</summary>
    public static void PromoteToForeground()
    {
        try
        {
            Current?.PromoteFromBackground();
        }
        catch (System.Exception ex)
        {
            FileLog31288.Logger
                .LogDebug(ex, "ForegroundService: promote to foreground failed");
        }
    }
    public static void StartPlaybackService(string title, string artist, string albumFolderPath = "")
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(MediaPlaybackForegroundService));
            intent.PutExtra("TrackTitle", title);
            intent.PutExtra("TrackArtist", artist);
            intent.PutExtra("AlbumFolderPath", albumFolderPath);
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
        catch (System.Exception ex)
        {
            FileLog31288.Logger.LogError(ex, "ForegroundService: Start failed");
        }
    }

    public static void StopPlaybackService()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(MediaPlaybackForegroundService));
            context.StopService(intent);
        }
        catch (System.Exception ex)
        {
            FileLog31288.Logger.LogError(ex, "ForegroundService: Stop failed");
        }
    }

    public static void UpdateNotification()
    {
        // One decode per track change, not three: this used to decode the same full-resolution cover
        // immediately and again at 500 ms and 1200 ms.
        SetArtworkOnMediaSession();
    }

    /// <summary>The running service, so the platform layer can demote or promote it (UX-06).</summary>
    internal static MediaPlaybackForegroundService? Current { get; private set; }

    private static readonly object _artworkLock = new();
    private static string _cachedArtworkPath = string.Empty;
    private static global::Android.Graphics.Bitmap? _cachedArtwork;

    /// <summary>
    /// Decodes a cover at a bounded size for the notification, cacheing it for the current track.
    /// Decoding at full resolution on the UI thread caused jank, and a bitmap over the binder limit threw
    /// <c>TransactionTooLargeException</c> — which the caller swallowed, so no artwork appeared at all.
    /// </summary>
    internal static global::Android.Graphics.Bitmap? LoadArtworkBitmap(string? path, int maxPixels = 512)
    {
        if (string.IsNullOrEmpty(path)) return null;

        lock (_artworkLock)
        {
            if (_cachedArtwork != null && !_cachedArtwork.IsRecycled &&
                string.Equals(_cachedArtworkPath, path, System.StringComparison.Ordinal))
            {
                return _cachedArtwork;
            }
        }

        // Bounded decode lives in AndroidNotificationHelper so the media session cannot decode a full-size
        // cover and blow the binder limit (DUP-05).
        var bitmap = AndroidNotificationHelper.DecodeArtwork(path, maxPixels);
        if (bitmap == null) return null;

        lock (_artworkLock)
        {
            _cachedArtwork = bitmap;
            _cachedArtworkPath = path;
        }

        return bitmap;
    }


    private static void SetArtworkOnMediaSession() => _ = SetArtworkOnMediaSessionAsync();

    private static async Task SetArtworkOnMediaSessionAsync()
    {
        try
        {
            var audioService = IPlatformApplication.Current?.Services?.GetService<Synesthesia_Audio.Services.AudioService>();
            var coverPath = audioService?.CurrentTrack?.CoverImagePath;
            if (string.IsNullOrEmpty(coverPath)) return;

            // Decoding happens off the UI thread; InSampleSize keeps the bitmap small enough for the binder.
            var bitmap = await Task.Run(() => LoadArtworkBitmap(coverPath)).ConfigureAwait(false);
            if (bitmap == null) return;

            var mediaSession = MediaSessionHelper.CurrentSession;
            if (mediaSession?.Controller?.Metadata == null) return;

            var metadata = new global::Android.Support.V4.Media.MediaMetadataCompat.Builder(mediaSession.Controller.Metadata)
                .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyArt, bitmap)
                .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyAlbumArt, bitmap)
                .Build();
            mediaSession.SetMetadata(metadata);
        }
        catch (System.Exception ex)
        {
            // Previously an empty catch: a failure left the notification with no artwork and no trace.
            FileLog31288.Logger
                .LogWarning(ex, "Media session artwork update failed");
        }
    }
}
#endif
