using System.Text.Json;

namespace Synesthesia_Audio.Core;

/// <summary>
/// File-backed credential container. Only already-protected values ever reach the file, so no
/// caller can persist a plaintext secret through this class. Protection is injected, which keeps
/// the container testable without any platform dependency.
/// </summary>
public sealed class SecureFileStore
{
    private readonly string _path;
    private readonly Func<string, Task<string>> _protectAsync;
    private readonly Func<string, Task<string>> _unprotectAsync;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, string>? _entries;

    public SecureFileStore(string path, Func<string, Task<string>> protectAsync, Func<string, Task<string>> unprotectAsync)
    {
        _path = path;
        _protectAsync = protectAsync;
        _unprotectAsync = unprotectAsync;
    }

    public async Task<string?> GetAsync(string key)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var entries = await LoadAsync().ConfigureAwait(false);
            if (!entries.TryGetValue(key, out var stored) || string.IsNullOrEmpty(stored))
                return null;

            try
            {
                return await _unprotectAsync(stored).ConfigureAwait(false);
            }
            catch
            {
                // A blob protected under another Windows user cannot be decrypted. Drop it so the
                // caller re-prompts instead of failing forever.
                entries.Remove(key);
                await PersistAsync(entries).ConfigureAwait(false);
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync(string key, string value)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var entries = await LoadAsync().ConfigureAwait(false);
            entries[key] = await _protectAsync(value).ConfigureAwait(false);
            await PersistAsync(entries).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(string key)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var entries = await LoadAsync().ConfigureAwait(false);
            if (entries.Remove(key))
                await PersistAsync(entries).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadAsync()
    {
        if (_entries != null) return _entries;

        if (File.Exists(_path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_path).ConfigureAwait(false);
                _entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                return _entries;
            }
            catch (JsonException)
            {
                // Corrupt store: start clean rather than wedging every credential read.
            }
        }

        _entries = new Dictionary<string, string>();
        return _entries;
    }

    private async Task PersistAsync(Dictionary<string, string> entries)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        // Write beside the target then move, so an interrupted write cannot half-destroy the store.
        var temporary = _path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(entries)).ConfigureAwait(false);
        File.Move(temporary, _path, overwrite: true);
    }
}
