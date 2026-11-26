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
    private readonly ObservableCollection<TrackGroup> _groups = new();

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

        var tracksPanel = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassSheetRadius },
            Padding = new Thickness(18),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingSection,
                Children =
                {
                    GlassTheme.SectionLabel("TRACKS"),

                    // Grouped CollectionView: sub-albums are groups, tracks are their items. A grouped view
                    // virtualises, where the nested BindableLayouts this replaces materialised every track in the
                    // album at once (PERF-07).
                    new CollectionView
                    {
                        IsGrouped = true,
                        ItemsSource = _groups,
                        SelectionMode = SelectionMode.None,
                        GroupHeaderTemplate = new DataTemplate(() =>
                        {
                            var title = new Label
                            {
                                FontSize = AppFontSizes.PathLabel,
                                FontAttributes = AppFonts.Bold,
                                VerticalOptions = LayoutOptions.Center
                            }
                                .Column(0)
                                .Bind(Label.TextProperty, nameof(TrackGroup.Title))
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

                            var arrow = new Label { FontSize = AppFontSizes.PathLabel, VerticalOptions = LayoutOptions.Center }
                                .Column(1)
                                .Bind(Label.TextProperty, nameof(TrackGroup.IsExpanded), converter: BoolToArrowConverter.Instance)
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

                            var header = new Grid
                            {
                                Padding = AppLayout.PathHeaderPadding,
                                ColumnDefinitions = Columns.Define(Star, Auto),
                                Children = { title, arrow }
                            }
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);

                            var tap = new TapGestureRecognizer();
                            tap.Tapped += (sender, _) =>
                            {
                                if ((sender as BindableObject)?.BindingContext is TrackGroup group)
                                    group.SetExpanded(!group.IsExpanded);
                            };
                            header.GestureRecognizers.Add(tap);

                            // The synthetic container folder is not a real sub-album, so its header is hidden
                            // while its tracks still show.
                            header.Bind(VisualElement.IsVisibleProperty, nameof(TrackGroup.Title), converter: NotMainTitleConverter.Instance);

                            return header;
                        }),
                        ItemTemplate = new DataTemplate(() =>
                        {
                            var trackTap = new TapGestureRecognizer();
                            trackTap.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(AlbumViewModel.PlayTrackCommand), source: BindingContext));
                            trackTap.SetBinding(TapGestureRecognizer.CommandParameterProperty, ".");

                            var playGlyph = new Label { Text = "\u25B6", FontSize = AppFontSizes.ButtonLarge, VerticalOptions = LayoutOptions.Center }
                                .Column(0)
                                .AppThemeBinding(Label.TextColorProperty, AppColors.SuccessLight, AppColors.SuccessDark);

                            var titleLabel = new Label { FontSize = AppFontSizes.ButtonLarge, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
                                .Bind(Label.TextProperty, nameof(Track.Title))
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

                            var translatedLabel = new Label { FontSize = AppFontSizes.Caption }
                                .Bind(Label.TextProperty, nameof(Track.TranslatedTitle))
                                .Bind(VisualElement.IsVisibleProperty, nameof(Track.TranslatedTitle), converter: new EmptyStringToFalseConverter())
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

                            // The row is a button in behaviour but announced as loose text (UX-07).
                            row.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(Track.Title)));
                            SemanticProperties.SetHint(row, "Double tap to play this track");

                            var pointer = new PointerGestureRecognizer();
                            pointer.PointerEntered += (_, _) => row.Opacity = 0.82;
                            pointer.PointerExited += (_, _) => row.Opacity = 1;
                            row.GestureRecognizers.Add(pointer);
                            row.GestureRecognizers.Add(trackTap);

                            return row;
                        })
                    }
                }
            }
        }.Glass(strong: true, elevated: true);

        var metaColumn = new VerticalStackLayout
        {
            Spacing = AppLayout.GlassSectionGap,
            VerticalOptions = LayoutOptions.Start,
            Children = { hero, metaSheet }
        };

        // One grid reflows: stacked in compact, two panes above the breakpoint.
        var contentGrid = new Grid();
        contentGrid.Children.Add(metaColumn);
        contentGrid.Children.Add(tracksPanel);

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = AppLayout.GlassContentPadding,
                Children = { contentGrid }
            }
        };

        var scaffold = new GlassScaffold("Album", back: true);
        scaffold.SetBody(new Grid { Children = { scroll, loading } });
        scaffold.AddOverlay(new FloatingPlayerBar(audioService, cacheService));

        scaffold.LayoutModeChanged += (_, wide) => ApplyAlbumLayout(contentGrid, metaColumn, tracksPanel, wide);

        Content = scaffold;

        // The grouped list follows the view model: Album is set by query navigation and re-raised after a
        // translation, so the groups are rebuilt on every change (PERF-07).
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        RebuildGroups(viewModel);
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AlbumViewModel.Album) && sender is AlbumViewModel viewModel)
            RebuildGroups(viewModel);
    }

    /// <summary>Rebuilds the group list from the album's sub-albums, preserving each one's expanded state.</summary>
    private void RebuildGroups(AlbumViewModel viewModel)
    {
        _groups.Clear();

        if (viewModel.Album?.SubAlbums == null) return;

        foreach (var subAlbum in viewModel.Album.SubAlbums)
            _groups.Add(new TrackGroup(subAlbum));
    }

    protected override void OnDisappearing()
    {
        // The view model is transient per navigation, but the subscription would keep this page alive if it were not
        // removed - the same leak that the main page had to fix.
        if (BindingContext is AlbumViewModel viewModel)
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnDisappearing();
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

    private static void ApplyAlbumLayout(Grid grid, View metaColumn, View tracksPanel, bool wide)
    {
        grid.RowDefinitions.Clear();
        grid.ColumnDefinitions.Clear();

        if (wide)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(AppBreakpoints.AlbumMetaWidth)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnSpacing = AppLayout.GlassSectionGap;

            Grid.SetRow(metaColumn, 0);
            Grid.SetColumn(metaColumn, 0);
            Grid.SetRow(tracksPanel, 0);
            Grid.SetColumn(tracksPanel, 1);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnSpacing = 0;

            Grid.SetRow(metaColumn, 0);
            Grid.SetColumn(metaColumn, 0);
            Grid.SetRow(tracksPanel, 1);
            Grid.SetColumn(tracksPanel, 0);
        }
    }
}
