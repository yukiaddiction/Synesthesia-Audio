using System.ComponentModel;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Views;

namespace Synesthesia_Audio;

public class App : Application
{
    private readonly IServiceProvider _services;
    private readonly AudioService _audioService;
    private readonly LibraryCacheService _cacheService;
    private readonly ScanStateService _scanStateService;
    private readonly MainViewModel _mainViewModel;
    private readonly ILogger<App> _logger;
    private bool _sessionRestoreStarted;

    public App(
        IServiceProvider services,
        SettingsService settingsService,
        AudioService audioService,
        LibraryCacheService cacheService,
        ScanStateService scanStateService,
        MainViewModel mainViewModel,
        ILogger<App> logger)
    {
        _services = services;
        _audioService = audioService;
        _cacheService = cacheService;
        _scanStateService = scanStateService;
        _mainViewModel = mainViewModel;
        _logger = logger;
        UserAppTheme = settingsService.CurrentSettings.IsDarkMode ? AppTheme.Dark : AppTheme.Light;

        Styles.AppFontSizes.Initialize();
        Services.Logging.FileLogger.PurgeOldLogs();
        _logger.LogInformation("App started — theme: {Theme}", UserAppTheme);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // A re-created activity needs a fresh, unparented UI graph. Reusing the singleton Shell
        // made Window.OnPageChanged run against a null MauiContext and killed the process.
        var shell = _services.GetRequiredService<AppShell>();

        // A view cannot be re-parented into a new window, so each window gets its own element.
        var mediaElement = new MediaElement
        {
            ShouldShowPlaybackControls = true,
            WidthRequest = 0,
            HeightRequest = 0,
            Opacity = 0
        };
        _audioService.SetMediaElement(mediaElement);
        shell.SetMediaElement(mediaElement);

        var window = new Window(shell);

        // Declared outside the WINDOWS block because the shutdown path below unsubscribes it on every
        // platform; it stays null where there is no desktop chrome to re-apply.
        EventHandler<AppThemeChangedEventArgs>? themeChangedHandler = null;

#if WINDOWS
        // Desktop window contract: open at a comfortable size, never below what the wide
        // composition needs, and remember whatever the user resized it to.
        window.Width = Preferences.Get("WindowWidth", 1440d);
        window.Height = Preferences.Get("WindowHeight", 900d);
        window.MinimumWidth = 880;
        window.MinimumHeight = 640;

        // Per window, not per app: this used to be a field on the singleton App, so a re-created window
        // never had its chrome or keyboard shortcuts attached again.
        var desktopChromeApplied = false;

        window.HandlerChanged += (_, _) =>
        {
            if (desktopChromeApplied)
                return;

            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
            {
                desktopChromeApplied = true;
                Desktop.WindowChrome.Apply(native, Application.Current?.RequestedTheme == AppTheme.Dark);
                Desktop.KeyboardShortcuts.Attach(native);
            }
        };

        // Application lives for the whole process, so this handler is unsubscribed when the window dies;
        // otherwise every re-created window leaked another one that still targeted the dead window.
        if (Application.Current != null)
        {
            themeChangedHandler = (_, _) => MainThread.BeginInvokeOnMainThread(() => ApplyDesktopChrome(window));
            Application.Current.RequestedThemeChanged += themeChangedHandler;
        }
#endif

        window.Destroying += (s, e) =>
        {
            try
            {
                if (themeChangedHandler != null && Application.Current != null)
                {
                    Application.Current.RequestedThemeChanged -= themeChangedHandler;
                }

                SaveWindowBounds(window);
                _cacheService.FlushPendingSave();
                _scanStateService.FlushPendingSave();
                _audioService.DetachMediaElement();
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
                SaveWindowBounds(window);
                _cacheService.FlushPendingSave();
                _scanStateService.FlushPendingSave();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cache flush on window stop failed");
            }
        };
        window.Resumed += (s, e) =>
        {
            try
            {
                MainThread.BeginInvokeOnMainThread(async () => await _mainViewModel.HandleResumeAsync());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Resume handling failed");
            }
        };
        if (!_sessionRestoreStarted)
        {
            _sessionRestoreStarted = true;
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

        return window;
    }

    /// <summary>
    /// Persists the current window size so the next launch reopens at the same place.
    /// Only the Windows target has a resizable desktop window.
    /// </summary>
    private static void SaveWindowBounds(Window window)
    {
#if WINDOWS
        try
        {
            if (window.Width > 0)
                Preferences.Set("WindowWidth", window.Width);
            if (window.Height > 0)
                Preferences.Set("WindowHeight", window.Height);
        }
        catch
        {
            // Window bounds are a convenience, never a failure path.
        }
#endif
    }

#if WINDOWS
    private static void ApplyDesktopChrome(Window window)
    {
        if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
            Desktop.WindowChrome.Apply(native, Application.Current?.RequestedTheme == AppTheme.Dark);
    }
#endif
}
