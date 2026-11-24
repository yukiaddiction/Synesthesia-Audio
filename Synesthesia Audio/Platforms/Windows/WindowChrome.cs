#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Synesthesia_Audio.Desktop;

/// <summary>
/// Paints the Windows title bar with the app palette so the caption strip stops clashing with the
/// theme. This is the one place the design leaves MAUI's cross-platform surface.
/// </summary>
internal static class WindowChrome
{
    public static void Apply(Microsoft.UI.Xaml.Window? window, bool isDark)
    {
        var appWindow = GetAppWindow(window);
        if (appWindow?.TitleBar is not { } titleBar)
            return;

        if (!AppWindowTitleBar.IsCustomizationSupported())
            return;

        var background = isDark ? Rgb(0x11, 0x2A, 0x3E) : Rgb(0xFF, 0xFF, 0xFF);
        var foreground = isDark ? Rgb(0xDA, 0xEE, 0xF1) : Rgb(0x11, 0x2A, 0x3E);
        var hover = isDark ? Rgb(0x21, 0x4F, 0x6F) : Rgb(0xDF, 0xEB, 0xF7);
        var inactive = isDark ? Rgb(0x84, 0xC6, 0xDF) : Rgb(0x45, 0x8C, 0xC8);

        titleBar.BackgroundColor = background;
        titleBar.ForegroundColor = foreground;
        titleBar.InactiveBackgroundColor = background;
        titleBar.InactiveForegroundColor = inactive;
        titleBar.ButtonBackgroundColor = background;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = hover;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonInactiveBackgroundColor = background;
        titleBar.ButtonInactiveForegroundColor = inactive;
    }

    private static AppWindow? GetAppWindow(Microsoft.UI.Xaml.Window? window)
    {
        if (window is null)
            return null;

        try
        {
            var handle = WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            return AppWindow.GetFromWindowId(windowId);
        }
        catch
        {
            // Title-bar theming is cosmetic; never let it take the window down.
            return null;
        }
    }

    private static Windows.UI.Color Rgb(byte r, byte g, byte b) => ColorHelper.FromArgb(255, r, g, b);
}
#endif
