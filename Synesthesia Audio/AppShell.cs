using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Views;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio;

public class AppShell : Shell
{
    private readonly MainViewModel _mainViewModel;
    private MediaElement? _mediaElement;
    private NavItem _allNav = null!;
    private NavItem _allAgeNav = null!;
    private NavItem _r18Nav = null!;
    private NavItem _settingsNav = null!;

    private readonly ILogger<AppShell> _logger;

    public AppShell(AudioService audioService, MainViewModel mainViewModel, ILogger<AppShell> logger)
    {
        _logger = logger;
        _mainViewModel = mainViewModel;
        FlyoutBehavior = FlyoutBehavior.Flyout;

        this.AppThemeBinding(Shell.FlyoutBackgroundColorProperty, AppColors.GlassFlyoutLight, AppColors.GlassFlyoutDark);
        this.AppThemeBinding(Shell.ForegroundColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);
        this.AppThemeBinding(Shell.TitleColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);
        this.AppThemeBinding(Shell.UnselectedColorProperty, AppColors.TextTertiaryLight, AppColors.TextTertiaryDark);
        this.AppThemeBinding(Shell.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);

        Items.Add(new ShellContent
        {
            Title = "Library",
            Route = "Library",
            ContentTemplate = new DataTemplate(() => IPlatformApplication.Current!.Services.GetRequiredService<MainPage>())
        });

        Items.Add(new ShellContent
        {
            Title = "Settings",
            Route = "Settings",
            ContentTemplate = new DataTemplate(() => IPlatformApplication.Current!.Services.GetRequiredService<SettingsPage>())
        });

        Routing.RegisterRoute(nameof(AlbumPage), typeof(AlbumPage));
        Routing.RegisterRoute(nameof(LogViewerPage), typeof(LogViewerPage));

        FlyoutContent = CreateFlyoutContent();

        _mainViewModel.PropertyChanged += FilterChangedHandler;

        Navigated += (s, e) => UpdateFlyoutSelection();

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += ThemeChangedHandler;
        }

        UpdateFlyoutSelection();
    }

    private System.ComponentModel.PropertyChangedEventHandler? _filterChangedHandler;
    private EventHandler<AppThemeChangedEventArgs>? _themeChangedHandler;

    private System.ComponentModel.PropertyChangedEventHandler FilterChangedHandler =>
        _filterChangedHandler ??= (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedFilter))
            {
                UpdateFlyoutSelection();
            }
        };

    private EventHandler<AppThemeChangedEventArgs> ThemeChangedHandler =>
        _themeChangedHandler ??= (_, _) => MainThread.BeginInvokeOnMainThread(UpdateFlyoutSelection);

    /// <summary>
    /// The shell is per window but subscribes to process-lifetime objects; unsubscribing here keeps a dead window's shell from leaking.
    /// </summary>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler != null) return;

        if (_filterChangedHandler != null)
        {
            _mainViewModel.PropertyChanged -= _filterChangedHandler;
            _filterChangedHandler = null;
        }

        if (_themeChangedHandler != null)
        {
            if (Application.Current != null)
                Application.Current.RequestedThemeChanged -= _themeChangedHandler;
            _themeChangedHandler = null;
        }
    }

    public void SetMediaElement(MediaElement? mediaElement)
    {
        if (mediaElement is null) return;

        _mediaElement = mediaElement;

        if (FlyoutContent is ScrollView sv && sv.Content is VerticalStackLayout flyoutStack)
        {
            flyoutStack.Children.Insert(0, _mediaElement);
        }
    }

    private View CreateFlyoutContent()
    {
        // "All" keeps works with an unknown age rating reachable, since they are excluded from All Age.
        _allNav = CreateNavItem("All works");
        _allNav.Container.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    _mainViewModel.SelectedFilter = MainViewModel.FilterAll;
                    FlyoutIsPresented = false;
                    await GoToAsync("//Library");
                    UpdateFlyoutSelection();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Flyout navigation failed (All)");
                }
            })
        });

        _allAgeNav = CreateNavItem("All Age");
        _allAgeNav.Container.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    _mainViewModel.SelectedFilter = "SFW";
                    FlyoutIsPresented = false;
                    await GoToAsync("//Library");
                    UpdateFlyoutSelection();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Flyout navigation failed (All Age)");
                }
            })
        });

        _r18Nav = CreateNavItem("R-18");
        _r18Nav.Container.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    _mainViewModel.SelectedFilter = "NSFW";
                    FlyoutIsPresented = false;
                    await GoToAsync("//Library");
                    UpdateFlyoutSelection();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Flyout navigation failed (R-18)");
                }
            })
        });

        _settingsNav = CreateNavItem("Settings");
        _settingsNav.Container.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    FlyoutIsPresented = false;
                    await GoToAsync("//Settings");
                    UpdateFlyoutSelection();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Flyout navigation failed (Settings)");
                }
            })
        });

        var flyoutStack = new VerticalStackLayout
        {
            Padding = new Thickness(12, 20, 12, 12),
            Spacing = AppLayout.SpacingSmall,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = Columns.Define(Auto, Star),
                    ColumnSpacing = AppLayout.SpacingLarge,
                    Padding = new Thickness(8, 8, 8, 16),
                    Children =
                    {
                        GlassTheme.BrandMark("\u266B", 44, 20).Column(0),

                        new VerticalStackLayout
                        {
                            Spacing = 0,
                            VerticalOptions = LayoutOptions.Center,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Synesthesia Audio",
                                    FontSize = AppFontSizes.GroupTitle,
                                    FontAttributes = AppFonts.Bold
                                }
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                                new Label
                                {
                                    Text = "ASMR \u00B7 Voice drama",
                                    FontSize = AppFontSizes.Caption
                                }
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                            }
                        }.Column(1)
                    }
                },

                new BoxView { HeightRequest = 1, Margin = new Thickness(4, 0, 4, 12) }
                    .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark),

                GlassTheme.SectionLabel("SHELF"),

                _allNav.Container,
                _allAgeNav.Container,
                _r18Nav.Container,

                new BoxView { HeightRequest = 1, Margin = new Thickness(4, 12, 4, 12) }
                    .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark),

                _settingsNav.Container
            }
        };

        return new ScrollView { Content = flyoutStack };
    }

    private static NavItem CreateNavItem(string text)
    {
        var accent = new BoxView
        {
            WidthRequest = AppLayout.GlassTintBorderSize,
            CornerRadius = AppLayout.GlassTintBorderSize / 2,
            VerticalOptions = LayoutOptions.Fill,
            IsVisible = false
        };

        var label = new Label
        {
            Text = text,
            FontSize = AppFontSizes.BodyBold,
            FontAttributes = AppFonts.Bold,
            VerticalOptions = LayoutOptions.Center
        };

        var container = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            StrokeThickness = 0,
            Padding = new Thickness(8, 10),
            Margin = new Thickness(4, 2, 4, 2),
            Content = new Grid
            {
                ColumnDefinitions = Columns.Define(AppLayout.GlassTintBorderSize, Star),
                ColumnSpacing = AppLayout.SpacingExtraLarge,
                Children =
                {
                    accent.Column(0),
                    label.Column(1)
                }
            }
        };

        return new NavItem(container, label, accent);
    }

    private void UpdateFlyoutSelection()
    {
        bool isSettings = CurrentState?.Location?.OriginalString?.Contains("Settings") == true;
        if (isSettings)
        {
            SetSelectedNav(_settingsNav);
            return;
        }

        var filter = _mainViewModel.SelectedFilter;
        if (string.Equals(filter, MainViewModel.FilterR18, StringComparison.OrdinalIgnoreCase))
        {
            SetSelectedNav(_r18Nav);
        }
        else if (string.Equals(filter, MainViewModel.FilterAllAge, StringComparison.OrdinalIgnoreCase))
        {
            SetSelectedNav(_allAgeNav);
        }
        else
        {
            SetSelectedNav(_allNav);
        }
    }

    private void SetSelectedNav(NavItem selected)
    {
        ApplyNavState(_allNav, ReferenceEquals(_allNav, selected));
        ApplyNavState(_allAgeNav, ReferenceEquals(_allAgeNav, selected));
        ApplyNavState(_r18Nav, ReferenceEquals(_r18Nav, selected));
        ApplyNavState(_settingsNav, ReferenceEquals(_settingsNav, selected));
    }

    /// <summary>
    /// Uses AppThemeBinding instead of reading RequestedTheme, which can still be stale when the theme-changed event fires.
    /// </summary>
    private static void ApplyNavState(NavItem nav, bool isActive)
    {
        nav.Container.AppThemeBinding(
            VisualElement.BackgroundColorProperty,
            isActive ? AppColors.GlassTintLight : Colors.Transparent,
            isActive ? AppColors.GlassTintDark : Colors.Transparent);

        nav.Label.AppThemeBinding(
            Label.TextColorProperty,
            isActive ? AppColors.ActiveTextLight : AppColors.NormalTextLight,
            isActive ? AppColors.ActiveTextDark : AppColors.TextPrimaryDark);

        nav.Accent.IsVisible = isActive;
        nav.Accent.AppThemeBinding(
            BoxView.ColorProperty,
            isActive ? AppColors.PrimaryLight : Colors.Transparent,
            isActive ? AppColors.PrimaryDark : Colors.Transparent);
    }

    private sealed class NavItem
    {
        public NavItem(Border container, Label label, BoxView accent)
        {
            Container = container;
            Label = label;
            Accent = accent;
        }

        public Border Container { get; }
        public Label Label { get; }
        public BoxView Accent { get; }
    }
}
