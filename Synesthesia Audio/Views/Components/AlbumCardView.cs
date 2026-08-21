using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;

namespace Synesthesia_Audio.Views.Components;

public class AlbumCardView : Border
{
    public AlbumCardView(object parentBindingContext)
    {
        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.CardCornerRadius };
        Padding = 0;
        Margin = AppLayout.WorkCardMargin;

        Content = new Grid
        {
            RowDefinitions = GridRowsColumns.Rows.Define(AppLayout.CoverGridHeight, GridRowsColumns.Auto, GridRowsColumns.Auto),
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
                .Row(0)
                .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.CoverImagePath), converter: new EmptyStringToTrueConverter()),

                new Image { Aspect = Aspect.AspectFill, HeightRequest = AppLayout.CoverGridHeight }
                    .Row(0)
                    .Bind(Image.SourceProperty, nameof(WorkMetadata.CoverImagePath))
                    .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.CoverImagePath), converter: new EmptyStringToFalseConverter()),

                new VerticalStackLayout
                {
                    Margin = AppLayout.TextStackMargin,
                    Spacing = AppLayout.SpacingExtraSmall,
                    Children =
                    {
                        new Label { FontAttributes = AppFonts.Bold, FontSize = AppFontSizes.FloatingPlayerTitle, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2 }
                            .Bind(Label.TextProperty, nameof(WorkMetadata.Title))
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                        new Label { FontSize = AppFontSizes.Caption }
                            .Bind(Label.TextProperty, nameof(WorkMetadata.TranslatedTitle))
                            .Bind(VisualElement.IsVisibleProperty, nameof(WorkMetadata.TranslatedTitle), converter: new EmptyStringToFalseConverter())
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                    }
                }
                .Row(1),

                new Label { FontSize = AppFontSizes.Caption, Margin = AppLayout.TextCircleMargin, LineBreakMode = LineBreakMode.TailTruncation }
                    .Row(2)
                    .Bind(Label.TextProperty, nameof(WorkMetadata.VA))
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
            }
        };

        this.AppThemeBinding(Border.BackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceDark);
        this.AppThemeBinding(Border.StrokeProperty, AppColors.StrokeLight, AppColors.StrokeDark);

        var tapGesture = new TapGestureRecognizer();
        tapGesture.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(MainViewModel.OpenAlbumCommand), source: parentBindingContext));
        tapGesture.SetBinding(TapGestureRecognizer.CommandParameterProperty, ".");
        GestureRecognizers.Add(tapGesture);
    }
}
