using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Views.Components;

namespace Synesthesia_Audio.Views;

public class AlbumPage : ContentPage
{
    private readonly LibraryCacheService _cacheService;
    private readonly ObservableCollection<AlbumRow> _rows = new();

    public AlbumPage(AlbumViewModel viewModel, AudioService audioService, LibraryCacheService cacheService)
    {
        _cacheService = cacheService;
        BindingContext = viewModel;
        Shell.SetNavBarIsVisible(this, false);

        var loading = new ActivityIndicator
        {
            IsRunning = true,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HeightRequest = 40,
            WidthRequest = 40
        }
        // SuccessLight is 2.8:1 on white, below the 3:1 floor for a UI component, and never changed for the
        // dark theme (UX-01).
        .AppThemeBinding(ActivityIndicator.ColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
        .Bind(VisualElement.IsVisibleProperty, nameof(AlbumViewModel.Album), converter: new NotNullToFalseConverter());

        var hero = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius },
            StrokeThickness = 0,
            HeightRequest = AppLayout.GlassHeroHeight,
            Padding = 0,
            Content = BuildHeroCover()
        };

        var chips = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingLarge,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                GlassTheme.Chip(new Label { FontSize = AppFontSizes.Caption }
                    .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.Circle)}", stringFormat: "Circle \u00B7 {0}")
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark))
                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.Circle)}", converter: new EmptyStringToFalseConverter()),

                GlassTheme.Chip(new Label { FontSize = AppFontSizes.Caption }
                    .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.VA)}", stringFormat: "Cast \u00B7 {0}")
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark))
                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.VA)}", converter: new EmptyStringToFalseConverter())
            }
        };

        var editButton = GlassTheme.PillButton("Edit Work Info", AppFontSizes.Body, height: 34)
            .Bind(Button.CommandProperty, nameof(AlbumViewModel.OpenEditDialogCommand));
        ToolTipProperties.SetText(editButton, "Edit this work's metadata");

        var translateButton = GlassTheme.PillButton("Translate Info", AppFontSizes.Body, height: 34)
            .Bind(Button.CommandProperty, nameof(AlbumViewModel.TranslateCommand));
        ToolTipProperties.SetText(translateButton, "Translate metadata to English");

        var actions = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingExtraLarge,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new ActivityIndicator { HeightRequest = 20, WidthRequest = 20, IsVisible = false }
            .AppThemeBinding(ActivityIndicator.ColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
                    .Bind(ActivityIndicator.IsRunningProperty, nameof(AlbumViewModel.IsBusy))
                    .Bind(VisualElement.IsVisibleProperty, nameof(AlbumViewModel.IsBusy)),
                editButton,
                translateButton
            }
        };

        var metaSheet = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassSheetRadius },
            Margin = AppLayout.GlassSheetOverlap,
            Padding = new Thickness(18),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingSection,
                Children =
                {
                    new VerticalStackLayout
                    {
                        Spacing = AppLayout.SpacingSmall,
                        Children =
                        {
                            new Label { FontSize = AppFontSizes.PageTitle, FontAttributes = AppFonts.Bold, HorizontalTextAlignment = TextAlignment.Center }
                                .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.Title)}")
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                            new Label { FontSize = AppFontSizes.Subtitle, HorizontalTextAlignment = TextAlignment.Center }
                                .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.TranslatedTitle)}")
                                .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.TranslatedTitle)}", converter: new EmptyStringToFalseConverter())
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                        }
                    },

                    chips,
                    actions,

                    new FlexLayout
                    {
                        Wrap = FlexWrap.Wrap,
                        JustifyContent = FlexJustify.Center,
                        AlignItems = FlexAlignItems.Center,
                        AlignContent = FlexAlignContent.Center,
                        Direction = FlexDirection.Row
                    }
                    .Bind(BindableLayout.ItemsSourceProperty, nameof(AlbumViewModel.GenreTags))
                    .ItemTemplate(new DataTemplate(() =>
                    {
                        return GlassTheme.Chip(new Label { FontSize = AppFontSizes.Caption }
                            .Bind(Label.TextProperty, ".")
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark))
                            .Margin(new Thickness(3));
                    }))
                }
            }
        }.Glass(strong: true, elevated: true);

                // One scrolling surface: the metadata is the list header and the grouped CollectionView owns the
        // scrolling. Nothing is nested inside a ScrollView, which is what made the previous attempt render a
        // single group and refuse to scroll on Android (PERF-07).
        var metaHeader = new VerticalStackLayout
        {
            Spacing = AppLayout.GlassSectionGap,
            Padding = AppLayout.GlassContentPadding,
            VerticalOptions = LayoutOptions.Start,
            Children =
            {
                hero,
                metaSheet,
                GlassTheme.SectionLabel("TRACKS")
            }
        };

        var tracksView = BuildTracksView(viewModel);
        tracksView.Header = metaHeader;

        var scaffold = new GlassScaffold("Album", back: true);
        scaffold.SetBody(new Grid { Children = { tracksView, loading } });
        scaffold.AddOverlay(new FloatingPlayerBar(audioService, cacheService));

        Content = scaffold;

        // The list follows the view model: Album is set by query navigation and raised again after a
        // translation, so the groups are rebuilt on every change.
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RebuildRows(viewModel);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AlbumViewModel.Album) && sender is AlbumViewModel viewModel)
            RebuildRows(viewModel);
    }

    private void RebuildRows(AlbumViewModel viewModel)
    {
        _rows.Clear();

        if (viewModel.Album?.SubAlbums == null) return;

        foreach (var subAlbum in viewModel.Album.SubAlbums)
        {
            // A folder whose title is the work itself has nothing to label, so it gets no header row at all
            // rather than an invisible one that still takes up space in the list.
            if (!string.Equals(subAlbum.Title, AlbumRow.MainGroupName, StringComparison.OrdinalIgnoreCase))
                _rows.Add(AlbumRow.ForHeader(subAlbum));

            if (!subAlbum.IsExpanded) continue;

            foreach (var track in subAlbum.Tracks)
                _rows.Add(AlbumRow.ForTrack(track));
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is AlbumViewModel viewModel)
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnDisappearing();
    }

    /// <summary>
    /// The grouped track list. Sub-albums are the groups, tracks their items, so only the rows on screen are
    /// materialised (PERF-07). Collapsing a group empties its observable collection, which the view follows.
    /// </summary>
    private CollectionView BuildTracksView(AlbumViewModel viewModel)
    {
        return new CollectionView
        {
            // Flat rows, not groups: grouping dropped the first track and left a blank row on Android.
            IsGrouped = false,
            ItemsSource = _rows,
            SelectionMode = SelectionMode.None,

            // The floating player bar overlays the bottom of the page, so the list ends above it rather than
            // hiding the last tracks behind it.
            Footer = new BoxView { HeightRequest = 96, Color = Colors.Transparent },

            ItemTemplate = BuildRowSelector(),
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical) { ItemSpacing = 0 }
        };
    }

    /// <summary>
    /// Two templates: one for a sub-album header, one for a track. A selector keeps the header and the track
    /// separate without grouping, which is what broke the previous attempt.
    /// </summary>
    private DataTemplate BuildRowSelector()
    {
        var header = new DataTemplate(BuildHeaderRow);
        var track = new DataTemplate(BuildTrackRow);

        return new DataTemplateSelector
        {
            Header = header,
            Track = track
        };
    }

    private sealed class DataTemplateSelector : Microsoft.Maui.Controls.DataTemplateSelector
    {
        public DataTemplate? Header { get; init; }
        public DataTemplate? Track { get; init; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container) =>
            (item as AlbumRow)?.IsHeader == true ? Header! : Track!;
    }

    private View BuildHeaderRow()
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

        var arrow = new Label { FontSize = AppFontSizes.PathLabel, VerticalOptions = LayoutOptions.Center }
            .Column(1)
            .Bind(Label.TextProperty, nameof(AlbumRow.Arrow))
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var header = new Grid
        {
            Padding = AppLayout.PathHeaderPadding,
            ColumnDefinitions = Columns.Define(Star, Auto),
            Children = { title, arrow }
        }
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);

        // A work whose tracks sit directly in the work folder gets a header with nothing to say.
        header.Bind(VisualElement.IsVisibleProperty, nameof(AlbumRow.IsHeaderVisible));

        var tap = new TapGestureRecognizer();
        tap.Tapped += (sender, _) =>
        {
            if ((sender as BindableObject)?.BindingContext is AlbumRow row && row.SubAlbum != null
                && BindingContext is AlbumViewModel viewModel)
            {
                row.SubAlbum.IsExpanded = !row.SubAlbum.IsExpanded;
                RebuildRows(viewModel);
            }
        };
        header.GestureRecognizers.Add(tap);

        return header;
    }

    private View BuildTrackRow()
    {
        var trackTap = new TapGestureRecognizer();
        trackTap.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(AlbumViewModel.PlayTrackCommand), source: BindingContext));
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


    /// <summary>
    /// The hero artwork: the shared cover component plus the two things only the hero has - the scrim that
    /// makes the sheet above it read as depth, and the product-id badge.
    /// </summary>
    private static Grid BuildHeroCover()
    {
        var pathBinding = $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.CoverImagePath)}";

        var cover = CoverImageView.Create(
            pathBinding,
            Synesthesia_Audio.Core.CoverThumbnails.CardEdge,
            placeholderContent: new Label
            {
                Text = "\uD83C\uDFB5",
                FontSize = AppFontSizes.CoverPlaceholderIcon,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            });

        // Scrim so the overlapping sheet above the artwork reads as depth, not as a seam.
        cover.Children.Add(new Border
        {
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.End,
            HeightRequest = 130,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb("#00000000"), 0f),
                    new GradientStop(Color.FromArgb("#59000000"), 1f)
                }
            }
        });

        cover.Children.Add(new Border
        {
            BackgroundColor = AppColors.RjBadge,
            Padding = new Thickness(8, 4),
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(12),
            Content = new Label { TextColor = Colors.White, FontSize = AppFontSizes.Caption, FontAttributes = AppFonts.Bold }
                .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.ProductId)}")
        }
        .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.IsDlsiteWork)}"));

        return cover;
    }

}
