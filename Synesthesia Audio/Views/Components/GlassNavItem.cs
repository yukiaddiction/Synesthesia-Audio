using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>Shared by the desktop sidebar and the compact flyout so both stay identical.</summary>
public sealed class GlassNavItem : Border
{
    private readonly Label _label;
    private readonly BoxView _accent;

    public GlassNavItem(string text, EventHandler<TappedEventArgs>? onTap = null)
    {
        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius };
        StrokeThickness = 0;
        Padding = new Thickness(8, 10);
        Margin = new Thickness(4, 2, 4, 2);

        _accent = new BoxView
        {
            WidthRequest = AppLayout.GlassTintBorderSize,
            CornerRadius = AppLayout.GlassTintBorderSize / 2,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = false
        };

        _label = new Label
        {
            Text = text,
            FontSize = AppFontSizes.BodyBold,
            FontAttributes = AppFonts.Bold,
            VerticalOptions = LayoutOptions.Center
        };

        Content = new Grid
        {
            ColumnDefinitions = Columns.Define(AppLayout.GlassTintBorderSize, Star),
            ColumnSpacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                _accent.Column(0),
                _label.Column(1)
            }
        };

        if (onTap != null)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += onTap;
            GestureRecognizers.Add(tap);
        }

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => { if (!IsActive) Opacity = 0.75; };
        pointer.PointerExited += (_, _) => Opacity = 1;
        GestureRecognizers.Add(pointer);

        ToolTipProperties.SetText(this, text);
        SemanticProperties.SetDescription(this, text);
        if (onTap != null) SemanticProperties.SetHint(this, "Navigates to this section");
        SetActive(false);
    }

    public bool IsActive { get; private set; }

    /// <summary>Uses AppThemeBinding because reading RequestedTheme can lag the theme-changed notification.</summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;

        this.AppThemeBinding(
            VisualElement.BackgroundColorProperty,
            isActive ? AppColors.GlassTintLight : Colors.Transparent,
            isActive ? AppColors.GlassTintDark : Colors.Transparent);

        _label.AppThemeBinding(
            Label.TextColorProperty,
            isActive ? AppColors.ActiveTextLight : AppColors.NormalTextLight,
            isActive ? AppColors.ActiveTextDark : AppColors.TextPrimaryDark);

        _accent.IsVisible = isActive;
        _accent.AppThemeBinding(
            BoxView.ColorProperty,
            isActive ? AppColors.PrimaryLight : Colors.Transparent,
            isActive ? AppColors.PrimaryDark : Colors.Transparent);
    }
}
