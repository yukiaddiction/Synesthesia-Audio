using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.ViewModels;

using Synesthesia_Audio.Views.Components;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// Desktop-only jacket pane for the work highlighted in the shelf: artwork, then the credits a voice work is
/// judged by (cast, circle, genre), then the one action worth taking.
/// </summary>
public sealed class AlbumPreviewPane : Border
{
    private const double CoverHeight = 200;

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
                    Text = "\uD83C\uDFA7",
                    FontSize = AppFontSizes.CoverPlaceholderIcon,
                    HorizontalTextAlignment = TextAlignment.Center
                }.AppThemeBinding(Label.TextColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark),

                new Label
                {
                    Text = "Pick a work from the shelf",
                    FontSize = AppFontSizes.Body,
                    HorizontalTextAlignment = TextAlignment.Center
                }.AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                new Label
                {
                    Text = "Its jacket and cast appear here.",
                    FontSize = AppFontSizes.Caption,
                    HorizontalTextAlignment = TextAlignment.Center
                }.AppThemeBinding(Label.TextColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark)
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasSelectedWork), converter: new InvertedBoolConverter());

        var cover = new Grid
        {
            Children =
            {
                CoverImageView.Create(
                    $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.CoverImagePath)}",
                    Synesthesia_Audio.Core.CoverThumbnails.CardEdge,
                    CoverHeight,
                    placeholderContent: new Label
                    {
                        Text = "\uD83C\uDFB5",
                        FontSize = AppFontSizes.CoverPlaceholderIcon,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center
                    }),

                new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
                    StrokeThickness = 0,
                    Padding = new Thickness(8, 3),
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(10),
                    BackgroundColor = AppColors.RjBadge,
                    Content = new Label
                    {
                        Text = "R-18",
                        FontSize = AppFontSizes.Overline,
                        FontAttributes = AppFonts.Bold,
                        CharacterSpacing = 1.2,
                        TextColor = Colors.White
                    }
                }
                .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.IsNsfw)}")
            }
        };

        var coverFrame = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassFooterRadius },
            StrokeThickness = 0,
            HeightRequest = CoverHeight,
            Padding = 0,
            Content = cover
        };

        var title = new Label
        {
            FontSize = AppFontSizes.GroupTitle,
            FontAttributes = AppFonts.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        }
        .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Title)}")
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var translatedTitle = new Label { FontSize = AppFontSizes.DetailText }
            .Bind(Label.TextProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.TranslatedTitle)}")
            .Bind(VisualElement.IsVisibleProperty, $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.TranslatedTitle)}", converter: new EmptyStringToFalseConverter())
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        var cast = CreditRow("CAST", $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.VA)}");
        var circle = CreditRow("CIRCLE", $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Circle)}");
        var genre = CreditRow("GENRE", $"{nameof(MainViewModel.SelectedWork)}.{nameof(WorkMetadata.Genre)}");

        var openButton = GlassTheme.PillButton("Open work", AppFontSizes.Body, accent: true, height: AppLayout.ButtonHeightCompact)
            .Bind(Button.CommandProperty, nameof(MainViewModel.OpenAlbumCommand))
            .Bind(Button.CommandParameterProperty, nameof(MainViewModel.SelectedWork))
            .Invoke(b => b.HorizontalOptions = LayoutOptions.Fill);

        var details = new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingSmall,
            Children =
            {
                GlassTheme.SectionLabel("JACKET").Invoke(l => l.Margin = new Thickness(0, 0, 0, 2)),
                title,
                translatedTitle,
                new BoxView { HeightRequest = 1, Margin = new Thickness(0, 8) }
                    .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark),
                cast,
                circle,
                genre,
                openButton.Invoke(b => b.Margin = new Thickness(0, 10, 0, 0)),
                new Label
                {
                    Text = "\uD83C\uDFA7  Headphones recommended",
                    FontSize = AppFontSizes.Caption,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 0)
                }.AppThemeBinding(Label.TextColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark)
            }
        };

        var selectedContent = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingSection,
                Children = { coverFrame, details }
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasSelectedWork));

        Content = new Grid
        {
            Children = { emptyState, selectedContent }
        };

        this.Glass(strong: true, elevated: true);
    }

    private static Grid CreditRow(string role, string valuePath)
    {
        var roleLabel = new Label
        {
            Text = role,
            FontSize = AppFontSizes.Overline,
            FontAttributes = AppFonts.Bold,
            CharacterSpacing = 1.6,
            VerticalOptions = LayoutOptions.Start
        }
        .Column(0)
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark);

        var value = new Label
        {
            FontSize = AppFontSizes.Body,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        }
        .Column(1)
        .Bind(Label.TextProperty, valuePath)
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var row = new Grid
        {
            ColumnDefinitions = Columns.Define(Auto, Star),
            ColumnSpacing = 10,
            Margin = new Thickness(0, 2),
            Children = { roleLabel, value }
        };

        row.Bind(VisualElement.IsVisibleProperty, valuePath, converter: new EmptyStringToFalseConverter());
        return row;
    }
}
