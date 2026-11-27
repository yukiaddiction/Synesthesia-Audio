using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

public class AlbumCardView : Border
{
    private readonly Shadow _hoverShadow = GlassTheme.Elevation(6, 18, 0.28f);

    public AlbumCardView(MainViewModel viewModel, Func<bool> isWide)
    {
        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius };
        Padding = 0;
        Margin = AppLayout.WorkCardMargin;

        var cover = CoverImageView.Card(nameof(WorkMetadata.CoverImagePath));

        // The footer overlaps the cover so it reads as glass above the artwork, not a stacked card.
        var footer = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassFooterRadius },
            Margin = AppLayout.GlassCardFooterMargin,
            Padding = new Thickness(10, 8),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingExtraSmall,
                Children =
                {
                    new Label
                    {
                        FontAttributes = AppFonts.Bold,
                        FontSize = AppFontSizes.FloatingPlayerTitle,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines = 2
                    }
                    .Bind(Label.TextProperty, nameof(WorkMetadata.Title))
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                    new Label { FontSize = AppFontSizes.Caption }
                        .Bind(Label.TextProperty, nameof(WorkMetadata.TranslatedTitle))
                        .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.TranslatedTitle), converter: new EmptyStringToFalseConverter())
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                    new Label { FontSize = AppFontSizes.Caption, LineBreakMode = LineBreakMode.TailTruncation }
                        .Bind(Label.TextProperty, nameof(WorkMetadata.VA))
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                }
            }
        }.Glass(strong: true);

        // Otherwise a work whose metadata couldn't be fetched looks identical to a complete one.
        var pendingBadge = new Border
        {
            BackgroundColor = AppColors.WarningDark,
            Padding = new Thickness(6, 2),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(6),
            Content = new Label
            {
                Text = "METADATA PENDING",
                FontSize = AppFontSizes.ButtonSmall,
                FontAttributes = AppFonts.Bold,
                TextColor = AppColors.BackgroundDark
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.NeedsMetadataRefresh));

        Content = new Grid
        {
            RowDefinitions = Rows.Define(AppLayout.GlassCardCoverHeight, Auto),
            Children =
            {
                cover.Row(0),
                pendingBadge.Row(0),
                footer.Row(1)
            }
        };

        // No resting shadow on virtualised grid items for performance; only the hovered card gets one.
        this.Glass(elevated: false);
        ToolTipProperties.SetText(this, string.Empty);
        SetBinding(ToolTipProperties.TextProperty, new Binding(nameof(WorkMetadata.Title)));

        // The card itself has no readable text, so give screen readers the title.
        SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(WorkMetadata.Title)));
        SemanticProperties.SetHint(this, "Double tap to open the album");

        var singleTap = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
        singleTap.Tapped += (sender, _) =>
        {
            if ((sender as BindableObject)?.BindingContext is not WorkMetadata work)
                return;

            if (isWide())
                viewModel.SelectWorkCommand.Execute(work);
            else
                viewModel.OpenAlbumCommand.Execute(work);
        };

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += (sender, _) =>
        {
            if ((sender as BindableObject)?.BindingContext is WorkMetadata work)
                viewModel.OpenAlbumCommand.Execute(work);
        };

        GestureRecognizers.Add(singleTap);
        GestureRecognizers.Add(doubleTap);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => ApplyHover(true);
        pointer.PointerExited += (_, _) => ApplyHover(false);
        GestureRecognizers.Add(pointer);

        FlyoutBase.SetContextFlyout(this, BuildContextMenu(viewModel));
    }

    private void ApplyHover(bool isHovered)
    {
        // ClearValue because VisualElement.Shadow is not nullable.
        if (isHovered)
            Shadow = _hoverShadow;
        else
            ClearValue(VisualElement.ShadowProperty);

        _ = this.ScaleTo(isHovered ? 1.012 : 1.0, 90, Easing.CubicOut);
    }

    private MenuFlyout BuildContextMenu(MainViewModel viewModel)
    {
        var menu = new MenuFlyout();

        var open = new MenuFlyoutItem { Text = "Open Album" };
        open.Clicked += (_, _) =>
        {
            if (BindingContext is WorkMetadata work)
                viewModel.OpenAlbumCommand.Execute(work);
        };

        var copyTitle = new MenuFlyoutItem { Text = "Copy Title" };
        copyTitle.Clicked += async (_, _) =>
        {
            if (BindingContext is WorkMetadata work)
                await Clipboard.Default.SetTextAsync(work.Title);
        };

        var copyPath = new MenuFlyoutItem { Text = "Copy Folder Path" };
        copyPath.Clicked += async (_, _) =>
        {
            if (BindingContext is WorkMetadata work && !string.IsNullOrEmpty(work.FolderPath))
                await Clipboard.Default.SetTextAsync(work.FolderPath);
        };

        menu.Add(open);
        menu.Add(copyTitle);
        menu.Add(copyPath);
        return menu;
    }
}
