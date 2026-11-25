using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Storage;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Services.Logging;

namespace Synesthesia_Audio.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
    private readonly LibraryCacheService _cacheService;
    private readonly MainViewModel _mainViewModel;
    private readonly LogService _logService;
    private readonly ILogger<SettingsViewModel> _logger;

    private readonly Synesthesia_Audio.Services.Cloud.ICloudProvider _cloudProvider;
    private readonly ITranslationService _translationService;
    private readonly Services.Translation.NameRomanizationService _nameRomanization;

    private bool _suppressInstantSave = true;

    [ObservableProperty]
    private string _baseAudioFolderPath = string.Empty;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private bool _translateDlsiteTags;

    [ObservableProperty]
    private string _storageType = "Local";

    [ObservableProperty]
    private string _cloudServerUrl = string.Empty;

    [ObservableProperty]
    private string _cloudUsername = string.Empty;

    [ObservableProperty]
    private string _cloudPassword = string.Empty;

    [ObservableProperty]
    private bool _isWebDavSelected;

    [ObservableProperty]
    private bool _isLocalSelected = true;

    [ObservableProperty]
    private string _logSummary = string.Empty;

    [ObservableProperty]
    private bool _isBatteryExempt;

    [ObservableProperty]
    private string _translationProvider = TranslationProviders.GTranslate;

    [ObservableProperty]
    private string _translationCustomBaseUrl = string.Empty;

    [ObservableProperty]
    private string _translationApiKey = string.Empty;

    [ObservableProperty]
    private string _translationStatus = string.Empty;

    public string[] TranslationProviderOptions => TranslationProviders.All;

    /// <summary>GTranslate needs no key, so the key and endpoint fields only appear for paid providers.</summary>
    public bool IsTranslationKeyVisible => TranslationProvider != TranslationProviders.GTranslate;

    public bool IsCustomTranslationSelected => TranslationProvider == TranslationProviders.Custom;

    /// <summary>Set when the platform could not persist a secret, so the user is told rather than losing it.</summary>
    public string SecureStorageWarning => _settingsService.SecureStoreWarning ?? string.Empty;

    public bool IsSecureStorageWarningVisible => !string.IsNullOrEmpty(_settingsService.SecureStoreWarning);

    public string BatteryStatusText => IsBatteryExempt ? "Stably Unrestricted" : "Optimized (May stop)";
    public Color BatteryStatusColor => IsBatteryExempt ? Colors.Green : Colors.Orange;

#if ANDROID
    public bool IsBatteryCardVisible => true;
#else
    public bool IsBatteryCardVisible => false;
#endif

    public string AppVersionText => $"Version {AppInfo.Current.VersionString}";

    public SettingsViewModel(SettingsService settingsService, LibraryCacheService cacheService, MainViewModel mainViewModel, Synesthesia_Audio.Services.Cloud.ICloudProvider cloudProvider, LogService logService, ITranslationService translationService, Services.Translation.NameRomanizationService nameRomanization, ILogger<SettingsViewModel> logger)
    {
        Title = "Settings";
        _settingsService = settingsService;
        _cacheService = cacheService;
        _mainViewModel = mainViewModel;
        _cloudProvider = cloudProvider;
        _logService = logService;
        _translationService = translationService;
        _nameRomanization = nameRomanization;
        _logger = logger;

        BaseAudioFolderPath = _settingsService.CurrentSettings.BaseAudioFolderPath;
        IsDarkMode = _settingsService.CurrentSettings.IsDarkMode;
        TranslateDlsiteTags = _settingsService.CurrentSettings.TranslateDlsiteTags;
        StorageType = _settingsService.CurrentSettings.StorageType;
        CloudServerUrl = _settingsService.CurrentSettings.CloudServerUrl;
        CloudUsername = _settingsService.CurrentSettings.CloudUsername;
        CloudPassword = _settingsService.CurrentSettings.CloudPassword;
        TranslationProvider = _settingsService.CurrentSettings.TranslationProvider;
        TranslationCustomBaseUrl = _settingsService.CurrentSettings.TranslationCustomBaseUrl;
        TranslationApiKey = _settingsService.CurrentSettings.TranslationApiKey;

        UpdateSelectionStates();
        _suppressInstantSave = false;

        LogSummary = _logService.GetLogSummary();
        CheckBatteryStatus();
        _ = LoadPasswordIntoFormAsync();
        _ = LoadTranslationKeyIntoFormAsync();

        _logger.LogInformation("SettingsViewModel initialized — storage: {Storage}, darkMode: {DarkMode}, translateTags: {TranslateTags}, translation: {Provider}",
            StorageType, IsDarkMode, TranslateDlsiteTags, TranslationProvider);
    }

    private async Task LoadPasswordIntoFormAsync()
    {
        try
        {
            await _settingsService.EnsurePasswordLoadedAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (string.IsNullOrEmpty(CloudPassword))
                    CloudPassword = _settingsService.CurrentSettings.CloudPassword;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cloud password into settings form");
        }
    }

    private async Task LoadTranslationKeyIntoFormAsync()
    {
        try
        {
            await _settingsService.EnsureApiKeyLoadedAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (string.IsNullOrEmpty(TranslationApiKey))
                    TranslationApiKey = _settingsService.CurrentSettings.TranslationApiKey;

                OnPropertyChanged(nameof(SecureStorageWarning));
                OnPropertyChanged(nameof(IsSecureStorageWarningVisible));
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load translation API key into settings form");
        }
    }

    public void RefreshStatus()
    {
        LogSummary = _logService.GetLogSummary();
        CheckBatteryStatus();
        OnPropertyChanged(nameof(SecureStorageWarning));
        OnPropertyChanged(nameof(IsSecureStorageWarningVisible));
        _logger.LogDebug("Settings status refreshed — logs: {LogSummary}, batteryExempt: {BatteryExempt}", LogSummary, IsBatteryExempt);
    }

    partial void OnTranslationProviderChanged(string value)
    {
        OnPropertyChanged(nameof(IsTranslationKeyVisible));
        OnPropertyChanged(nameof(IsCustomTranslationSelected));

        if (_suppressInstantSave) return;

        _settingsService.CurrentSettings.TranslationProvider = value;
        _settingsService.SaveSettings(_settingsService.CurrentSettings);
        _logger.LogInformation("Translation provider changed to {Provider}", value);
    }

    /// <summary>
    /// Opens the About &amp; Sources screen. The EDRDG licence requires attribution on a screen
    /// reachable from a menu, not on a splash page, and it has to show how fresh the name data is.
    /// </summary>
    [RelayCommand]
    private async Task OpenAboutAsync()
    {
        try
        {
            var created = await _nameRomanization.GetBundledDataCreatedAsync();
            await Shell.Current.Navigation.PushModalAsync(new Views.AboutSourcesPage(AppInfo.Current.VersionString, created));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not open the About & Sources screen");
        }
    }

    /// <summary>
    /// Translates one probe string through whatever is typed in the form, then restores the saved
    /// values — the same swap-test-restore shape the WebDAV connection test already uses.
    /// </summary>
    [RelayCommand]
    private async Task TestTranslationAsync()
    {
        var originalProvider = _settingsService.CurrentSettings.TranslationProvider;
        var originalBaseUrl = _settingsService.CurrentSettings.TranslationCustomBaseUrl;
        var originalHeader = _settingsService.CurrentSettings.TranslationCustomAuthHeader;
        var originalKey = _settingsService.CurrentSettings.TranslationApiKey;

        _settingsService.CurrentSettings.TranslationProvider = TranslationProvider;
        _settingsService.CurrentSettings.TranslationCustomBaseUrl = TranslationCustomBaseUrl;
        _settingsService.CurrentSettings.TranslationApiKey = TranslationApiKey;

        TranslationStatus = "Testing…";
        try
        {
            _translationService.ClearError();
            var translated = await _translationService.TranslateJaToEnAsync("催眠音声");

            if (_translationService.RateLimitRemaining is { } wait)
                TranslationStatus = $"Rate limited — try again in {(int)Math.Ceiling(wait.TotalSeconds)} seconds.";
            else if (!string.IsNullOrWhiteSpace(translated))
                TranslationStatus = $"OK via {_translationService.ActiveProviderName}: {translated}";
            else
                TranslationStatus = _translationService.LastError ?? "No translation returned.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Translation test failed");
            TranslationStatus = $"Failed: {ex.Message}";
        }
        finally
        {
            _settingsService.CurrentSettings.TranslationProvider = originalProvider;
            _settingsService.CurrentSettings.TranslationCustomBaseUrl = originalBaseUrl;
            _settingsService.CurrentSettings.TranslationCustomAuthHeader = originalHeader;
            _settingsService.CurrentSettings.TranslationApiKey = originalKey;
        }
    }

    private void UpdateSelectionStates()
    {
        IsLocalSelected = StorageType == "Local";
        IsWebDavSelected = StorageType == "WebDAV";
    }

    partial void OnStorageTypeChanged(string value)
    {
        UpdateSelectionStates();
        if (!_suppressInstantSave)
            _logger.LogInformation("Storage type selection changed to {StorageType}", value);
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        if (_suppressInstantSave) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Application.Current != null)
                Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
        });

        _settingsService.CurrentSettings.IsDarkMode = value;
        _settingsService.SaveSettings(_settingsService.CurrentSettings);
        _logger.LogInformation("App theme changed to {Theme}", value ? "Dark" : "Light");
    }

    partial void OnTranslateDlsiteTagsChanged(bool value)
    {
        if (_suppressInstantSave) return;

        _settingsService.CurrentSettings.TranslateDlsiteTags = value;
        _settingsService.SaveSettings(_settingsService.CurrentSettings);
        _logger.LogInformation("Translate DLsite tags setting changed to {Value}", value);
    }

    partial void OnIsBatteryExemptChanged(bool value)
    {
        OnPropertyChanged(nameof(BatteryStatusText));
        OnPropertyChanged(nameof(BatteryStatusColor));
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        _logger.LogInformation("Saving library settings — storage: {Storage}, basePath: {BasePath}, serverUrl: {ServerUrl}",
            StorageType, BaseAudioFolderPath, CloudServerUrl);

        if (!await ConfirmInsecureCloudUrlAsync()) return;

        _settingsService.CurrentSettings.BaseAudioFolderPath = BaseAudioFolderPath;
        _settingsService.CurrentSettings.IsDarkMode = IsDarkMode;
        _settingsService.CurrentSettings.TranslateDlsiteTags = TranslateDlsiteTags;
        _settingsService.CurrentSettings.StorageType = StorageType;
        _settingsService.CurrentSettings.CloudServerUrl = CloudServerUrl;
        _settingsService.CurrentSettings.CloudUsername = CloudUsername;
        _settingsService.CurrentSettings.CloudPassword = CloudPassword;
        _settingsService.CurrentSettings.TranslationProvider = TranslationProvider;
        _settingsService.CurrentSettings.TranslationCustomBaseUrl = TranslationCustomBaseUrl;
        _settingsService.CurrentSettings.TranslationApiKey = TranslationApiKey;
        _settingsService.SaveSettings(_settingsService.CurrentSettings);

        if (Application.Current != null)
        {
            Application.Current.UserAppTheme = IsDarkMode ? AppTheme.Dark : AppTheme.Light;
        }

        await _mainViewModel.InitializeAsync();
        _logger.LogInformation("Library settings saved and library re-initialized");
    }

    /// <summary>
    /// A WebDAV server reached over <c>http://</c> receives the username and password as Basic auth in
    /// clear text. Saving such a URL now requires an explicit confirmation rather than happening silently.
    /// </summary>
    private async Task<bool> ConfirmInsecureCloudUrlAsync()
    {
        if (!string.Equals(StorageType, "WebDAV", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrWhiteSpace(CloudServerUrl)) return true;
        if (CloudServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;

        if (!CloudServerUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return true; // Not a URL we can reason about; the connection test will report it.
        }

        _logger.LogWarning("WebDAV server URL is not using HTTPS — asking the user to confirm");

        var confirmed = await Shell.Current.DisplayAlert(
            "Unencrypted connection",
            "This WebDAV address uses http://, not https://.\n\n" +
            "Your username and password are sent as Basic authentication with no encryption, so anyone on " +
            "the network path can read them. They can also copy or modify the files you stream.\n\n" +
            "Android also blocks plain http to non-local addresses, so this will only work on desktop.\n\n" +
            "Use an https:// address instead, or continue to accept the risk.",
            "Use it anyway",
            "Cancel");

        if (!confirmed)
        {
            _logger.LogInformation("User declined to save an unencrypted WebDAV URL");
        }

        return confirmed;
    }

    [RelayCommand]
    private async Task BrowseCloudFolderAsync()
    {
        _logger.LogInformation("Browsing cloud folders — server: {ServerUrl}", CloudServerUrl);

        // Build the connection from the form instead of overwriting the saved settings. The old
        // swap-then-restore only stayed applied until the picker page was shown, so the first folder tap
        // in the picker went out with stale credentials and browse failed.
        var connection = new Services.Cloud.WebDavConnection(CloudServerUrl, CloudUsername, CloudPassword);

        try
        {
            var result = await _cloudProvider.TestConnectionAsync(connection);
            if (!result.Success)
            {
                _logger.LogWarning("Cloud folder browse aborted — connection test failed: {Error}", result.ErrorMessage);
                await Shell.Current.DisplayAlert("Connection Failed", $"Could not browse folders because the cloud connection test failed:\n\n{result.ErrorMessage}", "OK");
                return;
            }

            var modal = new Views.CloudFolderPickerPage(_cloudProvider, connection, BaseAudioFolderPath, (selectedPath) =>
            {
                BaseAudioFolderPath = selectedPath;
                _logger.LogInformation("Cloud folder selected: {Path}", selectedPath);
            });

            await Shell.Current.Navigation.PushModalAsync(new NavigationPage(modal));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse cloud folders");
            await Shell.Current.DisplayAlert("Browse Error", $"Failed to browse cloud folders: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task TestCloudConnectionAsync()
    {
        _logger.LogInformation("Testing WebDAV connection — server: {ServerUrl}", CloudServerUrl);

        // Test exactly what the user has typed, without touching the saved settings.
        var connection = new Services.Cloud.WebDavConnection(CloudServerUrl, CloudUsername, CloudPassword);

        try
        {
            var result = await _cloudProvider.TestConnectionAsync(connection);
            if (result.Success)
            {
                _logger.LogInformation("WebDAV connection test succeeded — server: {ServerUrl}", CloudServerUrl);
                await Shell.Current.DisplayAlert("Connection Success", "Successfully connected to WebDAV server!", "OK");
            }
            else
            {
                _logger.LogWarning("WebDAV connection test failed — server: {ServerUrl}, error: {Error}", CloudServerUrl, result.ErrorMessage);
                await Shell.Current.DisplayAlert("Connection Failed", $"Could not connect to WebDAV server.\n\nDetails:\n{result.ErrorMessage}", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebDAV connection test error — server: {ServerUrl}", CloudServerUrl);
            await Shell.Current.DisplayAlert("Connection Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    [RelayCommand]
    private async Task PickFolderAsync()
    {
        var result = await FolderPicker.Default.PickAsync(default);
        if (result.IsSuccessful)
        {
            BaseAudioFolderPath = result.Folder.Path;
            _logger.LogInformation("Local library folder picked: {Path}", result.Folder.Path);
        }
        else
        {
            _logger.LogDebug("Local folder pick cancelled or failed");
        }
    }

    [RelayCommand]
    private async Task ResetRjCacheAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert("Reset Cache", "Are you sure you want to clear DLsite scraping cache? This will re-scrape info on next library scan.", "Yes", "No");
        if (confirm)
        {
            _logger.LogInformation("Resetting DLsite RJ works cache");
            _cacheService.ResetRjCache();
            await _mainViewModel.InitializeAsync();
            await Shell.Current.DisplayAlert("Cache Cleared", "DLsite cache has been reset.", "OK");
            _logger.LogInformation("DLsite RJ works cache reset complete");
        }
        else
        {
            _logger.LogDebug("DLsite cache reset cancelled by user");
        }
    }

    [RelayCommand]
    private async Task ViewLogsAsync()
    {
        _logger.LogDebug("Opening log viewer page");
        await Shell.Current.GoToAsync(nameof(Views.LogViewerPage));
    }

    [RelayCommand]
    private async Task CopyLogsAsync()
    {
        await _logService.CopyLogsToClipboardAsync();
        _logger.LogInformation("Application logs copied to clipboard");
        await Shell.Current.DisplayAlert("Copied", "Log content copied to clipboard.", "OK");
    }

    public void CheckBatteryStatus()
    {
#if ANDROID
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                var context = global::Android.App.Application.Context;
                var powerManager = (global::Android.OS.PowerManager?)context.GetSystemService(global::Android.Content.Context.PowerService);
                IsBatteryExempt = powerManager?.IsIgnoringBatteryOptimizations(context.PackageName) ?? false;
            }
            else
            {
                IsBatteryExempt = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Battery optimization status check failed");
            IsBatteryExempt = false;
        }
#else
        IsBatteryExempt = true;
#endif
    }

    [RelayCommand]
    private async Task RequestBatteryExemptionAsync()
    {
#if ANDROID
        try
        {
            _logger.LogInformation("Requesting battery optimization exemption");

            if (!OperatingSystem.IsAndroidVersionAtLeast(23))
            {
                await Shell.Current.DisplayAlert("Not Required", "Battery exemptions are not required on your version of Android.", "OK");
                IsBatteryExempt = true;
                return;
            }

            var context = global::Android.App.Application.Context;
            var powerManager = (global::Android.OS.PowerManager?)context.GetSystemService(global::Android.Content.Context.PowerService);
            bool alreadyExempt = powerManager?.IsIgnoringBatteryOptimizations(context.PackageName) ?? false;

            if (alreadyExempt)
            {
                _logger.LogInformation("Battery optimization already exempt");
                await Shell.Current.DisplayAlert("Battery Optimization", "App is already set to Unrestricted (ignoring battery optimization). It will run stably in the background!", "OK");
                IsBatteryExempt = true;
                return;
            }

            var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(global::Android.Net.Uri.Parse($"package:{context.PackageName}"));
            intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            context.StartActivity(intent);

            // Re-check after a brief moment
            await Task.Delay(2000);
            CheckBatteryStatus();
            _logger.LogInformation("Battery exemption request completed — exempt: {Exempt}", IsBatteryExempt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Battery exemption request failed");
            await Shell.Current.DisplayAlert("Request Failed", $"Could not open Battery settings directly. Please manually set Battery to 'Unrestricted' in App Info: {ex.Message}", "OK");
        }
#else
        await Shell.Current.DisplayAlert("Battery Optimization", "Not required on this platform.", "OK");
#endif
    }
}
