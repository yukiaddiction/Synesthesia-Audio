using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views;

public class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _viewModel;

    public SettingsPage(SettingsViewModel viewModel, AudioService audioService, TagMappingService tagService, LibraryCacheService cacheService, MainViewModel mainViewModel)
    {
        BindingContext = _viewModel = viewModel;

        this.AppThemeBinding(ContentPage.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);
        this.Bind(ContentPage.TitleProperty, nameof(SettingsViewModel.Title));

        Content = new Grid
        {
            Children =
            {
                new Grid
                {
                    RowDefinitions = Rows.Define(Auto, Star),
                    Children =
                    {
                        CreateBanner().Row(0),

                        new ScrollView
                        {
                            Padding = AppLayout.PagePadding,
                            Content = new VerticalStackLayout
                            {
                                Spacing = AppLayout.SpacingSection,
                                Children =
                                {
                                    CreateThemeCard(),
                                    CreateLibrarySourceCard(),
                                    CreateScrapingCard(),
                                    CreateTagManagementCard(tagService, cacheService, mainViewModel),
                                    CreateCacheCard(),
                                    CreateBatteryCard(),
                                    CreateLogsCard()
                                }
                            }
                        }
                        .Row(1)
                    }
                },

                new FloatingPlayerBar(audioService)
            }
        };
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

    private View CreateBanner()
    {
        return new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(20, 18),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingSmall,
                Children =
                {
                    new Label { Text = "Settings", FontSize = AppFontSizes.PageTitle, FontAttributes = AppFonts.Bold }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),
                    new Label { Text = "Manage library source, appearance and diagnostics", FontSize = AppFontSizes.Subtitle }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                }
            }
        }
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceDark);
    }

    private Border CreateCard(View content)
    {
        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.CardCornerRadius },
            StrokeThickness = 1,
            Padding = new Thickness(16),
            Content = content
        }
        .AppThemeBinding(Border.StrokeProperty, AppColors.StrokeAltLight, AppColors.SurfaceDark)
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceDark);
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

    private View CreateThemeCard()
    {
        return CreateCard(new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Auto),
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    Children =
                    {
                        CreateCardTitle("App Theme Mode"),
                        CreateCardCaption("Switch between light and dark UI color schemes")
                    }
                }
                .Column(0),

                new HorizontalStackLayout
                {
                    Spacing = AppLayout.SpacingLarge,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { Text = "Light", VerticalOptions = LayoutOptions.Center }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextSecondaryDark),
                        new Switch { VerticalOptions = LayoutOptions.Center }
                            .Bind(Switch.IsToggledProperty, nameof(SettingsViewModel.IsDarkMode)),
                        new Label { Text = "Dark", VerticalOptions = LayoutOptions.Center }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextPrimaryDark)
                    }
                }
                .Column(1)
            }
        });
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

                new Label { Text = "Storage Source" }
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark),

                new Picker
                {
                    Title = "Select Storage Source",
                    ItemsSource = new List<string> { "Local", "WebDAV" }
                }
                .Bind(Picker.SelectedItemProperty, nameof(SettingsViewModel.StorageType))
                .AppThemeBinding(Picker.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.PickerBgDark),

                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraLarge,
                    Children =
                    {
                        new Label { Text = "Base Audio Folder Path" }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark),

                        new HorizontalStackLayout
                        {
                            Spacing = AppLayout.SpacingExtraLarge,
                            Children =
                            {
                                new Entry { Placeholder = "e.g. D:\\Audio", IsReadOnly = true, HorizontalOptions = LayoutOptions.Fill }
                                    .Bind(Entry.TextProperty, nameof(SettingsViewModel.BaseAudioFolderPath))
                                    .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.PickerBgDark),

                                new Button { Text = "Browse" }
                                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.PickFolderCommand))
                                    .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark)
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
                        new Label { Text = "WebDAV Server URL" }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark),
                        new Entry { Placeholder = "e.g. https://webdav.pcloud.com" }
                            .Bind(Entry.TextProperty, nameof(SettingsViewModel.CloudServerUrl))
                            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.PickerBgDark),

                        new Label { Text = "Username" }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark),
                        new Entry { Placeholder = "e.g. user@email.com" }
                            .Bind(Entry.TextProperty, nameof(SettingsViewModel.CloudUsername))
                            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.PickerBgDark),

                        new Label { Text = "Password / Token" }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark),
                        new Entry { IsPassword = true, Placeholder = "App-specific password" }
                            .Bind(Entry.TextProperty, nameof(SettingsViewModel.CloudPassword))
                            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.PickerBgDark),

                        new Label { Text = "Remote Library Folder Path" }
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark),

                        new HorizontalStackLayout
                        {
                            Spacing = AppLayout.SpacingExtraLarge,
                            Children =
                            {
                                new Entry { Placeholder = "e.g. /Audio (leave blank for root)", HorizontalOptions = LayoutOptions.Fill }
                                    .Bind(Entry.TextProperty, nameof(SettingsViewModel.BaseAudioFolderPath))
                                    .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.PickerBgDark),

                                new Button { Text = "Browse" }
                                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.BrowseCloudFolderCommand))
                                    .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark)
                            }
                        },

                        new Button { Text = "Test Connection", HorizontalOptions = LayoutOptions.Start }
                            .Bind(Button.CommandProperty, nameof(SettingsViewModel.TestCloudConnectionCommand))
                            .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.SuccessLight, AppColors.SuccessAccentDark)
                    }
                }
                .Bind(VisualElement.IsVisibleProperty, nameof(SettingsViewModel.IsWebDavSelected)),

                new Button { Text = "Save & Rescan Library", Margin = new Thickness(0, 5, 0, 0) }
                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.SaveSettingsCommand))
                    .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
            }
        });
    }

    private View CreateScrapingCard()
    {
        return CreateCard(new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Auto),
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingExtraSmall,
                    Children =
                    {
                        CreateCardTitle("Scraping"),
                        CreateCardCaption("Translate DLsite tags to English during library scans")
                    }
                }
                .Column(0),

                new Switch { VerticalOptions = LayoutOptions.Center }
                    .Bind(Switch.IsToggledProperty, nameof(SettingsViewModel.TranslateDlsiteTags))
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
                    Spacing = AppLayout.SpacingExtraLarge,
                    Children =
                    {
                        new Button { Text = "Manage Tag Mappings" }
                            .Invoke(b => b.Clicked += async (_, _) =>
                            {
                                try
                                {
                                    await Shell.Current.Navigation.PushAsync(new TagMappingPage(tagService, cacheService, mainViewModel));
                                }
                                catch (Exception ex)
                                {
                                    Services.Logging.AppLog.Get<SettingsPage>().LogError(ex, "Failed to open tag mapping page");
                                }
                            })
                            .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark),

                        new Button { Text = "Clean & Reset Bad Tags" }
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
                            .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.WarningLight, AppColors.WarningDark)
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

                new Button { Text = "Reset RJ Works Cache", BackgroundColor = Colors.DarkRed, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Start }
                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.ResetRjCacheCommand))
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

                new Button { Text = "Configure", VerticalOptions = LayoutOptions.Center }
                    .Bind(Button.CommandProperty, nameof(SettingsViewModel.RequestBatteryExemptionCommand))
                    .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
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
                        new Button { Text = "View Logs", WidthRequest = 110, HeightRequest = AppLayout.ButtonHeightCompact, FontSize = AppFontSizes.ButtonMedium }
                            .Bind(Button.CommandProperty, nameof(SettingsViewModel.ViewLogsCommand))
                            .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark),

                        new Button { Text = "Copy Logs", WidthRequest = 110, HeightRequest = AppLayout.ButtonHeightCompact, FontSize = AppFontSizes.ButtonMedium }
                            .Bind(Button.CommandProperty, nameof(SettingsViewModel.CopyLogsCommand))
                            .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark)
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
            catch { }
            Services.Logging.AppLog.Get<SettingsPage>().LogWarning(ex, "Back navigation failed");
        }
    }
}
