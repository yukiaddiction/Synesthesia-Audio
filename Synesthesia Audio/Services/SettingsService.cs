using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services.Storage;

namespace Synesthesia_Audio.Services;

public class SettingsService
{
    private const string CloudPasswordKey = "CloudPassword";
    private const string TranslationApiKeyKey = "TranslationApiKey";

    private readonly string _settingsFilePath;
    private readonly ILogger<SettingsService> _logger;
    private readonly ISecureStore _secureStore;
    private readonly object _passwordLoadLock = new();
    private readonly object _settingsLock = new();
    private readonly object _apiKeyLoadLock = new();
    private Task? _passwordLoadTask;
    private Task? _apiKeyLoadTask;
    private GeneralSettings _currentSettings = new();
    public GeneralSettings CurrentSettings
    {
        get { lock (_settingsLock) return _currentSettings; }
        private set { lock (_settingsLock) _currentSettings = value; }
    }

    /// <summary>Non-null when the platform could not persist a secret, so the UI can say so.</summary>
    public string? SecureStoreWarning => _secureStore.LastError;

    /// <summary>False when secrets are held for this session only.</summary>
    public bool IsSecureStoragePersistent => _secureStore.IsPersistent;

    public SettingsService(ILogger<SettingsService> logger, ISecureStore secureStore)
    {
        _logger = logger;
        _secureStore = secureStore;

        string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsFilePath = Path.Combine(appDataDir, "SynesthesiaAudio_Settings.json");
        CurrentSettings = new GeneralSettings();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync().ConfigureAwait(false);
        await EnsurePasswordLoadedAsync().ConfigureAwait(false);
        await EnsureApiKeyLoadedAsync().ConfigureAwait(false);
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
                _logger.LogInformation("Settings loaded — storage: {Storage}, darkMode: {DarkMode}, basePath set: {HasPath}, translation: {Provider}",
                    settings.StorageType, settings.IsDarkMode, !string.IsNullOrEmpty(settings.BaseAudioFolderPath), settings.TranslationProvider);
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
                CloudPassword = _currentSettings.CloudPassword,
                TranslationProvider = _currentSettings.TranslationProvider,
                TranslationCustomBaseUrl = _currentSettings.TranslationCustomBaseUrl,
                TranslationCustomAuthHeader = _currentSettings.TranslationCustomAuthHeader,
                TranslationApiKey = _currentSettings.TranslationApiKey
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

    public Task EnsureApiKeyLoadedAsync()
    {
        lock (_apiKeyLoadLock)
        {
            _apiKeyLoadTask ??= LoadApiKeyAsync();
            return _apiKeyLoadTask;
        }
    }

    private async Task LoadPasswordAsync()
    {
        try
        {
            var password = await _secureStore.GetAsync(CloudPasswordKey).ConfigureAwait(false);
            if (string.IsNullOrEmpty(CurrentSettings.CloudPassword))
            {
                CurrentSettings.CloudPassword = password ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadPasswordAsync: secure store read failed");
        }
    }

    private async Task LoadApiKeyAsync()
    {
        try
        {
            var apiKey = await _secureStore.GetAsync(TranslationApiKeyKey).ConfigureAwait(false);
            if (string.IsNullOrEmpty(CurrentSettings.TranslationApiKey))
            {
                CurrentSettings.TranslationApiKey = apiKey ?? string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadApiKeyAsync: secure store read failed");
        }
    }

    public void SaveSettings(GeneralSettings settings)
    {
        CurrentSettings = settings;

        lock (_passwordLoadLock)
        {
            _passwordLoadTask = Task.CompletedTask;
        }
        lock (_apiKeyLoadLock)
        {
            _apiKeyLoadTask = Task.CompletedTask;
        }

        _ = PersistSecretAsync(CloudPasswordKey, settings.CloudPassword);
        _ = PersistSecretAsync(TranslationApiKeyKey, settings.TranslationApiKey);

        // serialize a clone instead of mutating the live settings object. the model has
        // [JsonIgnore] on the secret fields, but we set them to empty here anyway just in
        // case something bypasses the attribute.
        var clone = new GeneralSettings
        {
            BaseAudioFolderPath = settings.BaseAudioFolderPath,
            IsDarkMode = settings.IsDarkMode,
            TranslateDlsiteTags = settings.TranslateDlsiteTags,
            StorageType = settings.StorageType,
            CloudServerUrl = settings.CloudServerUrl,
            CloudUsername = settings.CloudUsername,
            CloudPassword = string.Empty,
            TranslationProvider = settings.TranslationProvider,
            TranslationCustomBaseUrl = settings.TranslationCustomBaseUrl,
            TranslationCustomAuthHeader = settings.TranslationCustomAuthHeader,
            TranslationApiKey = string.Empty
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
        _logger.LogInformation("Settings saved — storage: {Storage}, darkMode: {DarkMode}, translateTags: {TranslateTags}, translation: {Provider}",
            settings.StorageType, settings.IsDarkMode, settings.TranslateDlsiteTags, settings.TranslationProvider);
    }

    private async Task PersistSecretAsync(string key, string value)
    {
        try
        {
            if (!await _secureStore.SetAsync(key, value).ConfigureAwait(false))
            {
                _logger.LogWarning("PersistSecretAsync: secure store rejected the write for {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PersistSecretAsync: secure store write failed for {Key}", key);
        }
    }
}
