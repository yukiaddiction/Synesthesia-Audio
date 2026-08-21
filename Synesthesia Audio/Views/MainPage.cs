using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Models;
using Microsoft.Maui.Controls.Shapes;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Views.Components;

namespace Synesthesia_Audio.Views;

public class MainPage : ContentPage
{
    private readonly CollectionView _collectionView;

    public MainPage(MainViewModel viewModel, Services.AudioService audioService)
    {
        _collectionView = new CollectionView
        {
            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical),
            ItemTemplate = new DataTemplate(() => { return new AlbumCardView(viewModel); })
        };
        BindingContext = viewModel;

        this.AppThemeBinding(ContentPage.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);
        this.Bind(ContentPage.TitleProperty, nameof(MainViewModel.Title));

        Content = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Star),
            Children =
            {
                new Grid
                {
                    Margin = AppLayout.MainScanMargin,
                    RowDefinitions = Rows.Define(Auto),
                    Children =
                    {
                        new Grid
                        {
                            ColumnDefinitions = Columns.Define(Star, 44, 44),
                            ColumnSpacing = AppLayout.SpacingExtraLarge,
                            Margin = AppLayout.MainScanMargin,
                            Children =
                            {
                                new SearchBar { Placeholder = "Search titles, circles, VAs..." }
                                    .Column(0)
                                    .Bind(SearchBar.TextProperty, nameof(MainViewModel.SearchText)),

                                new Button
                                {
                                    Text = "\u21C5",
                                    FontSize = AppFontSizes.ButtonLarge,
                                    Padding = AppLayout.ButtonPadding,
                                    HeightRequest = 36,
                                }
                                .Column(1)
                                .Bind(Button.CommandProperty, nameof(MainViewModel.ShowSortOptionsCommand))
                                .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark),

                                new Button
                                {
                                    Text = "\u27F3",
                                    FontSize = AppFontSizes.ButtonLarge,
                                    Padding = AppLayout.ButtonPadding,
                                    HeightRequest = 36,
                                }
                                .Column(2)
                                .Bind(Button.CommandProperty, nameof(MainViewModel.ScanLibraryCommand))
                                .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
                            }
                        }
                        .Row(0)
                        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
                    }
                }.Row(0),

                _collectionView
                .Row(1)
                .Bind(CollectionView.ItemsSourceProperty, nameof(MainViewModel.Albums))
                .Bind(CollectionView.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
                .Bind(View.MarginProperty,
                    path: nameof(audioService.CurrentTrack),
                    source: audioService,
                    convert: (Track? track) => track != null ? new Thickness(0, 0, 0, 110) : new Thickness(0)),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingMedium,
                    Padding = new Thickness(40, 80),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "Welcome to Synesthesia Audio",
                            FontSize = AppFontSizes.SectionTitle,
                            FontAttributes = AppFonts.Bold,
                            HorizontalTextAlignment = TextAlignment.Center
                        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                        new Label
                        {
                            Text = "Set up your audio library folder to get started. Choose a local folder or connect to cloud storage.",
                            FontSize = AppFontSizes.Body,
                            HorizontalTextAlignment = TextAlignment.Center,
                            Margin = new Thickness(0, 8, 0, 16)
                        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                        new Button
                        {
                            Text = "Go to Settings",
                            FontSize = AppFontSizes.BodyBold,
                            HeightRequest = AppLayout.ButtonHeightDefault,
                            HorizontalOptions = LayoutOptions.Center,
                            Padding = new Thickness(30, 0)
                        }
                        .Bind(Button.CommandProperty, nameof(MainViewModel.GoToSettingsCommand))
                        .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
                    }
                }
                .Row(1)
                .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected), converter: new InvertedBoolConverter()),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingSmall,
                    Padding = AppLayout.LibraryScanPadding,
                    VerticalOptions = LayoutOptions.End,
                    Margin = AppLayout.LibraryScanMargin,
                    Children =
                    {
                        new Label { FontSize = AppFontSizes.Caption, HorizontalTextAlignment = TextAlignment.Center }
                            .Bind(Label.TextProperty, nameof(MainViewModel.ScanProgressText))
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),
                        new ProgressBar { HeightRequest = AppLayout.ProgressBarHeight, ProgressColor = AppColors.PrimaryLight }
                            .Bind(ProgressBar.ProgressProperty, nameof(MainViewModel.ScanProgress))
                    }
                }
                .AppThemeBinding(VisualElement.BackgroundColorProperty, Color.FromArgb("#F0FFFFFF"), Color.FromArgb("#F0112A3E"))
                .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.IsScanning)),

                new FloatingPlayerBar(audioService)
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }
        catch (Exception ex) { Synesthesia_Audio.Services.Logging.AppLog.Get<MainPage>().LogWarning(ex, "Notification permission request failed"); }

        try
        {
            if (BindingContext is MainViewModel vm)
            {
                await vm.InitializeAsync();
            }

#if ANDROID
            MainActivity.TryNavigateToAlbum();
#endif
        }
        catch (Exception ex)
        {
            Synesthesia_Audio.Services.Logging.AppLog.Get<MainPage>().LogError(ex, "MainPage initialization failed");
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;
        var span = width > 900 ? 4 : width > 600 ? 3 : 2;
        _collectionView.ItemsLayout = new GridItemsLayout(span, ItemsLayoutOrientation.Vertical);
    }
}
