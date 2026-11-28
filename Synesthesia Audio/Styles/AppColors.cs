using Microsoft.Maui.Graphics;

namespace Synesthesia_Audio.Styles;

public static class AppColors
{
    public static readonly Color PrimaryLight = Color.FromArgb("#458CC8");
    public static readonly Color PrimaryDark = Color.FromArgb("#AADBE9");

    public static readonly Color SuccessLight = Color.FromArgb("#5AA1D8");
    public static readonly Color SuccessDark = Color.FromArgb("#AADBE9");
    public static readonly Color SuccessAccentLight = Color.FromArgb("#83C3EE");
    public static readonly Color SuccessAccentDark = Color.FromArgb("#DAEEF1");

    public static readonly Color WarningLight = Color.FromArgb("#5AA1D8");
    public static readonly Color WarningDark = Color.FromArgb("#84C6DF");

    // Darker than Colors.Red so white badge text reaches 6.2:1 contrast (plain red was 4.0:1, under the 4.5:1 floor).
    public static readonly Color RjBadge = Color.FromArgb("#C1121F");

    public static readonly Color DangerLight = Color.FromArgb("#FF3B30");
    public static readonly Color DangerDark = Color.FromArgb("#FF453A");

    public static readonly Color DangerBgLight = Color.FromArgb("#FAD2E1");
    public static readonly Color DangerBgDark = Color.FromArgb("#3D1C24");
    // Chosen for contrast: 6.2:1 on white, 4.6:1 on the danger background.
    public static readonly Color DangerTextLight = Color.FromArgb("#C1121F");
    // 5.3:1 on the dark background.
    public static readonly Color DangerTextDark = Color.FromArgb("#FF6B6B");

    public static readonly Color BackgroundLight = Color.FromArgb("#FFFFFF");
    public static readonly Color BackgroundDark = Color.FromArgb("#112A3E");
    public static readonly Color BackgroundAltDark = Color.FromArgb("#214F6F");

    public static readonly Color SurfaceLight = Color.FromArgb("#DFEBF7");
    public static readonly Color SurfaceDark = Color.FromArgb("#2A5A7A");
    public static readonly Color SurfaceAltDark = Color.FromArgb("#112A3E");

    public static readonly Color StrokeLight = Color.FromArgb("#83C3EE");
    public static readonly Color StrokeDark = Color.FromArgb("#84C6DF");
    public static readonly Color StrokeAltLight = Color.FromArgb("#A1D3F9");
    public static readonly Color StrokeAltDark = Color.FromArgb("#84C6DF");

    public static readonly Color ControlBgLight = Color.FromArgb("#A1D3F9");
    public static readonly Color ControlBgDark = Color.FromArgb("#214F6F");
    public static readonly Color PickerBgDark = Color.FromArgb("#112A3E");

    public static readonly Color TextPrimaryLight = Color.FromArgb("#112A3E");
    public static readonly Color TextPrimaryDark = Color.FromArgb("#DAEEF1");

    // 5.4:1 on white, meeting WCAG AA for body text.
    public static readonly Color TextSecondaryLight = Color.FromArgb("#2F6EA3");
    public static readonly Color TextSecondaryDark = Color.FromArgb("#84C6DF");

    // 5.4:1 on white; the previous lighter tone failed even the large-text floor.
    public static readonly Color TextTertiaryLight = Color.FromArgb("#3A6F99");
    public static readonly Color TextTertiaryDark = Color.FromArgb("#84C6DF");
    public static readonly Color TextTertiaryAltDark = Color.FromArgb("#AADBE9");

    // Form labels are read at body size, so they use the 4.5:1-compliant tone.
    public static readonly Color TextFormLabelLight = Color.FromArgb("#2F6EA3");
    public static readonly Color TextFormLabelDark = Color.FromArgb("#DAEEF1");
    public static readonly Color TextFormLabelAltDark = Color.FromArgb("#84C6DF");

    public static readonly Color TextFormInputLight = Color.FromArgb("#112A3E");
    public static readonly Color TextFormInputDark = Color.FromArgb("#DAEEF1");

    public static readonly Color CoverPlaceholder = Color.FromArgb("#214F6F");
    public static readonly Color CoverPlaceholderText = Color.FromArgb("#84C6DF");

    public static readonly Color ActiveBgLight = Color.FromArgb("#A1D3F94D");
    public static readonly Color ActiveBgDark = Color.FromArgb("#214F6F66");
    public static readonly Color ActiveTextLight = Color.FromArgb("#2F6EA3");
    public static readonly Color ActiveTextDark = Color.FromArgb("#AADBE9");
    public static readonly Color NormalTextLight = Color.FromArgb("#112A3E");

    // Dark glass is a low-alpha white rather than the backdrop's teal, which made panels vanish into the aurora.
    public static readonly Color GlassFillLight = Color.FromArgb("#A6FFFFFF");
    public static readonly Color GlassFillDark = Color.FromArgb("#1AFFFFFF");
    public static readonly Color GlassFillStrongLight = Color.FromArgb("#DBFFFFFF");
    public static readonly Color GlassFillStrongDark = Color.FromArgb("#26AADBE9");
    public static readonly Color GlassFillSoftLight = Color.FromArgb("#7AFFFFFF");
    public static readonly Color GlassFillSoftDark = Color.FromArgb("#0FFFFFFF");

    public static readonly Color GlassStrokeTopLight = Color.FromArgb("#B3FFFFFF");
    public static readonly Color GlassStrokeTopDark = Color.FromArgb("#5CAADBE9");
    public static readonly Color GlassStrokeEdgeLight = Color.FromArgb("#4D83C3EE");
    public static readonly Color GlassStrokeEdgeDark = Color.FromArgb("#3D84C6DF");

    public static readonly Color GlassShadowLight = Color.FromArgb("#38458CC8");
    public static readonly Color GlassShadowDark = Color.FromArgb("#59000000");

    public static readonly Color GlassTintLight = Color.FromArgb("#3383C3EE");
    public static readonly Color GlassTintDark = Color.FromArgb("#33AADBE9");

    public static readonly Color GlassDeepLight = Color.FromArgb("#E6112A3E");
    public static readonly Color GlassDeepDark = Color.FromArgb("#D90B1C2B");

    public static readonly Color GlassScrimTransparentLight = Color.FromArgb("#00FFFFFF");
    public static readonly Color GlassScrimTransparentDark = Color.FromArgb("#00112A3E");

    // Not fully opaque: the faintly visible page behind is what makes the flyout read as glass, not a solid drawer.
    public static readonly Color GlassFlyoutLight = Color.FromArgb("#E6FFFFFF");
    public static readonly Color GlassFlyoutDark = Color.FromArgb("#E6112A3E");

    // Playback bars sit over dense page content, so they need enough opacity to keep cast and time text legible.
    public static readonly Color GlassFillPlayerLight = Color.FromArgb("#E6F7FBFE");
    public static readonly Color GlassFillPlayerDark = Color.FromArgb("#E6102334");

    // The shelf card's label plate: near-opaque on purpose, because a cover can be dark or pure white.
    public static readonly Color GlassLabelLight = Color.FromArgb("#F5FAFDFF");
    public static readonly Color GlassLabelDark = Color.FromArgb("#F50E2132");

    // Artwork colours are arbitrary, so anything printed on a cover sits on the same navy scrim in both themes.
    public static readonly Color CoverScrimClear = Color.FromArgb("#00000000");
    public static readonly Color CoverScrimDeep = Color.FromArgb("#A6000000");
    public static readonly Color CoverBadgeFill = Color.FromArgb("#B30B1C2B");
    public static readonly Color CoverBadgeText = Color.FromArgb("#DAEEF1");

    // The backdrop is a dim listening room: edge vignette and dust motes stop the aurora reading as wallpaper.
    public static readonly Color VignetteLight = Color.FromArgb("#33458CC8");
    public static readonly Color VignetteDark = Color.FromArgb("#A60B1C2B");
    public static readonly Color MoteLight = Color.FromArgb("#458CC8");
    public static readonly Color MoteDark = Color.FromArgb("#DAEEF1");
}
