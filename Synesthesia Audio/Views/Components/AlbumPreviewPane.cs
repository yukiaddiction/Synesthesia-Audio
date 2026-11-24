using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.ViewModels;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// Desktop master–detail pane: shows the album currently highlighted in the grid.
/// Hidden entirely in the compact layout.
/// </summary>
public sealed class AlbumPreviewPane : Border
{
    private const double CoverHeight = 190;

    public AlbumPreviewPane()
    {
        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius };
        Padding = new Thickness(16);

        var emptyState = new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingLarge,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(12, 60),
            Children =
            {
                new Label
                {
                    Text = "\u25A2",
                    FontSize = AppFontSizes.CoverPlaceholderIcon,
                    HorizontalTextAlignment = TextAlignment.Center
                }.AppThemeBinding(Label.TextColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark),

                new Label
                {
                    Text = "Select a work to preview",
                    FontSize = AppFontSizes.Body,
                    HorizontalTextAlignment = TextAlignment.Center
                }.AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasSelectedWork), converter: new InvertedBoolConverter());

        var cover = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassFooterRadius },
            StrokeThickness = 0,
            HeightRequest = CoverHeight,
            Padding = 0,
            Content = new Grid
            {
                Children =
                {
                    new Grid
                    {
                        BackgroundColor = AppColors.CoverPlaceholder,
                        Children =
                        {
                            new Label
                            {
                                Text = "\uD83C\uDFB5",
                                FontSize = AppFontSizes.CoverPlaceholderIcon,
                                HorizontalOptions = LayoutOptions.Center,
                                VerticalOptions = LayoutOptions.Center
                            }
                        }
                    }
                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.CoverImagePath)}", converter: new EmptyStringToTrueConverter()),

                    new Image { Aspect = Aspect.AspectFill }
                        .Bind(Image.SourceProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.CoverImagePath)}")
                        .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.CoverImagePath)}", converter: new EmptyStringToFalseConverter())
                }
            }
        };

        var chips = new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            Direction = FlexDirection.Row,
            JustifyContent = FlexJustify.Start,
            AlignItems = FlexAlignItems.Center
        };

        var details = new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                new Label
                {
                    FontSize = AppFontSizes.Subtitle,
                    FontAttributes = AppFonts.Bold,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 2
                }
                .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Title)}")
                .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                new Label { FontSize = AppFontSizes.Caption }
                    .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.TranslatedTitle)}")
                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.TranslatedTitle)}", converter: new EmptyStringToFalseConverter())
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                chips,

                new Label { FontSize = AppFontSizes.Caption, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 3 }
                    .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Genre)}")
                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Genre)}", converter: new EmptyStringToFalseConverter())
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                GlassTheme.PillButton("Open Album", AppFontSizes.Body, accent: true)
                    .Bind(Button.CommandProperty, nameof(MainViewModel.OpenAlbumCommand))
                    .Bind(Button.CommandParameterProperty, nameof(MainViewModel.SelectedWork))
                    .Invoke(b => b.HorizontalOptions = LayoutOptions.Start)
            }
        };

        chips.Children.Add(GlassTheme.Chip(new Label { FontSize = AppFontSizes.Caption }
            .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Circle)}")
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark))
            .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Circle)}", converter: new EmptyStringToFalseConverter()));

        chips.Children.Add(GlassTheme.Chip(new Label { FontSize = AppFontSizes.Caption }
            .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.VA)}")
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark))
            .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.VA)}", converter: new EmptyStringToFalseConverter()));

        chips.Children.Add(GlassTheme.Chip(new Label { FontSize = AppFontSizes.Caption }
            .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.AgeRating)}")
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark)));

        var selectedContent = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingSection,
                Children = { cover, details }
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasSelectedWork));

        Content = new Grid
        {
            Children = { emptyState, selectedContent }
        };

        this.Glass(strong: true, elevated: true);
    }
}
