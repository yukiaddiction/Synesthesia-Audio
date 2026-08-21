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

namespace Synesthesia_Audio.Views;

public class AlbumPage : ContentPage
{
    public AlbumPage(AlbumViewModel viewModel, AudioService audioService)
    {
        BindingContext = viewModel;
        this.AppThemeBinding(ContentPage.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);

        Content = new Grid
        {
            Children =
            {
                new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Color = AppColors.SuccessLight,
                    HeightRequest = 40,
                    WidthRequest = 40
                }
                .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}", converter: new NotNullToFalseConverter()),

                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Padding = AppLayout.PagePadding,
                        Spacing = AppLayout.SpacingPage,
                        Children =
                        {
                            new Grid
                            {
                                HorizontalOptions = LayoutOptions.Fill,
                                Margin = AppLayout.HeaderImageNegativeMargin,
                                HeightRequest = AppLayout.HeaderCoverHeight,
                                Children =
                                {
                                    new Border
                                    {
                                        StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(0) },
                                        HorizontalOptions = LayoutOptions.Fill,
                                        BackgroundColor = AppColors.CoverPlaceholder,
                                        HeightRequest = AppLayout.HeaderCoverHeight,
                                        Content = new Label { Text = "🎵", FontSize = AppFontSizes.CoverPlaceholderIcon, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
                                    }
                                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.CoverImagePath)}", converter: new EmptyStringToTrueConverter()),

                                    new Grid
                                    {
                                        HeightRequest = 180,
                                        BackgroundColor = Colors.Black,
                                        Children =
                                        {
                                            new Image { Aspect = Aspect.AspectFit }
                                                .Bind(Image.SourceProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.CoverImagePath)}")
                                        }
                                    }
                                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.CoverImagePath)}", converter: new EmptyStringToFalseConverter()),

                                    new Border
                                    {
                                        BackgroundColor = Colors.Red,
                                        Padding = new Thickness(6, 3),
                                        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.ChipCornerRadius },
                                        HorizontalOptions = LayoutOptions.Start,
                                        VerticalOptions = LayoutOptions.Start,
                                        Margin = new Thickness(12),
                                        Content = new Label { TextColor = Colors.White, FontSize = AppFontSizes.Caption, FontAttributes = AppFonts.Bold }
                                            .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.ProductId)}")
                                    }
                                    .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.IsDlsiteWork)}")
                                }
                            },

                            new VerticalStackLayout
                            {
                                Spacing = AppLayout.SpacingMedium,
                                HorizontalOptions = LayoutOptions.Fill,
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

                            new VerticalStackLayout
                            {
                                Spacing = AppLayout.SpacingMedium,
                                HorizontalOptions = LayoutOptions.Fill,
                                Children =
                                {
                                    new Label { FontSize = AppFontSizes.PathLabel, HorizontalTextAlignment = TextAlignment.Center }
                                        .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.Circle)}", stringFormat: "Circle: {0}")
                                        .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.Circle)}", converter: new EmptyStringToFalseConverter())
                                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),

                                    new Label { FontSize = AppFontSizes.PathLabel, HorizontalTextAlignment = TextAlignment.Center }
                                        .Bind(Label.TextProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.VA)}", stringFormat: "Cast: {0}")
                                        .Bind(VisualElement.IsVisibleProperty, $"{nameof(AlbumViewModel.Album)}.{nameof(WorkMetadata.VA)}", converter: new EmptyStringToFalseConverter())
                                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),

                                    new HorizontalStackLayout
                                    {
                                        Spacing = AppLayout.SpacingExtraLarge,
                                        HorizontalOptions = LayoutOptions.Center,
                                        Margin = new Thickness(0, 5, 0, 0),
                                        Children =
                                        {
                                            new ActivityIndicator
                                            {
                                                HeightRequest = 20,
                                                WidthRequest = 20,
                                                IsVisible = false,
                                                Color = AppColors.PrimaryLight
                                            }
                                            .Bind(ActivityIndicator.IsRunningProperty, nameof(AlbumViewModel.IsBusy))
                                            .Bind(VisualElement.IsVisibleProperty, nameof(AlbumViewModel.IsBusy)),

                                            new Button
                                            {
                                                Text = "Edit Work Info",
                                                FontSize = AppFontSizes.Body,
                                                HeightRequest = 34,
                                                CornerRadius = 17,
                                                Padding = new Thickness(15, 0)
                                            }
                                            .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark)
                                            .Bind(Button.CommandProperty, nameof(AlbumViewModel.OpenEditDialogCommand)),

                                            new Button
                                            {
                                                Text = "Translate Info",
                                                FontSize = AppFontSizes.Body,
                                                HeightRequest = 34,
                                                CornerRadius = 17,
                                                Padding = new Thickness(15, 0)
                                            }
                                            .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark)
                                            .Bind(Button.CommandProperty, nameof(AlbumViewModel.TranslateCommand))
                                        }
                                    }
                                }
                            },

                            new FlexLayout
                            {
                                Wrap = FlexWrap.Wrap,
                                JustifyContent = FlexJustify.Center,
                                AlignItems = FlexAlignItems.Center,
                                AlignContent = FlexAlignContent.Center,
                                Direction = FlexDirection.Row,
                                Margin = new Thickness(0, 5)
                            }
                            .Bind(BindableLayout.ItemsSourceProperty, nameof(AlbumViewModel.GenreTags))
                            .ItemTemplate(new DataTemplate(() =>
                            {
                                return new Border
                                {
                                    StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                                    Padding = new Thickness(10, 5),
                                    Margin = new Thickness(3),
                                    Content = new Label { FontSize = AppFontSizes.Caption }
                                        .Bind(Label.TextProperty, ".")
                                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark)
                                }
                                .AppThemeBinding(Border.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark)
                                .AppThemeBinding(Border.StrokeProperty, Colors.Transparent, Colors.Transparent);
                            })),

                            new BoxView { HeightRequest = 1, Margin = new Thickness(0, 10) }
                                .AppThemeBinding(BoxView.ColorProperty, AppColors.StrokeLight, AppColors.StrokeDark),

                            new Border
                            {
                                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                                Padding = new Thickness(12),
                                Content = new VerticalStackLayout
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
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.SurfaceAltDark);

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
                                                new Label { Text = "▶", TextColor = AppColors.SuccessLight, FontSize = AppFontSizes.ButtonLarge, VerticalOptions = LayoutOptions.Center }
                                                    .Column(0),

                                                new VerticalStackLayout
                                                {
                                                    Spacing = AppLayout.SpacingExtraSmall,
                                                    VerticalOptions = LayoutOptions.Center,
                                                    Children =
                                                    {
                                                        new Label { FontSize = AppFontSizes.ButtonLarge, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
                                                            .Bind(Label.TextProperty, nameof(Track.Title))
                                                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormInputLight, Colors.White),

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
                            .AppThemeBinding(Border.BackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceDark)
                            .AppThemeBinding(Border.StrokeProperty, AppColors.StrokeLight, AppColors.StrokeDark)
                        }
                    }
                },

                new FloatingPlayerBar(audioService)
            }
        };
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
