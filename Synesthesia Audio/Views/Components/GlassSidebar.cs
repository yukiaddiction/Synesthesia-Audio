using System.ComponentModel;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Services.Logging;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// The desktop navigation rail: brand, library filters, settings and the app version.
/// Only shown above <see cref="AppBreakpoints.Wide"/>; the compact layout keeps using the flyout.
/// </summary>
public sealed class GlassSidebar : Border
{
    private readonly GlassNavItem _all;
    private readonly GlassNavItem _allAge;
    private readonly GlassNavItem _r18;
    private readonly GlassNavItem _settings;

    private MainViewModel? _viewModel;
    private Shell? _shell;
    private bool _hooked;

    public GlassSidebar(MainViewModel? viewModel = null)
    {
        // The rail drives the library filter, so it must talk to the singleton view model rather than to
        // whatever page happens to be showing. Reading BindingContext meant the filters did nothing on
        // Settings, Album and every other page whose BindingContext is not a MainViewModel.
        _viewModel = viewModel ?? IPlatformApplication.Current?.Services?.GetService<MainViewModel>();

        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius };
        Margin = new Thickness(12, 12, 0, 12);
        Padding = new Thickness(10);

        _all = new GlassNavItem("All", (_, _) => NavigateAsync(MainViewModel.FilterAll, "//Library"));
        _allAge = new GlassNavItem("All Age", (_, _) => NavigateAsync(MainViewModel.FilterAllAge, "//Library"));
        _r18 = new GlassNavItem("R-18", (_, _) => NavigateAsync(MainViewModel.FilterR18, "//Library"));
        _settings = new GlassNavItem("Settings", (_, _) => NavigateAsync(null, "//Settings"));

        var brand = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(8, 8, 8, 10),
            Children =
            {
                new Label { Text = "Synesthesia Audio", FontSize = AppFontSizes.GroupTitle, FontAttributes = AppFonts.Bold }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),
                new Label { Text = "Audio library", FontSize = AppFontSizes.Caption }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
            }
        };

        var nav = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                GlassTheme.SectionLabel("LIBRARY"),
                _all,
                _allAge,
                _r18,
                new BoxView { HeightRequest = 1, Margin = new Thickness(8, 10) }
                    .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark),
                _settings
            }
        };

        var footer = new Label
        {
            Text = $"Version {AppInfo.Current.VersionString}",
            FontSize = AppFontSizes.Caption,
            Margin = new Thickness(8, 8, 8, 4)
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark);

        Content = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Auto, Star, Auto),
            Children =
            {
                brand.Row(0),
                new BoxView { HeightRequest = 1, Margin = new Thickness(8, 0, 8, 6) }
                    .Row(1)
                    .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark),
                nav.Row(2),
                footer.Row(3)
            }
        };

        this.Glass(strong: true, elevated: true);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Only a fallback: the singleton injected above is preferred, because pages other than the
        // library carry a different BindingContext.
        _viewModel ??= BindingContext as MainViewModel;
        Refresh();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_hooked)
            return;

        _shell = Shell.Current;
        if (_shell != null)
            _shell.Navigated += OnNavigated;
        if (_viewModel != null)
            _viewModel.PropertyChanged += OnViewModelChanged;

        _hooked = true;
        Refresh();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (!_hooked)
            return;

        if (_shell != null)
            _shell.Navigated -= OnNavigated;
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelChanged;

        _hooked = false;
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e) => Refresh();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedFilter))
            Refresh();
    }

    /// <summary>Recomputes which rail item is active from the current route and filter.</summary>
    public void Refresh()
    {
        var location = Shell.Current?.CurrentState?.Location?.OriginalString ?? string.Empty;
        var onSettings = location.Contains("Settings", StringComparison.OrdinalIgnoreCase);
        var isNsfw = string.Equals(_viewModel?.SelectedFilter, MainViewModel.FilterR18, StringComparison.OrdinalIgnoreCase);
        var isAllAge = string.Equals(_viewModel?.SelectedFilter, MainViewModel.FilterAllAge, StringComparison.OrdinalIgnoreCase);

        _settings.SetActive(onSettings);
        _all.SetActive(!onSettings && !isNsfw && !isAllAge);
        _allAge.SetActive(!onSettings && !isNsfw && isAllAge);
        _r18.SetActive(!onSettings && isNsfw);
    }

    private async void NavigateAsync(string? filter, string route)
    {
        try
        {
            if (filter != null && _viewModel != null)
                _viewModel.SelectedFilter = filter;

            if (Shell.Current is { } shell)
            {
                shell.FlyoutIsPresented = false;
                await shell.GoToAsync(route);
            }

            Refresh();
        }
        catch (Exception ex)
        {
            AppLog.Get<GlassSidebar>().LogWarning(ex, "Sidebar navigation failed");
        }
    }
}
