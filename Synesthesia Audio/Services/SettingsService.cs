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

    /// <summary>
    /// A copy, so callers can't mutate shared state behind the lock; write through <see cref="UpdateSettings"/>.
    /// </summary>
    public GeneralSettings CurrentSettings
    {
        get { lock (_settingsLock) return Clone(_currentSettings); }
    }

    public void UpdateSettings(Action<GeneralSettings> mutate)
    {
        if (mutate == null) return;

        GeneralSettings snapshot;
        lock (_settingsLock)
        {
            mutate(_currentSettings);
            snapshot = Clone(_currentSettings);
            _savedSinceConstruction = true;
        }

        // Secrets are [JsonIgnore]d in the document, so they go to the secure store instead.
        _ = PersistSecretAsync(CloudPasswordKey, snapshot.CloudPassword);
        _ = PersistSecretAsync(TranslationApiKeyKey, snapshot.TranslationApiKey);

        ResetSecretLoadTasks();

        _store.Replace(snapshot);

        _logger.LogInformation("Settings updated — storage: {Storage}, darkMode: {DarkMode}, translateTags: {TranslateTags}, translation: {Provider}",
            snapshot.StorageType, snapshot.IsDarkMode, snapshot.TranslateDlsiteTags, snapshot.TranslationProvider);
    }

    /// <summary>No save: used when loading a secret, which must not write back what was just read.</summary>
    public void UpdateSettingsInMemory(Action<GeneralSettings> mutate)
    {
        if (mutate == null) return;
        lock (_settingsLock) mutate(_currentSettings);
    }

    private void ResetSecretLoadTasks()
    {
        lock (_passwordLoadLock) _passwordLoadTask = Task.CompletedTask;
        lock (_apiKeyLoadLock) _apiKeyLoadTask = Task.CompletedTask;
    }

    public string? SecureStoreWarning => _secureStore.LastError;

    /// <summary>False when secrets are held for this session only.</summary>
    public bool IsSecureStoragePersistent => _secureStore.IsPersistent;

    public SettingsService(ILogger<SettingsService> logger, ISecureStore secureStore)
    {
        _logger = logger;
        _secureStore = secureStore;

        string appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsFilePath = Path.Combine(appDataDir, "SynesthesiaAudio_Settings.json");

        // No I/O here: Loaded reads the document, and startup awaits it before the UI reads any setting.
        _store = new JsonFileStore<GeneralSettings>(_settingsFilePath, saveDebounceMs: 250);
        _store.LoadFailed += ex => _logger.LogError(ex, "Settings could not be read — the previous file was kept as .bak");
        _store.SaveFailed += ex => _logger.LogError(ex, "SaveSettings: failed to write settings file");
    }

    /// <summary>
    /// Covers settings and both secrets. Startup awaits it before showing the UI, so a late load can't overwrite an
    /// early save.
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
            var persisted = await _store.Loaded.ConfigureAwait(false);

            bool adopt;
            lock (_settingsLock)
            {
                // A save during the load is authoritative; adopting the file copy would undo it.
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
            UpdateSettingsInMemory(s => s.CloudPassword = password ?? string.Empty);
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
            UpdateSettingsInMemory(s => s.TranslationApiKey = apiKey ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LoadApiKeyAsync: secure store read failed");
        }
    }

    public void SaveSettings(GeneralSettings settings)
    {
        lock (_settingsLock) _currentSettings = settings;

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

        // A copy, so a later mutation of the live object can't tear a concurrent write.
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
