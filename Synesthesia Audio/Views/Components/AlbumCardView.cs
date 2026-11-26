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

        var cover = new Grid
        {
            HeightRequest = AppLayout.GlassCardCoverHeight,
            Children =
            {
                new Grid
                {
                    BackgroundColor = AppColors.CoverPlaceholder,
                    Children =
                    {
                        new Label
                        {
                            Text = "NO COVER",
                            FontSize = AppFontSizes.SmallText,
                            FontAttributes = AppFonts.Bold,
                            TextColor = AppColors.CoverPlaceholderText,
                            HorizontalOptions = LayoutOptions.Center,
                            VerticalOptions = LayoutOptions.Center
                        }
                    }
                }
                .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.CoverImagePath), converter: new EmptyStringToTrueConverter()),

                new Image { Aspect = Aspect.AspectFill, HeightRequest = AppLayout.GlassCardCoverHeight }
                    .Bind(Image.SourceProperty, nameof(WorkMetadata.CoverImagePath),
                        converter: new CoverThumbnailConverter { Edge = Synesthesia_Audio.Core.CoverThumbnails.CardEdge })
                    .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.CoverImagePath), converter: new EmptyStringToFalseConverter())
            }
        };

        // The footer rides up over the cover; the overlap is what makes the panel read as glass
        // sitting above the artwork rather than as a plain stacked card.
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

        Content = new Grid
        {
            RowDefinitions = Rows.Define(AppLayout.GlassCardCoverHeight, Auto),
            Children =
            {
                cover.Row(0),
                footer.Row(1)
            }
        };

        // Grid items live in a virtualised CollectionView: no ambient shadow at rest, the stroke and
        // translucent fill carry the depth. A hover shadow is added only for the pointed-at card.
        this.Glass(elevated: false);
        ToolTipProperties.SetText(this, string.Empty);
        SetBinding(ToolTipProperties.TextProperty, new Binding(nameof(WorkMetadata.Title)));

        // The card is a tappable surface with no readable text of its own, so a screen reader had nothing to
        // announce (UX-07). The title is already bound for the tooltip; bind it as the description too.
        SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(WorkMetadata.Title)));
        SemanticProperties.SetHint(this, "Double tap to open the album");

        // Single tap previews on desktop and opens on compact; a double tap always opens.
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
        // ClearValue rather than assigning null: VisualElement.Shadow is not nullable.
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
