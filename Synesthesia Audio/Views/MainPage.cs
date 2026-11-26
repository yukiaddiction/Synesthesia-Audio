using System.ComponentModel;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Models;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Views.Components;

namespace Synesthesia_Audio.Views;

public class MainPage : ContentPage
{
    private readonly CollectionView _collectionView;
    private readonly MainViewModel _viewModel;
    private readonly Label _subtitleLabel;
    private GlassScaffold? _scaffold;

    public MainPage(MainViewModel viewModel, Services.AudioService audioService)
    {
        _viewModel = viewModel;
        _collectionView = new CollectionView
        {
            // Without this a search that matches nothing showed a blank area, which reads as a broken
            // screen rather than an empty result (UX-05).
            EmptyView = new Label
            {
                Text = "No albums match your search",
                FontSize = AppFontSizes.Body,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            }
                .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical),
            ItemTemplate = new DataTemplate(() => new AlbumCardView(viewModel, () => _scaffold?.IsWide ?? false))
        };

        this.Bind(ContentPage.TitleProperty, nameof(MainViewModel.Title));
        Shell.SetNavBarIsVisible(this, false);

        var scaffold = new GlassScaffold("Library");
        _scaffold = scaffold;
        _subtitleLabel = scaffold.SubtitleLabel;
        scaffold.TitleLabel.Bind(Label.TextProperty, nameof(MainViewModel.Title));

        var sortButton = GlassTheme.IconButton("\u21C5", AppFontSizes.ButtonLarge)
            .Bind(Button.CommandProperty, nameof(MainViewModel.ShowSortOptionsCommand))
            .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
            .Invoke(b => b.AutomationId = "SortButton");
        ToolTipProperties.SetText(sortButton, "Sort library");
        SemanticProperties.SetDescription(sortButton, "Sort library");
        SemanticProperties.SetHint(sortButton, "Opens the sort options");

        var rescanButton = GlassTheme.IconButton("\u27F3", AppFontSizes.ButtonLarge)
            .Bind(Button.CommandProperty, nameof(MainViewModel.ScanLibraryCommand))
            .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
            .Invoke(b => b.AutomationId = "RescanButton");
        ToolTipProperties.SetText(rescanButton, "Rescan library");
        SemanticProperties.SetDescription(rescanButton, "Rescan library");
        SemanticProperties.SetHint(rescanButton, "Scans the selected folder again");

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

        // A screen reader announces the placeholder text only incidentally; the description says what the field
        // is for (UX-07).
        SemanticProperties.SetDescription(searchBar, "Search library");
        SemanticProperties.SetHint(searchBar, "Filters albums by title as you type");
        var searchPill = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassPillRadius },
            Margin = new Thickness(16, 0, 16, 8),
            Padding = new Thickness(4, 0),
            Content = searchBar
        }
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
        .Glass();

        SearchHost = searchBar;

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

        var previewPane = new AlbumPreviewPane { IsVisible = false };
        PreviewPane = previewPane;

        // Column 1 collapses to zero while compact, so the grid gets the full width back.
        var contentGrid = new Grid
        {
            ColumnDefinitions = Columns.Define(Star, new GridLength(0)),
            ColumnSpacing = AppLayout.SpacingSection,
            Children =
            {
                _collectionView
                    .Bind(CollectionView.ItemsSourceProperty, nameof(MainViewModel.Albums))
                    .Bind(CollectionView.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
                    .Bind(View.MarginProperty,
                        path: nameof(audioService.CurrentTrack),
                        source: audioService,
                        convert: (Track? track) => track != null ? new Thickness(0, 0, 0, 120) : new Thickness(0, 0, 0, 16))
                    .Column(0),

                emptyState.Column(0),
                previewPane.Column(1)
            }
        };

        var body = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Star),
            Children =
            {
                searchPill.Row(0),
                contentGrid.Row(1)
            }
        };

        // The two players are independent components; only one is ever visible, chosen by mode.
        var compactPlayerHost = new Grid { Children = { new FloatingPlayerBar(audioService) } };
        var dockHost = new Grid { Children = { new TransportDock(audioService) }, IsVisible = false };

        scaffold.LayoutModeChanged += (_, wide) =>
        {
            contentGrid.ColumnDefinitions[1].Width = wide
                ? new GridLength(AppBreakpoints.PreviewPaneWidth)
                : new GridLength(0);

            previewPane.IsVisible = wide;
            compactPlayerHost.IsVisible = !wide;
            dockHost.IsVisible = wide;

            if (wide)
                SelectFirstIfNone();
        };

        scaffold.SetBody(body);
        scaffold.AddOverlay(scanToast);
        scaffold.AddOverlay(compactPlayerHost);
        scaffold.AddOverlay(dockHost);

        Content = scaffold;

        UpdateSubtitle();
    }

    /// <summary>The search box, so the Ctrl+F shortcut can focus it.</summary>
    public SearchBar? SearchHost { get; }

    private AlbumPreviewPane PreviewPane { get; }

    private void SelectFirstIfNone()
    {
        if (_viewModel.SelectedWork == null && _viewModel.Albums.Count > 0)
            _viewModel.SelectedWork = _viewModel.Albums[0];
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Albums is replaced wholesale on every refresh, so the caption follows the
        // view-model properties rather than subscribing to a collection instance.
        if (e.PropertyName is nameof(MainViewModel.Albums) or nameof(MainViewModel.SelectedFilter))
        {
            UpdateSubtitle();
            if (_scaffold?.IsWide == true)
                SelectFirstIfNone();
        }
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

        UiEvents.FocusSearchRequested += OnFocusSearchRequested;

        // MainViewModel is a singleton, so a page that subscribed in its constructor and never unsubscribed
        // kept the whole re-created UI tree alive and reacting after a window was re-created.
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

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

            // The intent's album request is consumed here, once the library is loaded (PAT-07).
            if (IPlatformApplication.Current?.Services?.GetService<StartupNavigation>() is { } startupNavigation)
            {
                var navigationService = IPlatformApplication.Current?.Services?.GetService<INavigationService>();
                if (navigationService != null)
                    await startupNavigation.TryNavigateAsync(cacheService: IPlatformApplication.Current!.Services!.GetRequiredService<LibraryCacheService>(), navigationService);
            }
        }
        catch (Exception ex)
        {
            Synesthesia_Audio.Services.Logging.AppLog.Get<MainPage>().LogError(ex, "MainPage initialization failed");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UiEvents.FocusSearchRequested -= OnFocusSearchRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

    /// <summary>Ctrl+F from the desktop keyboard handler.</summary>
    private void OnFocusSearchRequested()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (SearchHost is { IsVisible: true } search)
                search.Focus();
        });
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0) return;

        // Reserve the chrome the grid no longer occupies when the desktop panes are up.
        var available = width;
        if (AppBreakpoints.IsWide(width))
            available -= AppBreakpoints.SidebarWidth + AppBreakpoints.PreviewPaneWidth + 48;

        var span = available > 900 ? 4 : available > 640 ? 3 : 2;

        // OnSizeAllocated fires for every resize frame. Replacing the layout each time rebuilt the grid
        // and threw away the scroll position, so the column count is changed in place instead.
        if (_collectionView.ItemsLayout is GridItemsLayout grid)
        {
            if (grid.Span == span) return;
            grid.Span = span;
            return;
        }

        _collectionView.ItemsLayout = new GridItemsLayout(span, ItemsLayoutOrientation.Vertical);
    }
}
