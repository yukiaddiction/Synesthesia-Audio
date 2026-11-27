using CommunityToolkit.Maui.Markup;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// The two row views of the album's virtualised list: a folder header and a track.
///
/// They live here rather than inside the page so both templates are plain factories the DataTemplateSelector can
/// hand to the CollectionView, which materialises a row only when it scrolls into view (PERF-07).
/// </summary>
public static class AlbumRowViews
{
    /// <summary>
    /// A folder header: title, track count and a chevron. Tapping it folds the folder, which the page implements by
    /// rebuilding the row list.
    /// </summary>
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

    /// <summary>A track row: play glyph, title, and the translated title when there is one.</summary>
    public static View Track(object? playCommand)
    {
        var trackTap = new TapGestureRecognizer();
        trackTap.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(AlbumViewModel.PlayTrackCommand), source: playCommand));
        trackTap.SetBinding(TapGestureRecognizer.CommandParameterProperty, nameof(AlbumRow.Track));

        var playGlyph = new Label { Text = "\u25B6", FontSize = AppFontSizes.ButtonLarge, VerticalOptions = LayoutOptions.Center }
            .Column(0)
            .AppThemeBinding(Label.TextColorProperty, AppColors.SuccessLight, AppColors.SuccessDark);

        var titleLabel = new Label { FontSize = AppFontSizes.ButtonLarge, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
            .Bind(Label.TextProperty, nameof(AlbumRow.Title))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var translatedLabel = new Label { FontSize = AppFontSizes.Caption }
            .Bind(Label.TextProperty, nameof(AlbumRow.TranslatedTitle))
            .Bind(VisualElement.IsVisibleProperty, nameof(AlbumRow.HasTranslation))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        var row = new Grid
        {
            Padding = new Thickness(10, 12),
            ColumnDefinitions = Columns.Define(Auto, Star),
            ColumnSpacing = 12,
            Children =
            {
                playGlyph,
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    VerticalOptions = LayoutOptions.Center,
                    Children = { titleLabel, translatedLabel }
                }.Column(1)
            }
        };

        row.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(AlbumRow.Title)));
        SemanticProperties.SetHint(row, "Double tap to play this track");

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) => row.Opacity = 0.82;
        pointer.PointerExited += (_, _) => row.Opacity = 1;
        row.GestureRecognizers.Add(pointer);
        row.GestureRecognizers.Add(trackTap);

        return row;
    }
}
