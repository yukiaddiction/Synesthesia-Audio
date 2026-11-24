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
    public AlbumPage(AlbumViewModel viewModel, AudioService audioService)
    {
        BindingContext = viewModel;
        Shell.SetNavBarIsVisible(this, false);

        var loading = new ActivityIndicator
        {
            IsRunning = true,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Color = AppColors.SuccessLight,
            HeightRequest = 40,
            WidthRequest = 40
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(AlbumViewModel.Album), converter: new NotNullToFalseConverter());

        var hero = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius },
            StrokeThickness = 0,
            HeightRequest = AppLayout.GlassHeroHeight,
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
                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.CoverImagePath)}", converter: new EmptyStringToTrueConverter()),

                    new Image { Aspect = Aspect.AspectFill }
                        .Bind(Image.SourceProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.CoverImagePath)}")
                        .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.CoverImagePath)}", converter: new EmptyStringToFalseConverter()),

                    // Scrim so the overlapping sheet above the artwork reads as depth, not as a seam.
                    new Border
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
                    },

                    new Border
                    {
                        BackgroundColor = Colors.Red,
                        Padding = new Thickness(8, 4),
                        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
                        HorizontalOptions = LayoutOptions.Start,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(12),
                        Content = new Label { TextColor = Colors.White, FontSize = AppFontSizes.Caption, FontAttributes = AppFonts.Bold }
                            .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.ProductId)}")
                    }
                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.IsDlsiteWork)}")
                }
            }
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

        var actions = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingExtraLarge,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new ActivityIndicator { HeightRequest = 20, WidthRequest = 20, IsVisible = false, Color = AppColors.PrimaryLight }
                    .Bind(ActivityIndicator.IsRunningProperty, nameof(AlbumViewModel.IsBusy))
                    .Bind(VisualElement.IsVisibleProperty, nameof(AlbumViewModel.IsBusy)),

                GlassTheme.PillButton("Edit Work Info", AppFontSizes.Body, height: 34)
                    .Bind(Button.CommandProperty, nameof(AlbumViewModel.OpenEditDialogCommand)),

                GlassTheme.PillButton("Translate Info", AppFontSizes.Body, height: 34)
                    .Bind(Button.CommandProperty, nameof(AlbumViewModel.TranslateCommand))
            }
        };

        var sheet = new Border
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
                    })),

                    new BoxView { HeightRequest = 1 }
                        .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark),

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
                                    .Bind(Label.TextProperty, nameof(SubAlbum.IsExpanded), converter: new BoolToArrowConverter())
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
                        headerGrid.Bind(VisualElement.IsVisibleProperty, nameof(SubAlbum.Title), converter: new NotMainTitleConverter());

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
                                    new Label { Text = "\u25B6", TextColor = AppColors.SuccessLight, FontSize = AppFontSizes.ButtonLarge, VerticalOptions = LayoutOptions.Center }
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

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = AppLayout.GlassContentPadding,
                Spacing = AppLayout.GlassSectionGap,
                Children = { hero, sheet }
            }
        };

        var scaffold = new GlassScaffold("Album", back: true);
        scaffold.SetBody(new Grid { Children = { scroll, loading } });
        scaffold.AddOverlay(new FloatingPlayerBar(audioService));

        Content = scaffold;
    }
}

public class BoolToArrowConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b) return b ? "▼" : "▶";
        return "▶";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}

public class NotMainTitleConverter : IValueConverter
{
    private static readonly HashSet<string> _hiddenTitles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Main", "flac", "mp3", "wav", "m4a", "mp4"
    };

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var title = value as string;
        if (string.IsNullOrEmpty(title)) return false;
        return !_hiddenTitles.Contains(title);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
