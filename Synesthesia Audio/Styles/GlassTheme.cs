using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Synesthesia_Audio.Styles;

/// <summary>
/// Shared glassmorphism treatments. Every colour here comes from <see cref="AppColors"/>,
/// so the glass layer never introduces a hue that is not already in the app palette.
/// </summary>
public static class GlassTheme
{
    /// <summary>Ambient lift used by floating glass surfaces (bar, player, sheet, hero card).</summary>
    public static Shadow Elevation(double offsetY = 10, float radius = 24f, float opacity = 0.35f)
    {
        var shadow = new Shadow
        {
            Offset = new Point(0, offsetY),
            Radius = radius,
            Opacity = opacity
        };

        shadow.AppThemeBinding(
            Shadow.BrushProperty,
            new SolidColorBrush(AppColors.GlassShadowLight),
            new SolidColorBrush(AppColors.GlassShadowDark));

        return shadow;
    }

    /// <summary>Uppercase section heading that replaces hard dividers between card groups.</summary>
    public static Label SectionLabel(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = AppFontSizes.ButtonSmall,
            FontAttributes = AppFonts.Bold,
            CharacterSpacing = 1.6,
            Margin = new Thickness(6, 6, 0, 0)
        }
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);
    }

    /// <summary>A translucent chip used for genres and metadata pills.</summary>
    public static Border Chip(View content)
    {
        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            StrokeThickness = 1,
            Padding = new Thickness(10, 5),
            Content = content
        }.GlassSoft();
    }

    /// <summary>Square glass icon button (app bar affordances).</summary>
    public static Button IconButton(string glyph, double fontSize = 15, double size = AppLayout.GlassBarButtonSize)
    {
        var button = new Button
        {
            Text = glyph,
            FontSize = fontSize,
            WidthRequest = size,
            HeightRequest = size,
            Padding = 0,
            CornerRadius = (int)(size / 2)
        };

        return button.GlassPill();
    }

    /// <summary>
    /// Themes a switch so its track uses the brand colour instead of the platform default blue.
    /// </summary>
    public static TSwitch GlassSwitch<TSwitch>(this TSwitch toggle) where TSwitch : Switch
    {
        toggle.AppThemeBinding(Switch.OnColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark);
        toggle.AppThemeBinding(Switch.ThumbColorProperty, Colors.White, AppColors.TextPrimaryLight);
        return toggle;
    }

    /// <summary>Rounded glass text button used for actions inside cards and sheets.</summary>
    public static Button PillButton(string text, double fontSize, bool accent = false, double height = AppLayout.GlassBarButtonSize)
    {
        var button = new Button
        {
            Text = text,
            FontSize = fontSize,
            HeightRequest = height,
            Padding = new Thickness(16, 0),
            CornerRadius = (int)(height / 2)
        };

        return button.GlassPill(accent);
    }
}

public static class GlassExtensions
{
    /// <summary>Standard frosted panel: translucent fill plus a light edge stroke.</summary>
    public static TBorder Glass<TBorder>(this TBorder border, bool strong = false, bool elevated = false)
        where TBorder : Border
    {
        border.StrokeThickness = 1;

        border.AppThemeBinding(
            VisualElement.BackgroundColorProperty,
            strong ? AppColors.GlassFillStrongLight : AppColors.GlassFillLight,
            strong ? AppColors.GlassFillStrongDark : AppColors.GlassFillDark);

        border.AppThemeBinding(
            Border.StrokeProperty,
            new SolidColorBrush(strong ? AppColors.GlassStrokeTopLight : AppColors.GlassStrokeEdgeLight),
            new SolidColorBrush(strong ? AppColors.GlassStrokeTopDark : AppColors.GlassStrokeEdgeDark));

        if (elevated)
            border.Shadow = GlassTheme.Elevation();

        return border;
    }

    /// <summary>Lighter glass used for nested elements that sit on top of another glass panel.</summary>
    public static TBorder GlassSoft<TBorder>(this TBorder border, bool elevated = false)
        where TBorder : Border
    {
        border.StrokeThickness = 1;

        border.AppThemeBinding(
            VisualElement.BackgroundColorProperty,
            AppColors.GlassFillSoftLight,
            AppColors.GlassFillSoftDark);

        border.AppThemeBinding(
            Border.StrokeProperty,
            new SolidColorBrush(AppColors.GlassStrokeEdgeLight),
            new SolidColorBrush(AppColors.GlassStrokeEdgeDark));

        if (elevated)
            border.Shadow = GlassTheme.Elevation();

        return border;
    }

    /// <summary>Deep glass used by the log terminal where text contrast matters most.</summary>
    public static TBorder GlassDeep<TBorder>(this TBorder border) where TBorder : Border
    {
        border.StrokeThickness = 1;

        border.AppThemeBinding(
            VisualElement.BackgroundColorProperty,
            AppColors.GlassDeepLight,
            AppColors.GlassDeepDark);

        border.AppThemeBinding(
            Border.StrokeProperty,
            new SolidColorBrush(AppColors.GlassStrokeEdgeLight),
            new SolidColorBrush(AppColors.GlassStrokeEdgeDark));

        return border;
    }

    /// <summary>Brand tint marking the active navigation item or the playing track.</summary>
    public static TBorder GlassTint<TBorder>(this TBorder border) where TBorder : Border
    {
        border.AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassTintLight, AppColors.GlassTintDark);
        return border;
    }

    /// <summary>
    /// Glass button. <paramref name="accent"/> marks primary actions; the dark-mode accent
    /// uses dark text on the light brand colour so contrast stays readable.
    /// </summary>
    public static TButton GlassPill<TButton>(this TButton button, bool accent = false)
        where TButton : Button
    {
        button.BorderWidth = 1;

        if (accent)
        {
            button.AppThemeBinding(Button.TextColorProperty, Colors.White, AppColors.TextPrimaryLight);
            button.AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark);
            button.AppThemeBinding(Button.BorderColorProperty, Colors.Transparent, Colors.Transparent);
        }
        else
        {
            button.AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);
            button.AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);
            button.AppThemeBinding(Button.BorderColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark);
        }

        return button;
    }
}
