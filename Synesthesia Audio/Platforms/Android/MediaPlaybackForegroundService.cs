#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Platforms.Android;

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
            Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogError(ex, "ForegroundService: OnCreate channel creation failed");
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

            var context = global::Android.App.Application.Context;
            var appIntent = new Intent(context, typeof(MainActivity));
            appIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            if (!string.IsNullOrEmpty(_lastAlbumFolderPath))
            {
                appIntent.PutExtra("AlbumFolderPath", _lastAlbumFolderPath);
            }
            var pendingIntent = PendingIntent.GetActivity(
                context,
                0,
                appIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

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
                Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogWarning(ex, "Notification artwork load failed");
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
                    Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogWarning(ex, "ForegroundService: media session token unavailable, clearing stale session");
                    MediaSessionHelper.CurrentSession = null;
                }
            }

            StartForeground(NOTIFICATION_ID, builder.Build());
        }
        catch (System.Exception ex)
        {
            Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogError(ex, "ForegroundService: OnStartCommand failed");
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
                Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogError(stopEx, "ForegroundService: StopSelf failed");
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
            Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogWarning(ex, "ForegroundService: StopForeground failed in OnDestroy");
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

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(CHANNEL_ID, "Media Playback", NotificationImportance.Low)
            {
                Description = "Ongoing playback service to keep audio playing in background"
            };
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
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
            Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogError(ex, "ForegroundService: Start failed");
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
            Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>().LogError(ex, "ForegroundService: Stop failed");
        }
    }

    public static void UpdateNotification()
    {
        // One decode per track change, not three: this used to decode the same full-resolution cover
        // immediately and again at 500 ms and 1200 ms.
        SetArtworkOnMediaSession();
    }

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
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;

        lock (_artworkLock)
        {
            if (_cachedArtwork != null && !_cachedArtwork.IsRecycled &&
                string.Equals(_cachedArtworkPath, path, System.StringComparison.Ordinal))
            {
                return _cachedArtwork;
            }

            try
            {
                // First pass reads the dimensions only, so the second pass can subsample instead of
                // allocating a full-size bitmap just to throw it away.
                var bounds = new global::Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
                global::Android.Graphics.BitmapFactory.DecodeFile(path, bounds);

                var options = new global::Android.Graphics.BitmapFactory.Options
                {
                    InSampleSize = CalculateArtworkSampleSize(bounds.OutWidth, bounds.OutHeight, maxPixels)
                };

                var bitmap = global::Android.Graphics.BitmapFactory.DecodeFile(path, options);
                if (bitmap == null)
                {
                    Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>()
                        .LogWarning("Artwork decode returned no bitmap for {Path}", path);
                    return null;
                }

                _cachedArtwork = bitmap;
                _cachedArtworkPath = path;
                return bitmap;
            }
            catch (System.Exception ex)
            {
                Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>()
                    .LogWarning(ex, "Artwork decode failed for {Path}", path);
                return null;
            }
        }
    }

    private static int CalculateArtworkSampleSize(int width, int height, int maxPixels)
    {
        if (width <= 0 || height <= 0 || maxPixels <= 0) return 1;

        var sample = 1;
        while (width / sample > maxPixels || height / sample > maxPixels)
        {
            sample *= 2;
        }
        return sample;
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
            Synesthesia_Audio.Services.Logging.AppLog.Get<MediaPlaybackForegroundService>()
                .LogWarning(ex, "Media session artwork update failed");
        }
    }
}
#endif
