using System.Text;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Storage;

/// <summary>
/// Unpackaged Windows builds have no package identity, so MAUI SecureStorage cannot persist there; Windows uses a
/// DPAPI-protected file instead. Other platforms use SecureStorage (Keystore / Keychain).
/// </summary>
public sealed class SecureStore : ISecureStore
{
    private const string FileName = "SynesthesiaAudio_Secure.json";

    private readonly ILogger<SecureStore> _logger;
    private readonly SecureFileStore? _fileStore;
    private readonly Dictionary<string, string> _sessionOnly = new(StringComparer.Ordinal);
    private readonly object _sessionLock = new();

    public string? LastError { get; private set; }
    public bool IsPersistent { get; private set; } = true;

    public SecureStore(ILogger<SecureStore> logger)
    {
        _logger = logger;
#if WINDOWS
        // Own subfolder, so the container does not sit loose among every other app's files in LocalAppData.
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SynesthesiaAudio");
        var path = Path.Combine(directory, FileName);

        MigrateFromLocalAppDataRoot(directory, path);

        _fileStore = new SecureFileStore(path, DpapiProtectAsync, DpapiUnprotectAsync);
#endif
    }

#if WINDOWS
    // Older builds wrote to the LocalAppData root; moving the file keeps the move from looking like lost credentials.
    private void MigrateFromLocalAppDataRoot(string newDirectory, string newPath)
    {
        try
        {
            if (File.Exists(newPath)) return;

            var oldPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FileName);
            if (!File.Exists(oldPath)) return;

            Directory.CreateDirectory(newDirectory);
            File.Move(oldPath, newPath);
            _logger.LogInformation("Moved the credential container into its own application folder");
        }
        catch (Exception ex)
        {
            // Safe to leave behind: the user just has to enter the secret again.
            _logger.LogWarning(ex, "Could not move the existing credential container");
        }
    }
#endif

    public async Task<string?> GetAsync(string key)
    {
#if WINDOWS
        if (_fileStore != null)
        {
            try
            {
                var stored = await _fileStore.GetAsync(key).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(stored)) return stored;
            }
            catch (Exception ex)
            {
                MarkSessionOnly(key, ex);
            }
        }
#else
        try
        {
            var stored = await Microsoft.Maui.Storage.SecureStorage.Default.GetAsync(key).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(stored)) return stored;
        }
        catch (Exception ex)
        {
            MarkSessionOnly(key, ex);
        }
#endif

        lock (_sessionLock)
            return _sessionOnly.TryGetValue(key, out var sessionValue) ? sessionValue : null;
    }

    public async Task<bool> SetAsync(string key, string value)
    {
#if WINDOWS
        if (_fileStore != null)
        {
            try
            {
                await _fileStore.SetAsync(key, value).ConfigureAwait(false);
                IsPersistent = true;
                LastError = null;
                return true;
            }
            catch (Exception ex)
            {
                MarkSessionOnly(key, ex);
            }
        }
        else
        {
            MarkSessionOnly(key, new InvalidOperationException("The encrypted file container is unavailable."));
        }
#else
        try
        {
            await Microsoft.Maui.Storage.SecureStorage.Default.SetAsync(key, value).ConfigureAwait(false);
            IsPersistent = true;
            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            MarkSessionOnly(key, ex);
        }
#endif

        lock (_sessionLock)
            _sessionOnly[key] = value;

        // False means "not durable", so callers can tell the user instead of the credential vanishing on exit.
        return false;
    }

    public async Task RemoveAsync(string key)
    {
        lock (_sessionLock)
            _sessionOnly.Remove(key);

#if WINDOWS
        if (_fileStore == null) return;
        try
        {
            await _fileStore.RemoveAsync(key).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SecureStore: failed to remove {Key} from the file container", key);
        }
#else
        try
        {
            Microsoft.Maui.Storage.SecureStorage.Default.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SecureStore: failed to remove {Key} from SecureStorage", key);
        }
        await Task.CompletedTask.ConfigureAwait(false);
#endif
    }

    private void MarkSessionOnly(string key, Exception ex)
    {
        IsPersistent = false;
        LastError = $"Secure storage is unavailable on this device, so '{key}' is kept only until the app closes.";
        _logger.LogWarning(ex, "SecureStore: durable storage unavailable for {Key}; keeping it for this session only", key);
    }

#if WINDOWS
    private static Task<string> DpapiProtectAsync(string value) =>
        Task.FromResult(Convert.ToBase64String(WindowsDpapi.Protect(Encoding.UTF8.GetBytes(value))));

    private static Task<string> DpapiUnprotectAsync(string value) =>
        Task.FromResult(Encoding.UTF8.GetString(WindowsDpapi.Unprotect(Convert.FromBase64String(value))));
#endif
}
