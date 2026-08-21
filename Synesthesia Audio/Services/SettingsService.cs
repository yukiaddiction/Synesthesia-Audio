using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;
    private readonly object _passwordLoadLock = new();
    private readonly object _settingsLock = new();
    private Task? _passwordLoadTask;
    private GeneralSettings _currentSettings = new();
    public GeneralSettings CurrentSettings
    {
        get { lock (_settingsLock) return _currentSettings; }
        private set { lock (_settingsLock) _currentSettings = value; }
    }

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsFilePath = Path.Combine(appDataDir, "SynesthesiaAudio_Settings.json");
        CurrentSettings = new GeneralSettings();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync().ConfigureAwait(false);
        await EnsurePasswordLoadedAsync().ConfigureAwait(false);
    }

    private async Task LoadSettingsAsync()
    {
        if (File.Exists(_settingsFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<GeneralSettings>(json) ?? new GeneralSettings();

                CurrentSettings = settings;
                _logger.LogInformation("Settings loaded — storage: {Storage}, darkMode: {DarkMode}, basePath set: {HasPath}",
                    settings.StorageType, settings.IsDarkMode, !string.IsNullOrEmpty(settings.BaseAudioFolderPath));
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LoadSettings: deserialization failed, falling back to defaults");
            }
        }

        _logger.LogInformation("Settings file not found, using default settings");
    }

    public GeneralSettings GetSettingsSnapshot()
    {
        lock (_settingsLock)
        {
            return new GeneralSettings
            {
                BaseAudioFolderPath = _currentSettings.BaseAudioFolderPath,
                IsDarkMode = _currentSettings.IsDarkMode,
                TranslateDlsiteTags = _currentSettings.TranslateDlsiteTags,
                StorageType = _currentSettings.StorageType,
                CloudServerUrl = _currentSettings.CloudServerUrl,
                CloudUsername = _currentSettings.CloudUsername,
                CloudPassword = _currentSettings.CloudPassword
            };
        }
    }

    public Task EnsurePasswordLoadedAsync()
    {
        lock (_passwordLoadLock)
        {
            _passwordLoadTask ??= LoadPasswordAsync();
            return _passwordLoadTask;
        }
    }

    private async Task LoadPasswordAsync()
    {
        try
        {
            var password = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync("CloudPassword").ConfigureAwait(false);
            if (string.IsNullOrEmpty(CurrentSettings.CloudPassword))
            {
                CurrentSettings.CloudPassword = password ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadPasswordAsync: SecureStorage read failed");
        }
    }

    public void SaveSettings(GeneralSettings settings)
    {
        CurrentSettings = settings;

        lock (_passwordLoadLock)
        {
            _passwordLoadTask = Task.CompletedTask;
        }
        _ = PersistPasswordAsync(settings.CloudPassword);

        // serialize a clone instead of mutating the live settings object. the model has
        // [JsonIgnore] on the password field, but we set it to empty here anyway just in
        // case something bypasses the attribute.
        var clone = new GeneralSettings
        {
            BaseAudioFolderPath = settings.BaseAudioFolderPath,
            IsDarkMode = settings.IsDarkMode,
            TranslateDlsiteTags = settings.TranslateDlsiteTags,
            StorageType = settings.StorageType,
            CloudServerUrl = settings.CloudServerUrl,
            CloudUsername = settings.CloudUsername,
            CloudPassword = string.Empty
        };

        var json = JsonSerializer.Serialize(clone);
        try
        {
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveSettings: failed to write settings file");
        }
        _logger.LogInformation("Settings saved — storage: {Storage}, darkMode: {DarkMode}, translateTags: {TranslateTags}",
            settings.StorageType, settings.IsDarkMode, settings.TranslateDlsiteTags);
    }

    private async Task PersistPasswordAsync(string password)
    {
        try
        {
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync("CloudPassword", password).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PersistPasswordAsync: SecureStorage write failed");
        }
    }
}
