using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
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
    public AlbumCardView(object parentBindingContext)
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
                    .Bind(Image.SourceProperty, nameof(WorkMetadata.CoverImagePath))
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

        // Grid items live in a virtualised CollectionView: no Shadow here, the stroke and the
        // translucent fill carry the depth instead, which keeps scrolling cheap on Android.
        this.Glass(elevated: false);

        var tapGesture = new TapGestureRecognizer();
        tapGesture.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(MainViewModel.OpenAlbumCommand), source: parentBindingContext));
        tapGesture.SetBinding(TapGestureRecognizer.CommandParameterProperty, ".");
        GestureRecognizers.Add(tapGesture);
    }
}
