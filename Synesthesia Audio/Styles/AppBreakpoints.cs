namespace Synesthesia_Audio.Styles;

/// <summary>
/// Shared so the sidebar, preview pane and page grids all switch between phone and desktop layouts at the same width.
/// </summary>
public static class AppBreakpoints
{
    public const double Wide = 1000;

    public const double SidebarWidth = 264;
    public const double PreviewPaneWidth = 340;
    public const double AlbumMetaWidth = 380;
    public const double TransportDockHeight = 76;
    public const double SettingsRailWidth = 220;

    // Caps line length so text stays readable on wide windows.
    public const double ReaderMaxWidth = 820;

    public static bool IsWide(double width) => width >= Wide;
}
