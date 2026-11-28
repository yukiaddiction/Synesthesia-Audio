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
    private bool _notificationPermissionChecked;
    private bool _initialized;

    private readonly ILogger<MainPage> _logger;

    public MainPage(MainViewModel viewModel, Services.AudioService audioService, ILogger<MainPage> logger)
    {
        _logger = logger;
        _viewModel = viewModel;

        // Required: without it every binding resolves against null and OnAppearing never initialises the
        // view model, so the library silently stays empty.
        BindingContext = viewModel;
        _collectionView = new CollectionView
        {
            // A blank area reads as a broken screen rather than a search with no matches.
            EmptyView = new Label
            {
                Text = "No works on this shelf match your search",
                FontSize = AppFontSizes.Body,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Center
            }
                .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

            ItemsLayout = new GridItemsLayout(2, ItemsLayoutOrientation.Vertical),
            ItemTemplate = new DataTemplate(() => new AlbumCardView(viewModel, () => _scaffold?.IsWide ?? false, audioService))
        };

        this.Bind(ContentPage.TitleProperty, nameof(MainViewModel.Title));
        Shell.SetNavBarIsVisible(this, false);

        var scaffold = new GlassScaffold("Voice Works");
        _scaffold = scaffold;
        _subtitleLabel = scaffold.SubtitleLabel;
        scaffold.TitleLabel.Bind(Label.TextProperty, nameof(MainViewModel.Title));

        var sortButton = GlassTheme.IconButton("\u21C5", AppFontSizes.ButtonLarge)
            .Bind(Button.CommandProperty, nameof(MainViewModel.ShowSortOptionsCommand))
            .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
            .Invoke(b => b.AutomationId = "SortButton");
        ToolTipProperties.SetText(sortButton, "Sort shelf");
        SemanticProperties.SetDescription(sortButton, "Sort shelf");
        SemanticProperties.SetHint(sortButton, "Opens the sort options");

        var rescanButton = GlassTheme.IconButton("\u27F3", AppFontSizes.ButtonLarge)
            .Bind(Button.CommandProperty, nameof(MainViewModel.ScanLibraryCommand))
            .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
            .Invoke(b => b.AutomationId = "RescanButton");
        ToolTipProperties.SetText(rescanButton, "Rescan shelf");
        SemanticProperties.SetDescription(rescanButton, "Rescan shelf");
        SemanticProperties.SetHint(rescanButton, "Scans the selected folder again");

        scaffold.Actions.Add(sortButton);
        scaffold.Actions.Add(rescanButton);

        var searchBar = new SearchBar
        {
            Placeholder = "Search works, cast, circles\u2026",
            BackgroundColor = Colors.Transparent
        }
        .Bind(SearchBar.TextProperty, nameof(MainViewModel.SearchText))
        .Bind(VisualElement.IsVisibleProperty, nameof(MainViewModel.HasFolderSelected))
        .AppThemeBinding(SearchBar.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
        .AppThemeBinding(SearchBar.PlaceholderColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark);

        // Screen readers don't reliably announce the placeholder, so the field's purpose is stated explicitly.
        SemanticProperties.SetDescription(searchBar, "Search the shelf");
        SemanticProperties.SetHint(searchBar, "Filters works by title, cast, circle or genre as you type");
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
                        Text = "\uD83C\uDFA7",
                        FontSize = AppFontSizes.CoverPlaceholderIcon,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark),

                    new Label
                    {
                        Text = "Your listening room is empty",
                        FontSize = AppFontSizes.SectionTitle,
                        FontAttributes = AppFonts.Bold,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                    new Label
                    {
                        Text = "Point the app at a folder of voice works and the shelf fills itself. Covers, cast, circles and scenes come from DLsite, or from the folders themselves.",
                        FontSize = AppFontSizes.Body,
                        HorizontalTextAlignment = TextAlignment.Center,
                        Margin = new Thickness(0, 2, 0, 14)
                    }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                    GlassTheme.PillButton("Set up your shelf", AppFontSizes.BodyBold, accent: true, height: AppLayout.ButtonHeightDefault)
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
            Margin = new Thickness(16, 0, 16, AppLayout.PlayerReserve),
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

        // The preview column is zero-width while compact so the album grid gets the full width.
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
                        convert: (Track? track) => track != null
                            ? new Thickness(0, 0, 0, AppLayout.PlayerReserve)
                            : new Thickness(0, 0, 0, 16))
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

        var compactPlayerHost = new Grid { Children = { new FloatingPlayerBar(audioService, IPlatformApplication.Current!.Services!.GetRequiredService<LibraryCacheService>()) } };
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

    /// <summary>Exposed so the Ctrl+F shortcut can focus it.</summary>
    public SearchBar? SearchHost { get; }

    private AlbumPreviewPane PreviewPane { get; }

    private void SelectFirstIfNone()
    {
        if (_viewModel.SelectedWork == null && _viewModel.Albums.Count > 0)
            _viewModel.SelectedWork = _viewModel.Albums[0];
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Albums is replaced wholesale on refresh, so watch the property rather than a collection instance.
        if (e.PropertyName is nameof(MainViewModel.Albums) or nameof(MainViewModel.SelectedFilter))
        {
            UpdateSubtitle();
            if (_scaffold?.IsWide == true)
                SelectFirstIfNone();
        }
    }

    private void UpdateSubtitle()
    {
        var filter = _viewModel.SelectedFilter switch
        {
            MainViewModel.FilterR18 => "R-18 shelf",
            MainViewModel.FilterAllAge => "All Age shelf",
            _ => "Whole shelf"
        };

        var count = _viewModel.Albums?.Count ?? 0;
        _subtitleLabel.Text = count > 0 ? $"{filter} \u00B7 {count} works" : filter;
        _subtitleLabel.IsVisible = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        UiEvents.FocusSearchRequested += OnFocusSearchRequested;

        // Subscribed per appearance, not in the constructor: the view model is a singleton and would otherwise
        // keep a re-created window's old UI tree alive.
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Once per page instance, so returning to the library doesn't re-prompt for permission or re-initialise.
        try
        {
            if (!_notificationPermissionChecked)
            {
                _notificationPermissionChecked = true;
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                {
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Notification permission request failed"); }

        try
        {
            if (BindingContext is MainViewModel vm && !_initialized)
            {
                _initialized = true;
                await vm.InitializeAsync();
            }

            UpdateSubtitle();

            // A launch intent's album request can only be honoured once the library is loaded.
            if (IPlatformApplication.Current?.Services?.GetService<StartupNavigation>() is { } startupNavigation)
            {
                var navigationService = IPlatformApplication.Current?.Services?.GetService<INavigationService>();
                if (navigationService != null)
                    await startupNavigation.TryNavigateAsync(cacheService: IPlatformApplication.Current!.Services!.GetRequiredService<LibraryCacheService>(), navigationService);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MainPage initialization failed");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        UiEvents.FocusSearchRequested -= OnFocusSearchRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
    }

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

        // In wide mode the grid only gets what's left after the sidebar, the preview pane and 48px of gutters.
        var available = width;
        if (AppBreakpoints.IsWide(width))
            available -= AppBreakpoints.SidebarWidth + AppBreakpoints.PreviewPaneWidth + 48;

        var span = available > 900 ? 4 : available > 640 ? 3 : 2;

        // Runs on every resize frame; replacing the layout would rebuild the grid and lose the scroll position.
        if (_collectionView.ItemsLayout is GridItemsLayout grid)
        {
            if (grid.Span == span) return;
            grid.Span = span;
            return;
        }

        _collectionView.ItemsLayout = new GridItemsLayout(span, ItemsLayoutOrientation.Vertical);
    }
}
