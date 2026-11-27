using System.Text.Json;

namespace Synesthesia_Audio.Core;

/// <summary>
/// One JSON document in one file. Loading is lazy and single; writes made before the load finishes are replayed on
/// top of it rather than lost; saves are debounced, serialised, and written via temp-file-then-move so a crash cannot
/// leave half a document; an unparsable file is moved to <c>.bak</c> instead of being silently replaced.
/// </summary>
public sealed class JsonFileStore<T> : IAsyncDisposable where T : class, new()
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly Func<T, T>? _onBeforeSave;
    private readonly int _saveDebounceMs;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _stateLock = new();
    private readonly List<Func<T, T>> _pendingMutations = new();

    private T _value = new();
    private Task<T>? _loadTask;
    private bool _loaded;
    private bool _dirty;
    private bool _disposed;
    private Timer? _saveTimer;

    /// <param name="onBeforeSave">
    /// Transforms a copy just before serialising, for fields that must not persist (secrets, derived data).
    /// </param>
    public JsonFileStore(string path, JsonSerializerOptions? options = null, Func<T, T>? onBeforeSave = null, int saveDebounceMs = 400)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));

        _path = path;
        _options = options ?? new JsonSerializerOptions();
        _onBeforeSave = onBeforeSave;
        _saveDebounceMs = Math.Max(0, saveDebounceMs);
    }

    public string Path => _path;

    public event Action<Exception>? LoadFailed;

    /// <summary>The in-memory document is still intact when this fires.</summary>
    public event Action<Exception>? SaveFailed;

    /// <summary>Completes only after mutations queued before the read have been replayed.</summary>
    public Task<T> Loaded
    {
        get
        {
            lock (_stateLock)
            {
                // Task.Run keeps the synchronous prologue off the caller's thread and off this lock.
                return _loadTask ??= Task.Run(LoadCoreAsync);
            }
        }
    }

    /// <summary>The callback runs under the store lock, so keep it short.</summary>
    public TResult Read<TResult>(Func<T, TResult> read)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        lock (_stateLock)
        {
            return read(_value);
        }
    }

    /// <summary>
    /// The callback returns the replacement document (usually the same instance mutated in place). Before the load
    /// finishes the mutation is also queued for replay, so the load cannot overwrite it.
    /// </summary>
    public T Update(Func<T, T> mutate)
    {
        if (mutate == null) throw new ArgumentNullException(nameof(mutate));

        T result;
        bool scheduleSave;
        lock (_stateLock)
        {
            result = mutate(_value) ?? _value;
            _value = result;
            if (!_loaded) _pendingMutations.Add(mutate);
            _dirty = true;
            scheduleSave = _loaded;
        }

        if (scheduleSave) ScheduleSave();
        return result;
    }

    /// <summary>Drops queued mutations, since a full replacement is authoritative.</summary>
    public void Replace(T value)
    {
        var replacement = value ?? new T();
        bool scheduleSave;
        lock (_stateLock)
        {
            if (!_loaded)
            {
                _pendingMutations.Clear();
                _pendingMutations.Add(_ => replacement);
            }
            _value = replacement;
            _dirty = true;
            scheduleSave = _loaded;
        }

        if (scheduleSave) ScheduleSave();
    }

    public async Task FlushAsync()
    {
        // Awaiting the load guarantees queued mutations are replayed before serialising.
        await Loaded.ConfigureAwait(false);

        bool hadPending;
        lock (_stateLock)
        {
            hadPending = _dirty;
            _dirty = false;
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        if (!hadPending) return;
        await WriteCoreAsync().ConfigureAwait(false);
    }

    public async Task SaveNowAsync()
    {
        await Loaded.ConfigureAwait(false);
        lock (_stateLock) _dirty = false;
        await WriteCoreAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// For callers about to delete the file: a debounced save firing afterwards would otherwise resurrect it.
    /// </summary>
    public void ResetWithoutSaving()
    {
        lock (_stateLock)
        {
            _value = new T();
            _pendingMutations.Clear();
            _loaded = true;
            _dirty = false;
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private async Task<T> LoadCoreAsync()
    {
        T loaded = new();

        if (File.Exists(_path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_path).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var deserialized = JsonSerializer.Deserialize<T>(json, _options);
                    if (deserialized != null) loaded = deserialized;
                }
            }
            catch (Exception ex)
            {
                // Never start clean over a file we merely failed to understand: keep it aside so it can be recovered.
                BackupUnreadableFile(ex);
            }
        }

        bool needsSave;
        lock (_stateLock)
        {
            // Replay writes made while the read was in flight, so a late load cannot clobber newer state.
            foreach (var mutate in _pendingMutations)
            {
                loaded = mutate(loaded) ?? loaded;
            }
            _pendingMutations.Clear();

            _value = loaded;
            _loaded = true;
            needsSave = _dirty;
        }

        if (needsSave) ScheduleSave();
        return loaded;
    }

    private void BackupUnreadableFile(Exception cause)
    {
        try
        {
            File.Move(_path, _path + ".bak", overwrite: true);
        }
        catch
        {
            // Leaving the file in place is the safer failure; the cause is still reported.
        }

        LoadFailed?.Invoke(cause);
    }

    private void ScheduleSave()
    {
        lock (_stateLock)
        {
            if (_disposed) return;

            _saveTimer ??= new Timer(_ => SaveFromTimer(), null, Timeout.Infinite, Timeout.Infinite);
            _saveTimer.Change(_saveDebounceMs, Timeout.Infinite);
        }
    }

    private void SaveFromTimer()
    {
        // Timer callbacks cannot await; observe the fault so it does not go unobserved (write errors raise SaveFailed).
        _ = FlushAsync().ContinueWith(
            static t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task WriteCoreAsync()
    {
        try
        {
            await _writeGate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            // Serialise under the state lock: documents are mutated in place, so a parallel Update during
            // enumeration would throw or persist a torn document.
            string json;
            lock (_stateLock)
            {
                var toWrite = _onBeforeSave != null ? _onBeforeSave(_value) : _value;
                json = JsonSerializer.Serialize(toWrite, _options);
            }

            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Write beside the target and move over it, so an interrupted write never leaves a truncated document.
            var temporary = _path + ".tmp";
            await File.WriteAllTextAsync(temporary, json).ConfigureAwait(false);
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex)
        {
            SaveFailed?.Invoke(ex);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Flush before tearing down the writer so shutdown never loses the last write.
        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        catch
        {
            // Shutdown path: nothing useful left to report to.
        }

        Timer? timer;
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
            timer = _saveTimer;
            _saveTimer = null;
        }

        if (timer != null)
        {
            await timer.DisposeAsync().ConfigureAwait(false);
        }

        _writeGate.Dispose();
    }
}
