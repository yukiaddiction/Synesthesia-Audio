using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace Synesthesia_Audio.Styles;

public static class AppLayout
{
    public static readonly Thickness PagePadding = new Thickness(20, 20, 20, 110);
    public static readonly Thickness StandardPadding = new Thickness(20);
    public static readonly Thickness PathHeaderPadding = new Thickness(15, 10);
    public static readonly Thickness FloatingPlayerPadding = new Thickness(15, 12);
    public static readonly Thickness TagRowPadding = new Thickness(10, 8);
    public static readonly Thickness MainScanMargin = new Thickness(10, 5);
    public static readonly Thickness ButtonPadding = new Thickness(8, 0);
    public static readonly Thickness MainHeaderPadding = new Thickness(15, 10, 15, 5);
    public static readonly Thickness MainScrollPadding = new Thickness(15, 5, 15, 20);

    public static readonly Thickness FloatingPlayerMargin = new Thickness(15, 10, 15, 15);
    public static readonly Thickness FloatingPlayerSliderMargin = new Thickness(-15, 8, -15, -12);
    public static readonly Thickness HeaderImageNegativeMargin = new Thickness(-20, -20, -20, 20);
    public static readonly Thickness WorkCardMargin = new Thickness(5);
    public static readonly Thickness TextStackMargin = new Thickness(8, 4, 8, 0);
    public static readonly Thickness TextCircleMargin = new Thickness(8, 4, 8, 8);
    public static readonly Thickness LibraryScanPadding = new Thickness(15, 8);
    public static readonly Thickness LibraryScanMargin = new Thickness(0, 0, 0, 90);
    public static readonly Thickness CollectionViewMargin = new Thickness(0, 0, 0, 95);
    
    public const double SpacingNone = 0;
    public const double SpacingExtraSmall = 2;
    public const double SpacingSmall = 4;
    public const double SpacingMedium = 6;
    public const double SpacingLarge = 8;
    public const double SpacingExtraLarge = 10;
    public const double SpacingSection = 15;
    public const double SpacingPage = 20;

    public static readonly CornerRadius CardCornerRadius = new CornerRadius(14);
    public static readonly CornerRadius ButtonCornerRadius = new CornerRadius(10);
    public static readonly CornerRadius ChipCornerRadius = new CornerRadius(6);
    public static readonly CornerRadius FloatingPlayerCornerRadius = new CornerRadius(16);

    public const double ButtonHeightDefault = 45;
    public const double ButtonHeightCompact = 38;
    public const double ButtonHeightSmall = 30;
    public const double HeaderCoverHeight = 180;
    public const double CoverGridHeight = 160;
    public const double ProgressBarHeight = 4;
    public const double VideoInlineHeight = 250;
    public const double VideoInlineBottomMargin = 120;

    public static readonly CornerRadius GlassBarRadius = new CornerRadius(20);
    public static readonly CornerRadius GlassSheetRadius = new CornerRadius(28);
    public static readonly CornerRadius GlassCardRadius = new CornerRadius(22);
    public static readonly CornerRadius GlassFooterRadius = new CornerRadius(16);
    public static readonly CornerRadius GlassPillRadius = new CornerRadius(999);
    public static readonly CornerRadius GlassChipRadius = new CornerRadius(12);

    public static readonly Thickness GlassBarMargin = new Thickness(16, 12, 16, 4);
    public static readonly Thickness GlassBarPadding = new Thickness(10);
    public static readonly Thickness GlassCardPadding = new Thickness(18);
    public static readonly Thickness GlassContentPadding = new Thickness(16, 6, 16, 130);
    public static readonly Thickness GlassSheetOverlap = new Thickness(0, -36, 0, 0);
    public static readonly Thickness GlassCardFooterMargin = new Thickness(8, -18, 8, 8);

    public const double GlassBarHeight = 58;
    public const double GlassBarButtonSize = 38;
    public const double GlassSectionGap = 18;
    public const double GlassCardGap = 12;
    public const double GlassHeroHeight = 260;
    public const double GlassCardCoverHeight = 150;
    public const double GlassTintBorderSize = 3;
}
