using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Views.Components;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views;

public class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;
    private readonly GlassScaffold _scaffold;
    private readonly VerticalStackLayout _libraryGroup;
    private readonly VerticalStackLayout _appearanceGroup;
    private readonly VerticalStackLayout _diagnosticsGroup;
    private readonly GlassNavItem _libraryNav;
    private readonly GlassNavItem _appearanceNav;
    private readonly GlassNavItem _diagnosticsNav;

    private int _activeGroupIndex;

    public SettingsPage(SettingsViewModel viewModel, AudioService audioService, TagMappingService tagService, LibraryCacheService cacheService, MainViewModel mainViewModel)
    {
        BindingContext = _viewModel = viewModel;

        this.Bind(ContentPage.TitleProperty, nameof(SettingsViewModel.Title));
        Shell.SetNavBarIsVisible(this, false);

        _scaffold = new GlassScaffold("Settings", "Manage library source, appearance and diagnostics", back: true);

        _libraryGroup = new VerticalStackLayout
        {
            Spacing = AppLayout.GlassSectionGap,
            Children = { GlassTheme.SectionLabel("LIBRARY"), CreateLibrarySourceCard(), CreateScrapingCard(), CreateTranslationCard() }
        };

        _appearanceGroup = new VerticalStackLayout
        {
            Spacing = AppLayout.GlassSectionGap,
            Children = { GlassTheme.SectionLabel("APPEARANCE"), CreateTagManagementCard(tagService, cacheService, mainViewModel) }
        };

        _diagnosticsGroup = new VerticalStackLayout
        {
            Spacing = AppLayout.GlassSectionGap,
            Children = { GlassTheme.SectionLabel("DIAGNOSTICS"), CreateCacheCard(), CreateBatteryCard(), CreateLogsCard() }
        };

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.GlassSectionGap,
                Children = { CreateIdentityCard(), _libraryGroup, _appearanceGroup, _diagnosticsGroup }
            }
        };

        _libraryNav = new GlassNavItem("Library", (_, _) => SelectGroup(0));
        _appearanceNav = new GlassNavItem("Appearance", (_, _) => SelectGroup(1));
        _diagnosticsNav = new GlassNavItem("Diagnostics", (_, _) => SelectGroup(2));

        var rail = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius },
            Padding = new Thickness(10),
            IsVisible = false,
            VerticalOptions = LayoutOptions.Start,
            Content = new VerticalStackLayout
            {
                Spacing = 0,
                Children = { _libraryNav, _appearanceNav, _diagnosticsNav }
            }
        }.Glass(strong: true, elevated: true);

        var body = new Grid
        {
            Padding = AppLayout.GlassContentPadding,
            ColumnDefinitions = { new ColumnDefinition(new GridLength(0)), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = AppLayout.SpacingSection,
            Children =
            {
                rail.Column(0),
                scroll.Column(1)
            }
        };

        _scaffold.LayoutModeChanged += (_, wide) =>
        {
            body.ColumnDefinitions[0].Width = wide
                ? new GridLength(AppBreakpoints.SettingsRailWidth)
                : new GridLength(0);

            rail.IsVisible = wide;
            ApplyGroupVisibility(wide);
        };

        _scaffold.SetBody(body);
        _scaffold.AddOverlay(new FloatingPlayerBar(audioService));

        Content = _scaffold;

        SelectGroup(0);
    }

    private void SelectGroup(int index)
    {
        _activeGroupIndex = index;

        _libraryNav.SetActive(index == 0);
        _appearanceNav.SetActive(index == 1);
        _diagnosticsNav.SetActive(index == 2);

        ApplyGroupVisibility(_scaffold.IsWide);
    }

    /// <summary>
    /// Compact keeps every group stacked; wide shows only the group picked on the rail.
    /// </summary>
    private void ApplyGroupVisibility(bool wide)
    {
        var groups = new[] { _libraryGroup, _appearanceGroup, _diagnosticsGroup };
        for (var i = 0; i < groups.Length; i++)
            groups[i].IsVisible = !wide || i == _activeGroupIndex;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            _viewModel.RefreshStatus();
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<SettingsPage>().LogWarning(ex, "SettingsPage OnAppearing error");
        }
    }

    private View CreateIdentityCard()
    {
        return CreateCard(new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Auto),
            ColumnSpacing = AppLayout.SpacingSection,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = "Synesthesia Audio", FontSize = AppFontSizes.GroupTitle, FontAttributes = AppFonts.Bold }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),
                        CreateCardCaption(_viewModel.AppVersionText),

                        GlassTheme.PillButton("About & Sources", AppFontSizes.Caption)
                            .Bind(Button.CommandProperty, nameof(SettingsViewModel.OpenAboutCommand))
                            .Invoke(b => b.HorizontalOptions = LayoutOptions.Start)
                    }
                }
                .Column(0),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingSmall,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.End,
                    Children =
                    {
                        CreateCardCaption("Dark mode"),
                        new Switch { VerticalOptions = LayoutOptions.Center }
                            .Bind(Switch.IsToggledProperty, nameof(SettingsViewModel.IsDarkMode))
                            .GlassSwitch()
                    }
                }
                .Column(1)
            }
        });
    }

    private Border CreateCard(View content)
    {
        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius },
            Padding = AppLayout.GlassCardPadding,
            Content = content
        }.Glass();
    }

    private Label CreateCardTitle(string text)
    {
        return new Label { Text = text, FontSize = AppFontSizes.GroupTitle, FontAttributes = AppFonts.Bold }
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);
    }

    private Label CreateCardCaption(string text)
    {
        return new Label { Text = text, FontSize = AppFontSizes.Caption }
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);
    }

    private Label CreateFormLabel(string text)
    {
        return new Label { Text = text, FontSize = AppFontSizes.Body }
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark);
    }

    private View CreateLibrarySourceCard()
    {
        return CreateCard(new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                CreateCardTitle("Library Source"),
                CreateCardCaption("Choose where your audio works library is stored"),

                CreateFormLabel("Storage Source"),

                new Picker
                {
                    Title = "Select Storage Source",
                    ItemsSource = new List<string> { "Local", "WebDAV" }
                }
                .Bind(Picker.SelectedItemProperty, nameof(SettingsViewModel.StorageType))
                .AppThemeBinding(Picker.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraLarge,
                    Children =
                    {
                        CreateFormLabel("Base Audio Folder Path"),

                        new HorizontalStackLayout
                        {
                            Spacing = AppLayout.SpacingLarge,
                            Children =
                            {
                                new Entry { Placeholder = "e.g. D:\\Audio", IsReadOnly = true, HorizontalOptions = LayoutOptions.Fill }
                                    .Bind(Entry.TextProperty, nameof(SettingsViewModel.BaseAudioFolderPath))
                                    .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                                GlassTheme.PillButton("Browse", AppFontSizes.Body)
                                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.PickFolderCommand))
                            }
                        }
                    }
                }
                .Bind(VisualElement.IsVisibleProperty, nameof(SettingsViewModel.IsLocalSelected)),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraLarge,
                    Children =
                    {
                        CreateFormLabel("WebDAV Server URL"),
                        new Entry { Placeholder = "e.g. https://webdav.pcloud.com" }
                            .Bind(Entry.TextProperty, nameof(SettingsViewModel.CloudServerUrl))
                            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                        CreateFormLabel("Username"),
                        new Entry { Placeholder = "WebDAV username" }
                            .Bind(Entry.TextProperty, nameof(SettingsViewModel.CloudUsername))
                            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                        CreateFormLabel("Password / Token"),
                        new Entry { IsPassword = true, Placeholder = "App-specific password" }
                            .Bind(Entry.TextProperty, nameof(SettingsViewModel.CloudPassword))
                            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                        CreateFormLabel("Remote Library Folder Path"),

                        new HorizontalStackLayout
                        {
                            Spacing = AppLayout.SpacingLarge,
                            Children =
                            {
                                new Entry { Placeholder = "e.g. /Audio (leave blank for root)", HorizontalOptions = LayoutOptions.Fill }
                                    .Bind(Entry.TextProperty, nameof(SettingsViewModel.BaseAudioFolderPath))
                                    .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                                GlassTheme.PillButton("Browse", AppFontSizes.Body)
                                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.BrowseCloudFolderCommand))
                            }
                        },

                        GlassTheme.PillButton("Test Connection", AppFontSizes.Body, accent: true)
                            .Bind(Button.CommandProperty, nameof(SettingsViewModel.TestCloudConnectionCommand))
                            .Invoke(b => b.HorizontalOptions = LayoutOptions.Start)
                    }
                }
                .Bind(VisualElement.IsVisibleProperty, nameof(SettingsViewModel.IsWebDavSelected)),

                GlassTheme.PillButton("Save & Rescan Library", AppFontSizes.BodyBold, accent: true, height: AppLayout.ButtonHeightDefault)
                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.SaveSettingsCommand))
                    .Invoke(b => b.Margin = new Thickness(0, 4, 0, 0))
            }
        });
    }

    private View CreateTranslationCard()
    {
        return CreateCard(new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                CreateCardTitle("Translation"),
                CreateCardCaption("Free Google/Bing chain by default. Switch to your own paid API key for better quality. " +
                                  "Anything translated — tag names, titles and track titles — is sent to those services, " +
                                  "so translating is only done for a work you choose, or during a scan when tag " +
                                  "translation is switched on below."),

                CreateFormLabel("Provider"),

                new Picker
                {
                    Title = "Select Translation Provider",
                    ItemsSource = _viewModel.TranslationProviderOptions
                }
                .Bind(Picker.SelectedItemProperty, nameof(SettingsViewModel.TranslationProvider))
                .AppThemeBinding(Picker.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraLarge,
                    Children =
                    {
                        CreateFormLabel("API Key"),
                        new Entry { IsPassword = true, Placeholder = "DeepL key (ends with :fx) or your provider key" }
                            .Bind(Entry.TextProperty, nameof(SettingsViewModel.TranslationApiKey))
                            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark),

                        new VerticalStackLayout
                        {
                            Spacing = AppLayout.SpacingExtraLarge,
                            Children =
                            {
                                CreateFormLabel("Custom Base URL"),
                                new Entry { Placeholder = "e.g. https://api-free.deepl.com" }
                                    .Bind(Entry.TextProperty, nameof(SettingsViewModel.TranslationCustomBaseUrl))
                                    .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark)
                            }
                        }
                        .Bind(VisualElement.IsVisibleProperty, nameof(SettingsViewModel.IsCustomTranslationSelected))
                    }
                }
                .Bind(VisualElement.IsVisibleProperty, nameof(SettingsViewModel.IsTranslationKeyVisible)),

                new Label { FontSize = AppFontSizes.Caption, LineBreakMode = LineBreakMode.WordWrap }
                    .Bind(Label.TextProperty, nameof(SettingsViewModel.SecureStorageWarning))
                    .Bind(VisualElement.IsVisibleProperty, nameof(SettingsViewModel.IsSecureStorageWarningVisible))
                    .AppThemeBinding(Label.TextColorProperty, AppColors.DangerLight, AppColors.DangerDark),

                GlassTheme.PillButton("Test Translation", AppFontSizes.Body, accent: true)
                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.TestTranslationCommand))
                    .Invoke(b => b.HorizontalOptions = LayoutOptions.Start),

                CreateCardCaption(string.Empty)
                    .Bind(Label.TextProperty, nameof(SettingsViewModel.TranslationStatus))
            }
        });
    }

    private View CreateScrapingCard()
    {
        return CreateCard(new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Auto),
            ColumnSpacing = AppLayout.SpacingSection,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    Children =
                    {
                        CreateCardTitle("Scraping"),
                        CreateCardCaption("Translate DLsite tags to English during library scans. This sends the " +
                                          "unmapped Japanese tags to Google/Bing. Leave it off to keep your library " +
                                          "local — already-mapped tags still translate without any network call.")
                    }
                }
                .Column(0),

                new Switch { VerticalOptions = LayoutOptions.Center }
                    .Bind(Switch.IsToggledProperty, nameof(SettingsViewModel.TranslateDlsiteTags))
                    .GlassSwitch()
                    .Column(1)
            }
        });
    }

    private View CreateTagManagementCard(TagMappingService tagService, LibraryCacheService cacheService, MainViewModel mainViewModel)
    {
        return CreateCard(new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                CreateCardTitle("Tag & Genre Management"),
                CreateCardCaption("Maintain the Japanese-to-English tag dictionary used by the library"),

                new HorizontalStackLayout
                {
                    Spacing = AppLayout.SpacingLarge,
                    Children =
                    {
                        GlassTheme.PillButton("Manage Tag Mappings", AppFontSizes.Body, accent: true)
                            .Invoke(b => b.Clicked += async (_, _) =>
                            {
                                try
                                {
                                    // Guarded: a double click used to push the page twice.
                                    await NavigationGuard.RunAsync(() =>
                                        Shell.Current.Navigation.PushAsync(new TagMappingPage(tagService, cacheService, mainViewModel)));
                                }
                                catch (Exception ex)
                                {
                                    Services.Logging.AppLog.Get<SettingsPage>().LogError(ex, "Failed to open tag mapping page");
                                }
                            }),

                        GlassTheme.PillButton("Clean & Reset Bad Tags", AppFontSizes.Body)
                            .Invoke(b => b.Clicked += async (_, _) =>
                            {
                                try
                                {
                                    cacheService.CleanAndReMapAllWorkGenres(tagService);
                                    await mainViewModel.InitializeAsync();
                                    await DisplayAlert("Tags Cleaned", "Successfully removed bad dictionary entries and re-mapped all library work tags to clean English!", "OK");
                                }
                                catch (Exception ex)
                                {
                                    Services.Logging.AppLog.Get<SettingsPage>().LogError(ex, "Tag clean failed");
                                }
                            })
                    }
                }
            }
        });
    }

    private View CreateCacheCard()
    {
        return CreateCard(new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                CreateCardTitle("Cache Management"),
                CreateCardCaption("Clear scraped DLsite metadata so it is fetched again on the next scan"),

                new Button { Text = "Reset RJ Works Cache", HorizontalOptions = LayoutOptions.Start }
                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.ResetRjCacheCommand))
                    .AppThemeBinding(Button.TextColorProperty, AppColors.DangerTextLight, AppColors.DangerTextDark)
                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.DangerBgLight, AppColors.DangerBgDark)
            }
        });
    }

    private View CreateBatteryCard()
    {
        return CreateCard(new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Auto),
            ColumnSpacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    Children =
                    {
                        CreateCardTitle("Background Stability"),
                        CreateCardCaption("Exclude app from battery optimizations to prevent audio playback from stopping in the background."),
                        new HorizontalStackLayout
                        {
                            Spacing = AppLayout.SpacingSmall,
                            Margin = new Thickness(0, 5, 0, 0),
                            Children =
                            {
                                new Label { Text = "Status: ", FontSize = AppFontSizes.Caption, FontAttributes = AppFonts.Bold }
                                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),
                                new Label { FontSize = AppFontSizes.Caption, FontAttributes = AppFonts.Bold }
                                    .Bind(Label.TextProperty, nameof(SettingsViewModel.BatteryStatusText))
                                    .Bind(Label.TextColorProperty, nameof(SettingsViewModel.BatteryStatusColor))
                            }
                        }
                    }
                }
                .Column(0),

                GlassTheme.PillButton("Configure", AppFontSizes.Body, accent: true)
                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.RequestBatteryExemptionCommand))
                    .Invoke(b => b.VerticalOptions = LayoutOptions.Center)
                    .Column(1)
            }
        })
        .Bind(VisualElement.IsVisibleProperty, nameof(SettingsViewModel.IsBatteryCardVisible));
    }

    private View CreateLogsCard()
    {
        return CreateCard(new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Auto),
            ColumnSpacing = AppLayout.SpacingExtraLarge,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    Children =
                    {
                        CreateCardTitle("Application Logs"),
                        CreateCardCaption("Review diagnostics for scans, playback, cloud streaming and scraping"),
                        new Label { FontSize = AppFontSizes.Caption }
                            .Bind(Label.TextProperty, nameof(SettingsViewModel.LogSummary))
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                    }
                }
                .Column(0),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingLarge,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        GlassTheme.PillButton("View Logs", AppFontSizes.ButtonMedium, accent: true, height: AppLayout.ButtonHeightCompact)
                            .Bind(Button.CommandProperty, nameof(SettingsViewModel.ViewLogsCommand))
                            .Invoke(b => b.WidthRequest = 110),

                        GlassTheme.PillButton("Copy Logs", AppFontSizes.ButtonMedium, height: AppLayout.ButtonHeightCompact)
                            .Bind(Button.CommandProperty, nameof(SettingsViewModel.CopyLogsCommand))
                            .Invoke(b => b.WidthRequest = 110)
                    }
                }
                .Column(1)
            }
        });
    }

    protected override bool OnBackButtonPressed()
    {
        _ = GoBackAsync();
        return true;
    }

    private static async Task GoBackAsync()
    {
        try
        {
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.Navigation.PopAsync();
            else
                await Shell.Current.GoToAsync("//Library");
        }
        catch (Exception ex)
        {
            try { await Shell.Current.GoToAsync("//Library"); }
            catch (Exception fallbackEx) { Services.Logging.AppLog.Get<SettingsPage>().LogDebug(fallbackEx, "Fallback navigation to the library failed"); }
            Services.Logging.AppLog.Get<SettingsPage>().LogWarning(ex, "Back navigation failed");
        }
    }
}
