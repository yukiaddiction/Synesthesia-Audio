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
        // Primary, not SuccessLight: that was 2.8:1 on white, under the 3:1 contrast floor for UI components.
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

        // Cover and metadata live in the list header so the page is one virtualised scroll; in a row above the
        // list they could take the whole phone screen and leave the tracks no height.
        var header = new VerticalStackLayout
        {
            Spacing = AppLayout.GlassSectionGap,
            Padding = new Thickness(0, 6, 0, 0),
            Children =
            {
                hero,
                metaSheet,
                GlassTheme.SectionLabel("TRACKS").Margin(new Thickness(2, 4, 2, 0))
            }
        };

        // Folder headers are flat rows rather than CollectionView groups, which rendered only one group on Android.
        var trackList = new CollectionView
        {
            ItemsSource = _rows,
            SelectionMode = SelectionMode.None,
            ItemTemplate = BuildRowSelector(),
            Header = header,
            Footer = new BoxView { HeightRequest = 130, Color = Colors.Transparent },
            Margin = new Thickness(16, 0)
        };

        var body = trackList;

        var scaffold = new GlassScaffold("Album", back: true);
        scaffold.SetBody(new Grid { Children = { body, loading } });
        scaffold.AddOverlay(new FloatingPlayerBar(audioService, cacheService));

        Content = scaffold;

    }

    private DataTemplate BuildRowSelector() => new RowTemplateSelector
    {
        OnToggle = ToggleSection,
        SectionVisible = () => true,
        PlayCommand = BindingContext
    };

    private sealed class RowTemplateSelector : DataTemplateSelector
    {
        public Action<AlbumRow>? OnToggle { get; init; }
        public Func<bool>? SectionVisible { get; init; }
        public object? PlayCommand { get; init; }

        private DataTemplate? _header;
        private DataTemplate? _track;

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            _header ??= new DataTemplate(() => AlbumRowViews.Header(OnToggle!, SectionVisible ?? (() => true)));
            _track ??= new DataTemplate(() => AlbumRowViews.Track(PlayCommand));

            return (item as AlbumRow)?.IsHeader == true ? _header : _track;
        }
    }

    private void RebuildRows(AlbumViewModel viewModel)
    {
        _rows.Clear();

        if (viewModel.Album?.SubAlbums == null) return;

        foreach (var subAlbum in viewModel.Album.SubAlbums)
        {
            // The main folder (files directly in the work) gets a header too, so it can be collapsed like the rest.
            _rows.Add(AlbumRow.ForHeader(subAlbum));

            if (!subAlbum.IsExpanded) continue;

            foreach (var track in subAlbum.Tracks)
                _rows.Add(AlbumRow.ForTrack(track));
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AlbumViewModel.Album) && sender is AlbumViewModel viewModel)
            RebuildRows(viewModel);
    }

    /// <summary>
    /// Builds the rows immediately as well as on change: the album usually arrives through the query property
    /// before the page appears, so waiting for a change alone leaves the list empty.
    /// </summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is AlbumViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
            RebuildRows(viewModel);
        }
    }

    protected override void OnDisappearing()
    {
        if (BindingContext is AlbumViewModel viewModel)
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        base.OnDisappearing();
    }

    private void ToggleSection(AlbumRow row)
    {
        if (row.SubAlbum == null || BindingContext is not AlbumViewModel viewModel) return;

        row.SubAlbum.IsExpanded = !row.SubAlbum.IsExpanded;
        RebuildRows(viewModel);
    }
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

        // Darkens the bottom so the overlapping metadata sheet reads as depth, not as a seam.
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
