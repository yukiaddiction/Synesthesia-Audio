#if ANDROID
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Services;

namespace Synesthesia_Audio.Platforms.Android;

[Service(ForegroundServiceType = ForegroundService.TypeDataSync, Exported = false)]
public class ScanForegroundService : Service
{
    private const string CHANNEL_ID = "synesthesia_scan_channel";
    private const int NOTIFICATION_ID = 1002;

    private static volatile bool _isRunning;
    public static bool IsRunning => _isRunning;

    private CancellationTokenSource? _cts;

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
            Services.Logging.AppLog.Get<ScanForegroundService>().LogError(ex, "ScanForegroundService: OnCreate channel creation failed");
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        try
        {
            // A second start while a scan is running used to begin a parallel scan: two walks writing the
            // same cache, and the first one's failure list clobbered by the second (BUG-m-02).
            if (_isRunning)
            {
                Services.Logging.AppLog.Get<ScanForegroundService>()
                    .LogInformation("ScanForegroundService: scan already running, ignoring duplicate start");
                return StartCommandResult.NotSticky;
            }

            CreateNotificationChannel();
            _isRunning = true;
            StartForeground(NOTIFICATION_ID, BuildNotification("Scanning library...", "Preparing...", 0));
            _cts = new CancellationTokenSource();
            RunScanAsync(_cts.Token).FireAndForget(ex =>  Services.Logging.AppLog.Get<ScanForegroundService>().LogError(ex, "ScanForegroundService: background scan faulted"));
        }
        catch (System.Exception ex)
        {
            Services.Logging.AppLog.Get<ScanForegroundService>().LogError(ex, "ScanForegroundService: OnStartCommand failed");
            _isRunning = false;
            StopSelf();
        }

        return StartCommandResult.NotSticky;
    }

    private async Task RunScanAsync(CancellationToken token)
    {
        var stateService = IPlatformApplication.Current?.Services?.GetService<ScanStateService>();
        try
        {
            var services = IPlatformApplication.Current?.Services;
            var scanService = services?.GetService<ScanFolderService>();
            var settings = services?.GetService<SettingsService>();

            if (scanService == null || settings == null || stateService == null)
            {
                stateService?.MarkFailed("Background scan could not start: app services were not available.");
                return;
            }

            var basePath = settings.CurrentSettings.BaseAudioFolderPath;
            bool isCloud = settings.CurrentSettings.StorageType == "WebDAV";
            if (!isCloud && string.IsNullOrEmpty(basePath))
            {
                stateService.MarkFailed("No library folder is configured. Pick a folder in Settings first.");
                return;
            }

            var progress = new Progress<(double, string)>(p =>
            {
                stateService.UpdateProgress(p.Item1, p.Item2);
                try
                {
                    var manager = NotificationManagerCompat.From(this);
                    manager.Notify(NOTIFICATION_ID, BuildNotification("Scanning library...", p.Item2, (int)(p.Item1 * 100)));
                }
                catch (System.Exception ex)
                {
                    Services.Logging.AppLog.Get<ScanForegroundService>().LogWarning(ex, "ScanForegroundService: notification update failed");
                }
            });

            await Task.Run(() => scanService.ScanLibraryAsync(basePath, progress, token));
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<ScanForegroundService>().LogError(ex, "Background scan failed");
            try
            {
                stateService?.MarkFailed($"Background scan failed: {ex.Message}");
            }
            catch (System.Exception stateEx)
            {
                Services.Logging.AppLog.Get<ScanForegroundService>().LogError(stateEx, "ScanForegroundService: failed to record scan failure");
            }
        }
        finally
        {
            _isRunning = false;
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
                StopSelf();
            }
            catch (System.Exception stopEx)
            {
                Services.Logging.AppLog.Get<ScanForegroundService>().LogWarning(stopEx, "ScanForegroundService: stop failed");
            }
        }
    }

    public override void OnDestroy()
    {
        try
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
        catch (System.Exception ex)
        {
            Services.Logging.AppLog.Get<ScanForegroundService>().LogWarning(ex, "ScanForegroundService: cleanup failed");
        }
        _isRunning = false;
        base.OnDestroy();
    }

    /// <summary>
    /// Android 15+ stops a dataSync foreground service that runs too long. Cancelling first means the scan
    /// ends as Interrupted rather than leaving the UI showing a scan that is no longer running.
    /// </summary>
    public override void OnTimeout(int startId)
    {
        try
        {
            Services.Logging.AppLog.Get<ScanForegroundService>()
                .LogWarning("ScanForegroundService: foreground timeout, cancelling the scan");
            _cts?.Cancel();
        }
        catch (System.Exception ex)
        {
            Services.Logging.AppLog.Get<ScanForegroundService>().LogWarning(ex, "ScanForegroundService: timeout handling failed");
        }

        StopSelf(startId);
    }

    private Notification BuildNotification(string title, string text, int progressPct)
    {
        var context = global::Android.App.Application.Context;
        var appIntent = new Intent(context, typeof(MainActivity));
        appIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var pendingIntent = PendingIntent.GetActivity(
            context,
            1,
            appIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
            .SetContentTitle(title)
            .SetContentText(text)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentIntent(pendingIntent)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetOnlyAlertOnce(true);

        if (progressPct >= 0)
        {
            builder.SetProgress(100, Math.Clamp(progressPct, 0, 100), false);
        }

        return builder.Build();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(CHANNEL_ID, "Library Scan", NotificationImportance.Low)
            {
                Description = "Keeps the library folder scan running while the app is closed"
            };
            var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    public static void StartScanService()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new Intent(context, typeof(ScanForegroundService));
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
            Services.Logging.AppLog.Get<ScanForegroundService>().LogError(ex, "ScanForegroundService: Start failed");
        }
    }
}
#endif
