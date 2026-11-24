namespace Synesthesia_Audio.Styles;

/// <summary>
/// Width thresholds that switch between the compact (phone) composition and the desktop one.
/// Keeping these in one place stops the sidebar, preview pane and page grids from disagreeing.
/// </summary>
public static class AppBreakpoints
{
    /// <summary>At or above this width the persistent sidebar and multi-pane layouts are used.</summary>
    public const double Wide = 1000;

    public const double SidebarWidth = 264;
    public const double PreviewPaneWidth = 340;
    public const double AlbumMetaWidth = 380;
    public const double TransportDockHeight = 76;
    public const double SettingsRailWidth = 220;

    /// <summary>Caps line length on wide panels so text does not stretch across the window.</summary>
    public const double ReaderMaxWidth = 820;

    public static bool IsWide(double width) => width >= Wide;
}
