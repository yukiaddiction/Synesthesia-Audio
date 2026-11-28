using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// A work on the shelf, drawn as a jacket: artwork on top, then a near-opaque glass label carrying the title
/// and credits. The label is deliberately not translucent over the cover, so bright artwork cannot erase the text.
/// </summary>
public class AlbumCardView : Border
{
    private readonly Shadow _hoverShadow = GlassTheme.Elevation(6, 18, 0.28f);

    public AlbumCardView(MainViewModel viewModel, Func<bool> isWide, AudioService audioService)
    {
        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius };
        Padding = 0;
        Margin = AppLayout.WorkCardMargin;

        var cover = CoverImageView.Card(nameof(WorkMetadata.CoverImagePath));

        var rating = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            StrokeThickness = 0,
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(10),
            IsVisible = false,
            Content = new Label
            {
                Text = "R-18",
                FontSize = AppFontSizes.Overline,
                FontAttributes = AppFonts.Bold,
                CharacterSpacing = 1.2,
                TextColor = Colors.White
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.IsNsfw))
        .Invoke(b => b.BackgroundColor = AppColors.RjBadge);

        var allAge = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            StrokeThickness = 0,
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(10),
            BackgroundColor = AppColors.CoverBadgeFill,
            IsVisible = false,
            Content = new Label
            {
                Text = "ALL AGE",
                FontSize = AppFontSizes.Overline,
                FontAttributes = AppFonts.Bold,
                CharacterSpacing = 1.2,
                TextColor = AppColors.CoverBadgeText
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.IsSfw));

        // The active work is marked on its own jacket, so "what am I in the middle of?" is answered by the shelf.
        var nowPlaying = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            StrokeThickness = 0,
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(10),
            BackgroundColor = AppColors.PrimaryLight,
            IsVisible = false,
            Content = new Label
            {
                Text = "\u25CF  NOW",
                FontSize = AppFontSizes.Overline,
                FontAttributes = AppFonts.Bold,
                CharacterSpacing = 1.0,
                TextColor = Colors.White
            }
        };
        nowPlaying.SetBinding(VisualElement.IsVisibleProperty, NowPlayingBinding(audioService));

        // Otherwise a work whose metadata couldn't be fetched looks identical to a complete one.
        var pendingBadge = new Border
        {
            BackgroundColor = AppColors.WarningDark,
            Padding = new Thickness(7, 2),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(10),
            IsVisible = false,
            Content = new Label
            {
                Text = "METADATA PENDING",
                FontSize = AppFontSizes.Overline,
                FontAttributes = AppFonts.Bold,
                CharacterSpacing = 0.8,
                TextColor = AppColors.BackgroundDark
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.NeedsMetadataRefresh));

        cover.Children.Add(rating);
        cover.Children.Add(allAge);
        cover.Children.Add(nowPlaying);
        cover.Children.Add(pendingBadge);

        // Credits are printed with their role, because a name alone doesn't say whether it's the cast or the circle.
        var castRow = CreditRow("CV", nameof(WorkMetadata.VA),
            AppColors.PrimaryLight, AppColors.PrimaryDark,
            AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);
        castRow.Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.VA), converter: new EmptyStringToFalseConverter());

        var circleRow = CreditRow("CIRCLE", nameof(WorkMetadata.Circle),
            AppColors.StrokeLight, AppColors.StrokeDark,
            AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);
        circleRow.Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.Circle), converter: new EmptyStringToFalseConverter());
        circleRow.Margin = new Thickness(0, 3, 0, 0);

        var title = new Label
        {
            FontAttributes = AppFonts.Bold,
            FontSize = AppFontSizes.BodyBold,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2,
            LineHeight = 1.2,
            MinimumHeightRequest = 36,
            VerticalTextAlignment = TextAlignment.Start
        }
        .Bind(Label.TextProperty, nameof(WorkMetadata.Title))
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var divider = new BoxView { HeightRequest = 1, Margin = new Thickness(0, 6) }
            .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark);

        var labelPlate = new Border
        {
            MinimumHeightRequest = AppLayout.GlassCardLabelHeight,
            Padding = new Thickness(12, 10),
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children = { title, divider, castRow, circleRow }
            }
        }.GlassLabel();

        // A slim accent runs down the card's left edge while this work is the one loaded in the player.
        var playingBar = new BoxView
        {
            WidthRequest = 3,
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Fill,
            Color = AppColors.PrimaryLight,
            IsVisible = false,
            InputTransparent = true
        };
        playingBar.SetBinding(VisualElement.IsVisibleProperty, NowPlayingBinding(audioService));

        var body = new Grid
        {
            RowDefinitions = Rows.Define(AppLayout.GlassCardCoverHeight, Auto),
            Children =
            {
                cover.Row(0),
                labelPlate.Row(1)
            }
        };

        Grid.SetRowSpan(playingBar, 2);
        body.Children.Add(playingBar);
        Content = body;

        // No resting shadow on virtualised grid items for performance; only the hovered card gets one.
        this.Glass(elevated: false);
        SetBinding(ToolTipProperties.TextProperty, new Binding(nameof(WorkMetadata.Title)));

        // The card itself has no readable text, so give screen readers the title.
        SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(WorkMetadata.Title)));
        SemanticProperties.SetHint(this, "Double tap to open the work");

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

    /// <summary>"CV · name" / "CIRCLE · name", with the role in a smaller tracked label and the value truncating.</summary>
    private static Grid CreditRow(string role, string valuePath, Color roleLight, Color roleDark, Color valueLight, Color valueDark)
    {
        var roleLabel = new Label
        {
            Text = role,
            FontSize = AppFontSizes.Overline,
            FontAttributes = AppFonts.Bold,
            CharacterSpacing = 1.4,
            VerticalOptions = LayoutOptions.Center
        }
        .Column(0)
        .AppThemeBinding(Label.TextColorProperty, roleLight, roleDark);

        var value = new Label
        {
            FontSize = AppFontSizes.DetailText,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            VerticalOptions = LayoutOptions.Center
        }
        .Column(1)
        .Bind(Label.TextProperty, valuePath)
        .AppThemeBinding(Label.TextColorProperty, valueLight, valueDark);

        return new Grid
        {
            ColumnDefinitions = Columns.Define(Auto, Star),
            ColumnSpacing = 7,
            Children = { roleLabel, value }
        };
    }

    /// <summary>Both the "NOW" pill and the left accent use this, so the two can never disagree.</summary>
    private static MultiBinding NowPlayingBinding(AudioService audioService) => new()
    {
        Bindings =
        {
            new Binding(nameof(AudioService.NowPlayingFolderPath), source: audioService),
            new Binding(nameof(WorkMetadata.FolderPath))
        },
        Converter = new FolderPathMatchConverter()
    };

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

        var open = new MenuFlyoutItem { Text = "Open Work" };
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
