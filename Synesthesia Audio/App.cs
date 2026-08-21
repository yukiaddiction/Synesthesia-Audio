using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Views;

namespace Synesthesia_Audio;

public class App : Application
{
    private readonly AppShell _shell;
    private readonly AudioService _audioService;
    private readonly LibraryCacheService _cacheService;
    private readonly ILogger<App> _logger;

    public App(
        AppShell shell,
        SettingsService settingsService,
        AudioService audioService,
        LibraryCacheService cacheService,
        ILogger<App> logger)
    {
        _shell = shell;
        _audioService = audioService;
        _cacheService = cacheService;
        _logger = logger;
        UserAppTheme = settingsService.CurrentSettings.IsDarkMode ? AppTheme.Dark : AppTheme.Light;

        Styles.AppFontSizes.Initialize();
        Services.Logging.FileLogger.PurgeOldLogs();
        _logger.LogInformation("App started — theme: {Theme}", UserAppTheme);

        var mediaElement = new MediaElement
        {
            ShouldShowPlaybackControls = true,
            WidthRequest = 0,
            HeightRequest = 0,
            Opacity = 0
        };
        _audioService.SetMediaElement(mediaElement);
        _shell.SetMediaElement(mediaElement);

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(300);
                await _audioService.TryRestoreSessionAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session restore failed");
            }
        });
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell);
        window.Destroying += (s, e) =>
        {
            try
            {
                _cacheService.FlushPendingSave();
                _audioService.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "App shutdown cleanup failed");
            }
        };
        window.Stopped += (s, e) =>
        {
            try
            {
                _cacheService.FlushPendingSave();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache flush on window stop failed");
            }
        };
        return window;
    }
}
