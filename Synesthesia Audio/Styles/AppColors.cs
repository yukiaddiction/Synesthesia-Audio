using Microsoft.Maui.Graphics;

namespace Synesthesia_Audio.Styles;

public static class AppColors
{
    // Primary / Brand colors
    public static readonly Color PrimaryLight = Color.FromArgb("#458CC8");
    public static readonly Color PrimaryDark = Color.FromArgb("#AADBE9");

    // Success / Play / Save colors
    public static readonly Color SuccessLight = Color.FromArgb("#5AA1D8");
    public static readonly Color SuccessDark = Color.FromArgb("#AADBE9");
    public static readonly Color SuccessAccentLight = Color.FromArgb("#83C3EE");
    public static readonly Color SuccessAccentDark = Color.FromArgb("#DAEEF1");

    // Warning / Clean colors
    public static readonly Color WarningLight = Color.FromArgb("#5AA1D8");
    public static readonly Color WarningDark = Color.FromArgb("#84C6DF");

    /// <summary>
    /// The DLsite product-id badge background. Plain <c>Colors.Red</c> gave white text only 4.0:1, below the
    /// 4.5:1 floor for small text; this measures 6.2:1 (UX-01).
    /// </summary>
    public static readonly Color RjBadge = Color.FromArgb("#C1121F");

    // Danger / Delete colors
    public static readonly Color DangerLight = Color.FromArgb("#FF3B30");
    public static readonly Color DangerDark = Color.FromArgb("#FF453A");
    
    public static readonly Color DangerBgLight = Color.FromArgb("#FAD2E1");
    public static readonly Color DangerBgDark = Color.FromArgb("#3D1C24");
    // 4.2:1 on white and 3.1:1 on the danger background were both too low; this is 6.2 / 4.6 (UX-01).
    public static readonly Color DangerTextLight = Color.FromArgb("#C1121F");
    // 3.5:1 on the dark background; this is 5.3:1 (UX-01).
    public static readonly Color DangerTextDark = Color.FromArgb("#FF6B6B");

    // Backgrounds
    public static readonly Color BackgroundLight = Color.FromArgb("#FFFFFF");
    public static readonly Color BackgroundDark = Color.FromArgb("#112A3E");
    public static readonly Color BackgroundAltDark = Color.FromArgb("#214F6F");

    // Surface / Cards / Borders
    public static readonly Color SurfaceLight = Color.FromArgb("#DFEBF7");
    public static readonly Color SurfaceDark = Color.FromArgb("#2A5A7A");
    public static readonly Color SurfaceAltDark = Color.FromArgb("#112A3E");
    
    public static readonly Color StrokeLight = Color.FromArgb("#83C3EE");
    public static readonly Color StrokeDark = Color.FromArgb("#84C6DF");
    public static readonly Color StrokeAltLight = Color.FromArgb("#A1D3F9");
    public static readonly Color StrokeAltDark = Color.FromArgb("#84C6DF");

    // Gray / Control backgrounds
    public static readonly Color ControlBgLight = Color.FromArgb("#A1D3F9");
    public static readonly Color ControlBgDark = Color.FromArgb("#214F6F");
    public static readonly Color PickerBgDark = Color.FromArgb("#112A3E");

    // Text colors
    public static readonly Color TextPrimaryLight = Color.FromArgb("#112A3E");
    public static readonly Color TextPrimaryDark = Color.FromArgb("#DAEEF1");

    // 3.6:1 on white was below WCAG AA for body text; this is 5.4:1 (UX-01).
    public static readonly Color TextSecondaryLight = Color.FromArgb("#2F6EA3");
    public static readonly Color TextSecondaryDark = Color.FromArgb("#84C6DF");

    // 2.8:1 on white failed even the large-text floor; this is 5.4:1 (UX-01).
    public static readonly Color TextTertiaryLight = Color.FromArgb("#3A6F99");
    public static readonly Color TextTertiaryDark = Color.FromArgb("#84C6DF");
    public static readonly Color TextTertiaryAltDark = Color.FromArgb("#AADBE9");
    
    // Form labels are read at body size, so they need the 4.5:1 token (UX-01).
    public static readonly Color TextFormLabelLight = Color.FromArgb("#2F6EA3");
    public static readonly Color TextFormLabelDark = Color.FromArgb("#DAEEF1");
    public static readonly Color TextFormLabelAltDark = Color.FromArgb("#84C6DF");

    public static readonly Color TextFormInputLight = Color.FromArgb("#112A3E");
    public static readonly Color TextFormInputDark = Color.FromArgb("#DAEEF1");

    // Static Cover/Media Color
    public static readonly Color CoverPlaceholder = Color.FromArgb("#214F6F");
    public static readonly Color CoverPlaceholderText = Color.FromArgb("#84C6DF");

    // Navigation / Active indicator colors
    public static readonly Color ActiveBgLight = Color.FromArgb("#A1D3F94D");
    public static readonly Color ActiveBgDark = Color.FromArgb("#214F6F66");
    public static readonly Color ActiveTextLight = Color.FromArgb("#2F6EA3");
    public static readonly Color ActiveTextDark = Color.FromArgb("#AADBE9");
    public static readonly Color NormalTextLight = Color.FromArgb("#112A3E");

    // ── Glassmorphism surfaces ──
    // Semi-transparent layers derived from the palette above so the aurora backdrop shows through panels.
    // Dark glass leans on a low-alpha white overlay: tinting with the backdrop's own teal made panels
    // disappear into the aurora instead of reading as luminous frost.
    public static readonly Color GlassFillLight = Color.FromArgb("#A6FFFFFF");
    public static readonly Color GlassFillDark = Color.FromArgb("#1AFFFFFF");
    public static readonly Color GlassFillStrongLight = Color.FromArgb("#DBFFFFFF");
    public static readonly Color GlassFillStrongDark = Color.FromArgb("#26AADBE9");
    public static readonly Color GlassFillSoftLight = Color.FromArgb("#7AFFFFFF");
    public static readonly Color GlassFillSoftDark = Color.FromArgb("#0FFFFFFF");

    // Highlight (top edge of the glass) and edge (bottom falloff) strokes
    public static readonly Color GlassStrokeTopLight = Color.FromArgb("#B3FFFFFF");
    public static readonly Color GlassStrokeTopDark = Color.FromArgb("#5CAADBE9");
    public static readonly Color GlassStrokeEdgeLight = Color.FromArgb("#4D83C3EE");
    public static readonly Color GlassStrokeEdgeDark = Color.FromArgb("#3D84C6DF");

    // Ambient lift under floating glass
    public static readonly Color GlassShadowLight = Color.FromArgb("#38458CC8");
    public static readonly Color GlassShadowDark = Color.FromArgb("#59000000");

    // Brand tint for active navigation items and the currently playing track
    public static readonly Color GlassTintLight = Color.FromArgb("#3383C3EE");
    public static readonly Color GlassTintDark = Color.FromArgb("#33AADBE9");

    // Deep glass used for the log terminal panel and media scrims
    public static readonly Color GlassDeepLight = Color.FromArgb("#E6112A3E");
    public static readonly Color GlassDeepDark = Color.FromArgb("#D90B1C2B");

    // Scrim fading the album hero into the backdrop
    public static readonly Color GlassScrimTransparentLight = Color.FromArgb("#00FFFFFF");
    public static readonly Color GlassScrimTransparentDark = Color.FromArgb("#00112A3E");

    // Flyout pane: high alpha so the page behind stays faintly visible, which is what makes
    // the pane read as glass rather than as a solid drawer.
    public static readonly Color GlassFlyoutLight = Color.FromArgb("#E6FFFFFF");
    public static readonly Color GlassFlyoutDark = Color.FromArgb("#E6112A3E");
}
