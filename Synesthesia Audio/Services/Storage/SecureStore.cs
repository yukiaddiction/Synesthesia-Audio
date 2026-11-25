using System.Text;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services.Storage;

/// <summary>
/// Credential store for user-supplied secrets. Values are never written as plaintext: the
/// persistence layer only ever sees protected values.
/// <para>
/// Windows desktop builds are unpackaged, so MAUI's SecureStorage cannot persist (its container
/// needs package identity). Windows therefore keeps DPAPI and uses a file container from
/// <see cref="SecureFileStore"/>. Every other platform keeps SecureStorage, which is backed by the
/// Android Keystore / iOS Keychain.
/// </para>
/// <para>
/// If no protector is usable at runtime, the value is held for this session only and
/// <see cref="IsPersistent"/> turns false so the UI can say so, rather than dropping the secret.
/// </para>
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
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), FileName);
        _fileStore = new SecureFileStore(path, DpapiProtectAsync, DpapiUnprotectAsync);
#endif
    }

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
        return true;
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
