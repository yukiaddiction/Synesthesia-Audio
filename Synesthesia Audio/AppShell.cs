using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Maui.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Views;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio;

public class AppShell : Shell
{
    private readonly MainViewModel _mainViewModel;
    private MediaElement? _mediaElement;
    private Border _allAgeButtonBorder = null!;
    private Border _r18ButtonBorder = null!;
    private Border _settingsButtonBorder = null!;
    private Label _allAgeLabel = null!;
    private Label _r18Label = null!;
    private Label _settingsLabel = null!;

    public AppShell(AudioService audioService, MainViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
        FlyoutBehavior = FlyoutBehavior.Flyout;

        this.AppThemeBinding(Shell.FlyoutBackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceAltDark);
        this.AppThemeBinding(Shell.ForegroundColorProperty, Colors.White, Colors.White);
        this.AppThemeBinding(Shell.TitleColorProperty, Colors.White, AppColors.TextPrimaryDark);
        this.AppThemeBinding(Shell.UnselectedColorProperty, AppColors.TextTertiaryLight, AppColors.TextTertiaryLight);
        this.AppThemeBinding(Shell.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.BackgroundDark);

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

        _mainViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedFilter))
            {
                UpdateFlyoutSelection();
            }
        };

        Navigated += (s, e) => UpdateFlyoutSelection();

        if (Application.Current != null)
        {
            Application.Current.RequestedThemeChanged += (s, e) =>
            {
                MainThread.BeginInvokeOnMainThread(UpdateFlyoutSelection);
            };
        }

        UpdateFlyoutSelection();
    }

    public void SetMediaElement(MediaElement mediaElement)
    {
        _mediaElement = mediaElement;
        if (FlyoutContent is ScrollView sv && sv.Content is VerticalStackLayout flyoutStack)
        {
            flyoutStack.Children.Insert(0, _mediaElement);
        }
    }

    private View CreateFlyoutContent()
    {
        _allAgeLabel = new Label { Text = "All Age", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold, VerticalOptions = LayoutOptions.Center };
        _allAgeButtonBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.ButtonCornerRadius },
            StrokeThickness = 0,
            Padding = new Thickness(12, 10),
            Margin = new Thickness(16, 2, 8, 2),
            Content = _allAgeLabel,
            GestureRecognizers =
            {
                new TapGestureRecognizer
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
                            Services.Logging.AppLog.Get<AppShell>().LogWarning(ex, "Flyout navigation failed (All Age)");
                        }
                    })
                }
            }
        };

        _r18Label = new Label { Text = "R-18", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold, VerticalOptions = LayoutOptions.Center };
        _r18ButtonBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.ButtonCornerRadius },
            StrokeThickness = 0,
            Padding = new Thickness(12, 10),
            Margin = new Thickness(16, 2, 8, 2),
            Content = _r18Label,
            GestureRecognizers =
            {
                new TapGestureRecognizer
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
                            Services.Logging.AppLog.Get<AppShell>().LogWarning(ex, "Flyout navigation failed (R-18)");
                        }
                    })
                }
            }
        };

        _settingsLabel = new Label { Text = "Settings", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold, VerticalOptions = LayoutOptions.Center };
        _settingsButtonBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.ButtonCornerRadius },
            StrokeThickness = 0,
            Padding = new Thickness(12, 10),
            Margin = new Thickness(8, 2, 8, 2),
            Content = _settingsLabel,
            GestureRecognizers =
            {
                new TapGestureRecognizer
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
                            Services.Logging.AppLog.Get<AppShell>().LogWarning(ex, "Flyout navigation failed (Settings)");
                        }
                    })
                }
            }
        };

        var flyoutStack = new VerticalStackLayout
        {
            Padding = new Thickness(12, 20, 12, 12),
            Spacing = AppLayout.SpacingSmall,
            Children =
            {
                new Label
                {
                    Text = "Synesthesia Audio",
                    FontSize = AppFontSizes.GroupTitle,
                    FontAttributes = AppFonts.Bold,
                    Padding = new Thickness(8, 8, 8, 16),
                    VerticalOptions = LayoutOptions.Center
                },

                new BoxView
                {
                    HeightRequest = 1,
                    Margin = new Thickness(4, 0, 4, 12)
                }
                .AppThemeBinding(BoxView.ColorProperty, AppColors.StrokeAltLight, AppColors.StrokeAltDark),

                new Label
                {
                    Text = "LIBRARY",
                    FontSize = AppFontSizes.Caption,
                    FontAttributes = AppFonts.Bold,
                    CharacterSpacing = 1.2,
                    Margin = new Thickness(12, 6, 0, 6)
                }
                .AppThemeBinding(Label.TextColorProperty, AppColors.TextTertiaryLight, AppColors.TextTertiaryAltDark),

                _allAgeButtonBorder,
                _r18ButtonBorder,

                new BoxView
                {
                    HeightRequest = 1,
                    Margin = new Thickness(4, 12, 4, 12)
                }
                .AppThemeBinding(BoxView.ColorProperty, AppColors.StrokeAltLight, AppColors.StrokeAltDark),

                _settingsButtonBorder
            }
        };

        return new ScrollView { Content = flyoutStack };
    }

    private void UpdateFlyoutSelection()
    {
        bool isSettings = CurrentState?.Location?.OriginalString?.Contains("Settings") == true;
        if (isSettings)
        {
            SetSelectedNav("Settings");
            return;
        }

        if (_mainViewModel.SelectedFilter == "NSFW")
        {
            SetSelectedNav("R18");
        }
        else
        {
            SetSelectedNav("AllAge");
        }
    }

    private void SetSelectedNav(string nav)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        var activeBg = isDark ? AppColors.ActiveBgDark : AppColors.ActiveBgLight;
        var activeText = isDark ? AppColors.ActiveTextDark : AppColors.ActiveTextLight;

        var normalBg = Colors.Transparent;
        var normalText = isDark ? Colors.White : AppColors.NormalTextLight;

        bool isAllAge = nav == "AllAge";
        _allAgeButtonBorder.BackgroundColor = isAllAge ? activeBg : normalBg;
        _allAgeLabel.TextColor = isAllAge ? activeText : normalText;

        bool isR18 = nav == "R18";
        _r18ButtonBorder.BackgroundColor = isR18 ? activeBg : normalBg;
        _r18Label.TextColor = isR18 ? activeText : normalText;

        bool isSettings = nav == "Settings";
        _settingsButtonBorder.BackgroundColor = isSettings ? activeBg : normalBg;
        _settingsLabel.TextColor = isSettings ? activeText : normalText;
    }
}
