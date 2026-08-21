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
                if (!string.IsNullOrEmpty(coverPath) && System.IO.File.Exists(coverPath))
                {
                    var bitmap = global::Android.Graphics.BitmapFactory.DecodeFile(coverPath);
                    if (bitmap != null)
                    {
                        builder.SetLargeIcon(bitmap);
                        var mediaSession = MediaSessionHelper.CurrentSession;
                        if (mediaSession?.Controller?.Metadata != null)
                        {
                            var metadata = new global::Android.Support.V4.Media.MediaMetadataCompat.Builder(mediaSession.Controller.Metadata)
                                .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyArt, bitmap)
                                .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyAlbumArt, bitmap)
                                .Build();
                            mediaSession.SetMetadata(metadata);
                        }
                    }
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
        SetArtworkOnMediaSession();
        _ = RetryArtworkAsync(500);
        _ = RetryArtworkAsync(1200);
    }

    private static async Task RetryArtworkAsync(int delayMs)
    {
        await Task.Delay(delayMs);
        SetArtworkOnMediaSession();
    }

    private static void SetArtworkOnMediaSession()
    {
        try
        {
            var audioService = IPlatformApplication.Current?.Services?.GetService<Synesthesia_Audio.Services.AudioService>();
            var coverPath = audioService?.CurrentTrack?.CoverImagePath;
            if (!string.IsNullOrEmpty(coverPath) && System.IO.File.Exists(coverPath))
            {
                var bitmap = global::Android.Graphics.BitmapFactory.DecodeFile(coverPath);
                if (bitmap != null)
                {
                    var mediaSession = MediaSessionHelper.CurrentSession;
                    if (mediaSession?.Controller?.Metadata != null)
                    {
                        var metadata = new global::Android.Support.V4.Media.MediaMetadataCompat.Builder(mediaSession.Controller.Metadata)
                            .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyArt, bitmap)
                            .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyAlbumArt, bitmap)
                            .Build();
                        mediaSession.SetMetadata(metadata);
                    }
                }
            }
        }
        catch { }
    }
}
#endif
