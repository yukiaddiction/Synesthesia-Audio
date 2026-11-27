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


// Framework-created classes cannot take an injected logger, so the file shares one resolved instance.
internal static class FileLog87377
{
    internal static readonly ILogger<ScanForegroundService> Logger = Services.Logging.AppLog.Get<ScanForegroundService>();
}
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
            FileLog87377.Logger.LogError(ex, "ScanForegroundService: OnCreate channel creation failed");
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, [GeneratedEnum] StartCommandFlags flags, int startId)
    {
        try
        {
            // Ignore a second start: a parallel scan would write the same cache and clobber the first one's failures.
            if (_isRunning)
            {
                FileLog87377.Logger
                    .LogInformation("ScanForegroundService: scan already running, ignoring duplicate start");
                return StartCommandResult.NotSticky;
            }

            CreateNotificationChannel();
            _isRunning = true;
            StartForeground(NOTIFICATION_ID, BuildNotification("Scanning library...", "Preparing...", 0));
            _cts = new CancellationTokenSource();
            RunScanAsync(_cts.Token).FireAndForget(ex =>  FileLog87377.Logger.LogError(ex, "ScanForegroundService: background scan faulted"));
        }
        catch (System.Exception ex)
        {
            FileLog87377.Logger.LogError(ex, "ScanForegroundService: OnStartCommand failed");
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
                    FileLog87377.Logger.LogWarning(ex, "ScanForegroundService: notification update failed");
                }
            });

            await Task.Run(() => scanService.ScanLibraryAsync(basePath, progress, token));
        }
        catch (Exception ex)
        {
            FileLog87377.Logger.LogError(ex, "Background scan failed");
            try
            {
                stateService?.MarkFailed($"Background scan failed: {ex.Message}");
            }
            catch (System.Exception stateEx)
            {
                FileLog87377.Logger.LogError(stateEx, "ScanForegroundService: failed to record scan failure");
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
                FileLog87377.Logger.LogWarning(stopEx, "ScanForegroundService: stop failed");
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
            FileLog87377.Logger.LogWarning(ex, "ScanForegroundService: cleanup failed");
        }
        _isRunning = false;
        base.OnDestroy();
    }

    /// <summary>
    /// Android 15+ stops a long-running dataSync foreground service. Cancelling first ends the scan as Interrupted
    /// rather than leaving the UI showing a scan that is no longer running.
    /// </summary>
    public override void OnTimeout(int startId)
    {
        try
        {
            FileLog87377.Logger
                .LogWarning("ScanForegroundService: foreground timeout, cancelling the scan");
            _cts?.Cancel();
        }
        catch (System.Exception ex)
        {
            FileLog87377.Logger.LogWarning(ex, "ScanForegroundService: timeout handling failed");
        }

        StopSelf(startId);
    }

    private Notification BuildNotification(string title, string text, int progressPct)
    {
        var context = global::Android.App.Application.Context;
        var pendingIntent = AndroidNotificationHelper.BuildContentIntent(context, null, requestCode: 1);

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

    private void CreateNotificationChannel() =>
        AndroidNotificationHelper.CreateChannel(
            this, CHANNEL_ID, "Library Scan", "Keeps the library folder scan running while the app is closed");

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
            FileLog87377.Logger.LogError(ex, "ScanForegroundService: Start failed");
        }
    }
}
#endif
