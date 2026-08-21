using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace Synesthesia_Audio;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private static string? _pendingAlbumFolderPath;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleIntent(Intent);
    }

    protected override void OnNewIntent(Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        HandleIntent(intent);
    }

    private static void HandleIntent(Intent? intent)
    {
        var albumPath = intent?.GetStringExtra("AlbumFolderPath");
        if (!string.IsNullOrEmpty(albumPath))
        {
            _pendingAlbumFolderPath = albumPath;
        }
        TryNavigateToAlbum();
    }

    internal static void TryNavigateToAlbum()
    {
        try
        {
            if (string.IsNullOrEmpty(_pendingAlbumFolderPath)) return;
            if (Shell.Current == null) return;

            var services = IPlatformApplication.Current?.Services;
            if (services == null) return;

            var cacheService = services.GetService<Services.LibraryCacheService>();
            if (cacheService == null) return;

            var work = cacheService.GetWork(_pendingAlbumFolderPath);
            _pendingAlbumFolderPath = null;

            if (work != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    try
                    {
                        var shell = Shell.Current;
                        if (shell == null) return;
                        await shell.GoToAsync("//Library");
                        await shell.GoToAsync(nameof(Views.AlbumPage), new Dictionary<string, object> { { "Album", work } });
                    }
                    catch (Exception ex)
                    {
                        Services.Logging.AppLog.Get<MainActivity>().LogWarning(ex, "Album navigation from intent failed");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<MainActivity>().LogWarning(ex, "TryNavigateToAlbum failed");
        }
    }
}
