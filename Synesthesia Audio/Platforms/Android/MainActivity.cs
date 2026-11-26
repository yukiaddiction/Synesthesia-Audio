using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace Synesthesia_Audio;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.FontScale | ConfigChanges.Locale | ConfigChanges.LayoutDirection)]
public class MainActivity : MauiAppCompatActivity
{
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

    /// <summary>
    /// Records the album the intent asked for. The request is held by StartupNavigation, a singleton, because
    /// static state on an Activity outlives the Activity and a stale path could survive a re-created one (PAT-07).
    /// </summary>
    private void HandleIntent(Intent? intent)
    {
        var albumPath = intent?.GetStringExtra("AlbumFolderPath");
        if (string.IsNullOrEmpty(albumPath)) return;

        var services = IPlatformApplication.Current?.Services;
        services?.GetService<Services.StartupNavigation>()?.RequestAlbum(albumPath);
    }
}
