using CommunityToolkit.Maui.Markup;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>Plain row factories the album page's DataTemplateSelector hands to its virtualised list.</summary>
public static class AlbumRowViews
{
    public static View Header(Action<AlbumRow> onToggle, Func<bool> sectionVisible)
    {
        var title = new Label
        {
            FontSize = AppFontSizes.PathLabel,
            FontAttributes = AppFonts.Bold,
            VerticalOptions = LayoutOptions.Center
        }
            .Column(0)
            .Bind(Label.TextProperty, nameof(AlbumRow.Title))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var count = new Label { FontSize = AppFontSizes.Caption, VerticalOptions = LayoutOptions.Center }
            .Column(1)
            .Bind(Label.TextProperty, nameof(AlbumRow.TrackCount))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        var arrow = new Label
        {
            FontSize = AppFontSizes.PathLabel,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 0, 0)
        }
            .Column(2)
            .Bind(Label.TextProperty, nameof(AlbumRow.Arrow))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var header = new Grid
        {
            Padding = AppLayout.PathHeaderPadding,
            ColumnDefinitions = Columns.Define(Star, Auto, Auto),
            ColumnSpacing = 8,
            Children = { title, count, arrow }
        }
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);

        var tap = new TapGestureRecognizer();
        tap.Tapped += (sender, _) =>
        {
            if ((sender as BindableObject)?.BindingContext is AlbumRow row)
                onToggle(row);
        };
        header.GestureRecognizers.Add(tap);

        SemanticProperties.SetHint(header, "Double tap to open or close this folder");

        return header;
    }

    public static View Track(object? playCommand)
    {
        var trackTap = new TapGestureRecognizer();
        trackTap.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(AlbumViewModel.PlayTrackCommand), source: playCommand));
        trackTap.SetBinding(TapGestureRecognizer.CommandParameterProperty, nameof(AlbumRow.Track));

        // A numbered badge reads as a chapter in a voice drama, not as a file in a list.
        var number = new Label
        {
            FontSize = AppFontSizes.Overline,
            FontAttributes = AppFonts.Bold,
            CharacterSpacing = 0.6,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        }
        .Bind(Label.TextProperty, nameof(AlbumRow.SceneNumber))
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        var numberBadge = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = AppLayout.GlassPillRadius },
            StrokeThickness = 0,
            WidthRequest = 34,
            HeightRequest = 34,
            VerticalOptions = LayoutOptions.Center,
            Content = number
        }.GlassSoft();

        var titleLabel = new Label { FontSize = AppFontSizes.ButtonLarge, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
            .Bind(Label.TextProperty, nameof(AlbumRow.Title))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var translatedLabel = new Label { FontSize = AppFontSizes.Caption }
            .Bind(Label.TextProperty, nameof(AlbumRow.TranslatedTitle))
            .Bind(VisualElement.IsVisibleProperty, nameof(AlbumRow.HasTranslation))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        var playGlyph = new Label { Text = "\u25B7", FontSize = AppFontSizes.ButtonLarge, VerticalOptions = LayoutOptions.Center }
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark);

        var row = new Grid
        {
            Padding = new Thickness(10, 9),
            ColumnDefinitions = Columns.Define(Auto, Star, Auto),
            ColumnSpacing = 12,
            Children =
            {
                numberBadge.Column(0),
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { titleLabel, translatedLabel }
                }.Column(1),
                playGlyph.Column(2)
            }
        };

        row.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(AlbumRow.Title)));
        SemanticProperties.SetHint(row, "Double tap to play this scene");

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => row.Opacity = 0.82;
        pointer.PointerExited += (_, _) => row.Opacity = 1;
        row.GestureRecognizers.Add(pointer);
        row.GestureRecognizers.Add(trackTap);

        return row;
    }
}
