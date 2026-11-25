using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
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
    private readonly JsonFileStore<GeneralSettings> _store;
    private readonly object _passwordLoadLock = new();
    private readonly object _settingsLock = new();
    private readonly object _apiKeyLoadLock = new();
    private Task? _passwordLoadTask;
    private Task? _apiKeyLoadTask;
    private Task? _loadTask;
    private bool _savedSinceConstruction;
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

        // No I/O here on purpose: the document is read by Loaded, which startup awaits before the UI
        // reads any setting. The store owns its own copy of the document, so UI code that mutates
        // CurrentSettings can never race a save.
        _store = new JsonFileStore<GeneralSettings>(_settingsFilePath, saveDebounceMs: 250);
        _store.LoadFailed += ex => _logger.LogError(ex, "Settings could not be read — the previous file was kept as .bak");
        _store.SaveFailed += ex => _logger.LogError(ex, "SaveSettings: failed to write settings file");
    }

    /// <summary>
    /// Completes once settings and both secrets have been read. Startup awaits this before showing the
    /// UI, so an early save cannot be overwritten by a late load.
    /// </summary>
    public Task Loaded
    {
        get
        {
            lock (_settingsLock)
            {
                return _loadTask ??= InitializeAsync();
            }
        }
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Carries any settings saved while the read was in flight (see JsonFileStore).
            var persisted = await _store.Loaded.ConfigureAwait(false);

            bool adopt;
            lock (_settingsLock)
            {
                // A save made during the load already put the authoritative values in CurrentSettings;
                // adopting the file copy would undo it.
                adopt = !_savedSinceConstruction;
                if (adopt) _currentSettings = Clone(persisted);
            }

            if (adopt)
            {
                _logger.LogInformation("Settings loaded — storage: {Storage}, darkMode: {DarkMode}, basePath set: {HasPath}, translation: {Provider}",
                    persisted.StorageType, persisted.IsDarkMode, !string.IsNullOrEmpty(persisted.BaseAudioFolderPath), persisted.TranslationProvider);
            }
            else
            {
                _logger.LogInformation("Settings load finished after a save — keeping the saved values");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadSettings: deserialization failed, falling back to defaults");
        }

        await EnsurePasswordLoadedAsync().ConfigureAwait(false);
        await EnsureApiKeyLoadedAsync().ConfigureAwait(false);
    }

    private static GeneralSettings Clone(GeneralSettings source) => new()
    {
        BaseAudioFolderPath = source.BaseAudioFolderPath,
        IsDarkMode = source.IsDarkMode,
        TranslateDlsiteTags = source.TranslateDlsiteTags,
        StorageType = source.StorageType,
        CloudServerUrl = source.CloudServerUrl,
        CloudUsername = source.CloudUsername,
        CloudPassword = source.CloudPassword,
        TranslationProvider = source.TranslationProvider,
        TranslationCustomBaseUrl = source.TranslationCustomBaseUrl,
        TranslationCustomAuthHeader = source.TranslationCustomAuthHeader,
        TranslationApiKey = source.TranslationApiKey
    };

    public GeneralSettings GetSettingsSnapshot()
    {
        lock (_settingsLock)
        {
            return Clone(_currentSettings);
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
        lock (_settingsLock)
        {
            _savedSinceConstruction = true;
        }

        _ = PersistSecretAsync(CloudPasswordKey, settings.CloudPassword);
        _ = PersistSecretAsync(TranslationApiKeyKey, settings.TranslationApiKey);

        // Persist a copy: GeneralSettings carries [JsonIgnore] on the secrets, and keeping the store's
        // document distinct from the live object means a later mutation cannot tear a concurrent write.
        _store.Replace(Clone(settings));

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
