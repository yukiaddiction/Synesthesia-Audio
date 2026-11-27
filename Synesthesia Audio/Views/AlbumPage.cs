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

                    new VerticalStackLayout
                    {
                        Spacing = AppLayout.SpacingSection,
                        HorizontalOptions = LayoutOptions.Fill
                    }
                    .Bind(BindableLayout.ItemsSourceProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.SubAlbums)}")
                    .ItemTemplate(new DataTemplate(() =>
                    {
                        var subAlbumLayout = new VerticalStackLayout { Spacing = AppLayout.SpacingExtraLarge };

                        var headerGrid = new Grid
                        {
                            Padding = AppLayout.PathHeaderPadding,
                            ColumnDefinitions = Columns.Define(Star, Auto),
                            Children =
                            {
                                new Label { FontSize = AppFontSizes.PathLabel, FontAttributes = AppFonts.Bold, VerticalOptions = LayoutOptions.Center }
                                    .Column(0)
                                    .Bind(Label.TextProperty, nameof(SubAlbum.Title))
                                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                                new Label { FontSize = AppFontSizes.PathLabel, VerticalOptions = LayoutOptions.Center }
                                    .Column(1)
                                    .Bind(Label.TextProperty, nameof(SubAlbum.IsExpanded), converter: BoolToArrowConverter.Instance)
                                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                            }
                        }
                        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);

                        var tracksLayout = new VerticalStackLayout
                        {
                            Spacing = AppLayout.SpacingMedium,
                            Padding = new Thickness(10, 5)
                        }
                        .Bind(BindableLayout.ItemsSourceProperty, nameof(SubAlbum.Tracks));

                        var headerTap = new TapGestureRecognizer();
                        headerTap.Tapped += async (_, _) =>
                        {
                            var subAlbum = (headerGrid.BindingContext as SubAlbum);
                            if (subAlbum == null) return;
                            subAlbum.IsExpanded = !subAlbum.IsExpanded;
                            if (subAlbum.IsExpanded)
                            {
                                tracksLayout.IsVisible = true;
                                await tracksLayout.FadeTo(1, 200, Easing.CubicOut);
                            }
                            else
                            {
                                await tracksLayout.FadeTo(0, 150, Easing.CubicIn);
                                tracksLayout.IsVisible = false;
                            }
                        };
                        headerGrid.GestureRecognizers.Add(headerTap);
                        // Every folder gets a header, including the main one that holds files sitting directly in
                        // the work folder: the user asked for the main folder to be openable/collapsible like the
                        // rest, so it is no longer hidden.

                        BindableLayout.SetItemTemplate(tracksLayout, new DataTemplate(() =>
                        {
                            var trackTap = new TapGestureRecognizer();
                            trackTap.SetBinding(TapGestureRecognizer.CommandProperty, new Binding(nameof(AlbumViewModel.PlayTrackCommand), source: BindingContext));
                            trackTap.SetBinding(TapGestureRecognizer.CommandParameterProperty, ".");

                            var trackGrid = new Grid
                            {
                                Padding = new Thickness(10, 12),
                                ColumnDefinitions = Columns.Define(Auto, Star),
                                ColumnSpacing = 12,
                                Children =
                                {
                                    new Label { Text = "\u25B6", FontSize = AppFontSizes.ButtonLarge, VerticalOptions = LayoutOptions.Center }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.SuccessLight, AppColors.SuccessDark)
                                        .Column(0),

                                    new VerticalStackLayout
                                    {
                                        Spacing = AppLayout.SpacingExtraSmall,
                                        VerticalOptions = LayoutOptions.Center,
                                        Children =
                                        {
                                            new Label { FontSize = AppFontSizes.ButtonLarge, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
                                                .Bind(Label.TextProperty, nameof(Track.Title))
                                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                                            new Label { FontSize = AppFontSizes.Caption }
                                                .Bind(Label.TextProperty, nameof(Track.TranslatedTitle))
                                                .Bind(VisualElement.IsVisibleProperty, nameof(Track.TranslatedTitle), converter: new EmptyStringToFalseConverter())
                                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                                        }
                                    }
                                    .Column(1)
                                }
                            };

                            // Row hover makes the long desktop track list readable.
                            // The row is a button in behaviour but announced as loose text (UX-07): name it from
                            // the track title and say what tapping does.
                            trackGrid.SetBinding(SemanticProperties.DescriptionProperty, new Binding(nameof(Track.Title)));
                            SemanticProperties.SetHint(trackGrid, "Double tap to play this track");

                            var pointer = new PointerGestureRecognizer();
                            pointer.PointerEntered += (_, _) => trackGrid.Opacity = 0.82;
                            pointer.PointerExited += (_, _) => trackGrid.Opacity = 1;
                            trackGrid.GestureRecognizers.Add(pointer);

                            trackGrid.GestureRecognizers.Add(trackTap);
                            return trackGrid;
                        }));

                        subAlbumLayout.Children.Add(headerGrid);
                        subAlbumLayout.Children.Add(tracksLayout);

                        return subAlbumLayout;
                    }))
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
