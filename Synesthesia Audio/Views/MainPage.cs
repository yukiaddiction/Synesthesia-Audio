using System.ComponentModel;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Models;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Views.Components;

namespace Synesthesia_Audio.Views;

public class MainPage : ContentPage
{
    private readonly CollectionView _collectionView;
    private readonly MainViewModel _viewModel;
    private readonly Label _subtitleLabel;

    public MainPage(MainViewModel viewModel, Services.AudioService audioService)
    {
        _viewModel = viewModel;
        _collectionView = new CollectionView
        {
            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical),
            ItemTemplate = new DataTemplate(() => { return new AlbumCardView(viewModel); })
        };
        BindingContext = viewModel;

        this.Bind(ContentPage.TitleProperty, nameof(MainViewModel.Title));
        Shell.SetNavBarIsVisible(this, false);

        var scaffold = new GlassScaffold("Library");
        _subtitleLabel = scaffold.SubtitleLabel;
        scaffold.TitleLabel.Bind(Label.TextProperty, nameof(MainViewModel.Title));

        var sortButton = GlassTheme.IconButton("\u21C5", AppFontSizes.ButtonLarge)
            .Bind(Button.CommandProperty, nameof(MainViewModel.ShowSortOptionsCommand))
            .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
            .Invoke(b => b.AutomationId = "SortButton")
            .Invoke(b => SemanticProperties.SetDescription(b, "Sort library"));

        var rescanButton = GlassTheme.IconButton("\u27F3", AppFontSizes.ButtonLarge)
            .Bind(Button.CommandProperty, nameof(MainViewModel.ScanLibraryCommand))
            .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
            .Invoke(b => b.AutomationId = "RescanButton")
            .Invoke(b => SemanticProperties.SetDescription(b, "Rescan library"));

        scaffold.Actions.Add(sortButton);
        scaffold.Actions.Add(rescanButton);

        var searchBar = new SearchBar
        {
            Placeholder = "Search titles, circles, VAs...",
            BackgroundColor = Colors.Transparent
        }
        .Bind(SearchBar.TextProperty, nameof(MainViewModel.SearchText))
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
        .AppThemeBinding(SearchBar.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
        .AppThemeBinding(SearchBar.PlaceholderColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark);

        var searchPill = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassPillRadius },
            Margin = new Thickness(16, 0, 16, 8),
            Padding = new Thickness(4, 0),
            Content = searchBar
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
        .Glass();

        var emptyState = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassSheetRadius },
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 420,
            Margin = new Thickness(24),
            Padding = new Thickness(28, 32),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingMedium,
                Children =
                {
                    new Label
                    {
                        Text = "\u266B",
                        FontSize = AppFontSizes.CoverPlaceholderIcon,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark),

                    new Label
                    {
                        Text = "Welcome to Synesthesia Audio",
                        FontSize = AppFontSizes.SectionTitle,
                        FontAttributes = AppFonts.Bold,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                    new Label
                    {
                        Text = "Set up your audio library folder to get started. Choose a local folder or connect to cloud storage.",
                        FontSize = AppFontSizes.Body,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 14)
                    }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                    GlassTheme.PillButton("Go to Settings", AppFontSizes.BodyBold, accent: true, height: AppLayout.ButtonHeightDefault)
                        .Bind(Button.CommandProperty, nameof(MainViewModel.GoToSettingsCommand))
                        .Invoke(b => b.HorizontalOptions = LayoutOptions.Center)
                }
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected), converter: new InvertedBoolConverter())
        .Glass(strong: true, elevated: true);

        var scanToast = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(16, 0, 16, 112),
            Padding = new Thickness(14, 10),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingSmall,
                Children =
                {
                    new Label { FontSize = AppFontSizes.Caption, HorizontalTextAlignment = TextAlignment.Center }
                        .Bind(Label.TextProperty, nameof(MainViewModel.ScanProgressText))
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                    new ProgressBar { HeightRequest = AppLayout.ProgressBarHeight }
                        .Bind(ProgressBar.ProgressProperty, nameof(MainViewModel.ScanProgress))
                        .AppThemeBinding(ProgressBar.ProgressColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
                }
            }
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.IsScanStatusVisible))
        .Glass(strong: true, elevated: true);

        var content = new Grid
        {
            Children =
            {
                _collectionView
                    .Bind(CollectionView.ItemsSourceProperty, nameof(MainViewModel.Albums))
                    .Bind(CollectionView.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
                    .Bind(View.MarginProperty,
                        path: nameof(audioService.CurrentTrack),
                        source: audioService,
                        convert: (Track? track) => track != null ? new Thickness(0, 0, 0, 120) : new Thickness(0, 0, 0, 16)),

                emptyState
            }
        };

        var body = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Star),
            Children =
            {
                searchPill.Row(0),
                content.Row(1)
            }
        };

        scaffold.SetBody(body);
        scaffold.AddOverlay(scanToast);
        scaffold.AddOverlay(new FloatingPlayerBar(audioService));

        Content = scaffold;

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        UpdateSubtitle();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Albums is replaced wholesale on every refresh, so the caption follows the
        // view-model properties rather than subscribing to a collection instance.
        if (e.PropertyName is nameof(MainViewModel.Albums) or nameof(MainViewModel.SelectedFilter))
            UpdateSubtitle();
    }

    private void UpdateSubtitle()
    {
        var filter = _viewModel.SelectedFilter == "NSFW" ? "R-18" : "All Age";
        var count = _viewModel.Albums?.Count ?? 0;
        _subtitleLabel.Text = count > 0 ? $"{filter} \u00B7 {count} works" : filter;
        _subtitleLabel.IsVisible = true;
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

            UpdateSubtitle();

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
