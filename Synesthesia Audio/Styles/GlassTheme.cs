using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;

namespace Synesthesia_Audio.Styles;

/// <summary>
/// Glass treatments draw only from <see cref="AppColors"/> so they never add a hue outside the palette.
/// </summary>
public static class GlassTheme
{
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
        .Invoke(l => SemanticProperties.SetHeadingLevel(l, SemanticHeadingLevel.Level2))
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);
    }

    /// <summary>
    /// Use for every form field so none ships with default colours, which are unreadable in dark mode.
    /// </summary>
    public static Entry FormEntry(string? text = null, string? placeholder = null,
        bool isPassword = false, bool isReadOnly = false, bool fillWidth = false)
    {
        var entry = new Entry
        {
            Text = text,
            Placeholder = placeholder,
            IsPassword = isPassword,
            IsReadOnly = isReadOnly
        };

        if (fillWidth) entry.HorizontalOptions = LayoutOptions.Fill;

        entry.AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark);
        entry.AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);
        return entry;
    }

    /// <summary>The single form-label style, so labels look the same on every page.</summary>
    public static Label FormLabel(string text) =>
        new Label { Text = text, FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold }
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark);
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

    /// <summary>Replaces the platform's default blue track with the brand colour.</summary>
    public static TSwitch GlassSwitch<TSwitch>(this TSwitch toggle) where TSwitch : Switch
    {
        toggle.AppThemeBinding(Switch.OnColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark);
        toggle.AppThemeBinding(Switch.ThumbColorProperty, Colors.White, AppColors.TextPrimaryLight);
        return toggle;
    }

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

    /// <summary>For elements nested on another glass panel.</summary>
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

    /// <summary>Near-opaque glass for the log terminal, where text contrast matters most.</summary>
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

    public static TBorder GlassTint<TBorder>(this TBorder border) where TBorder : Border
    {
        border.AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassTintLight, AppColors.GlassTintDark);
        return border;
    }

    /// <summary>
    /// <paramref name="accent"/> marks primary actions; in dark mode it uses dark text, since the brand colour there is light.
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
