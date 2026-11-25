using System.Text.Json;

namespace Synesthesia_Audio.Core;

/// <summary>
/// A single JSON document backed by one file, with the guarantees every settings-like store in this
/// app needs:
/// <list type="bullet">
///   <item>Nothing is read in the constructor. <see cref="Loaded"/> starts the (single) read on first
///   access, so a caller can never observe a half-initialised store.</item>
///   <item>Mutations made before the read finishes are recorded and replayed on top of the loaded
///   document instead of being discarded, so an early write is never lost to a late load.</item>
///   <item>Writes are debounced, serialised through a semaphore, written to a temporary file and then
///   moved over the target, so a crash or a parallel writer cannot leave half a document behind.</item>
///   <item>A document that cannot be parsed is moved aside to <c>.bak</c> rather than silently replaced,
///   so the user's data is recoverable.</item>
/// </list>
/// </summary>
/// <typeparam name="T">The document type. Must be constructible so a missing file yields a usable value.</typeparam>
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

    /// <param name="path">Absolute path of the JSON document.</param>
    /// <param name="options">Serializer options; defaults to compact output.</param>
    /// <param name="onBeforeSave">
    /// Optional transform applied to a copy of the document immediately before serialising, for types
    /// that must not persist some fields (secrets, derived data). Never mutates the live document.
    /// </param>
    /// <param name="saveDebounceMs">Quiet period before a debounced write happens.</param>
    public JsonFileStore(string path, JsonSerializerOptions? options = null, Func<T, T>? onBeforeSave = null, int saveDebounceMs = 400)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A file path is required.", nameof(path));

        _path = path;
        _options = options ?? new JsonSerializerOptions();
        _onBeforeSave = onBeforeSave;
        _saveDebounceMs = Math.Max(0, saveDebounceMs);
    }

    public string Path => _path;

    /// <summary>Raised when a load failed and the file had to be moved aside.</summary>
    public event Action<Exception>? LoadFailed;

    /// <summary>Raised when a save could not be completed. The in-memory document is still intact.</summary>
    public event Action<Exception>? SaveFailed;

    /// <summary>
    /// Completes with the document once it has been read and every mutation queued beforehand has been
    /// replayed. Callers that need the persisted state must await this (or <see cref="FlushAsync"/>).
    /// </summary>
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

    /// <summary>Reads the live document. The callback runs under the store lock, so keep it short.</summary>
    public TResult Read<TResult>(Func<T, TResult> read)
    {
        if (read == null) throw new ArgumentNullException(nameof(read));
        lock (_stateLock)
        {
            return read(_value);
        }
    }

    /// <summary>
    /// Applies a mutation. The callback receives the current document and returns the document that
    /// replaces it (usually the same instance, mutated in place). Before the load finishes the
    /// mutation is also queued for replay, so it survives the load instead of being overwritten.
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

    /// <summary>
    /// Replaces the whole document. Any mutation still queued is dropped, because a full replacement is
    /// by definition authoritative.
    /// </summary>
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

    /// <summary>
    /// Ensures the document has been loaded, then writes any pending change immediately.
    /// </summary>
    public async Task FlushAsync()
    {
        // Awaiting the load first guarantees queued mutations have been replayed before we serialise.
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

    /// <summary>Writes the current document regardless of the dirty flag. Used by explicit save paths.</summary>
    public async Task SaveNowAsync()
    {
        await Loaded.ConfigureAwait(false);
        lock (_stateLock) _dirty = false;
        await WriteCoreAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resets the document to a fresh value and cancels every queued write without touching the file.
    /// Used when the caller is about to delete the file: a debounced save that fired afterwards would
    /// otherwise resurrect it.
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
                // Never start clean over a file we merely failed to understand: keep it aside so the
                // data can be recovered, and report the failure to whoever wired logging.
                BackupUnreadableFile(ex);
            }
        }

        bool needsSave;
        lock (_stateLock)
        {
            // Replay anything written while the read was in flight. This is the property that stops a
            // late load from clobbering newer state.
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
            // Leaving the file in place is the safer failure: we still report the cause below.
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
        // The timer contract cannot await; route failures to the SaveFailed event.
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
            // Serialise under the state lock. Documents are mutated in place, so letting a parallel
            // Update run while the serializer enumerates the live instance would either throw or
            // persist a torn document — the exact corruption this store exists to prevent.
            string json;
            lock (_stateLock)
            {
                var toWrite = _onBeforeSave != null ? _onBeforeSave(_value) : _value;
                json = JsonSerializer.Serialize(toWrite, _options);
            }

            var directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            // Write beside the target and move over it: an interrupted write can then never leave a
            // truncated document where the real one used to be.
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
        // Persist anything still pending before the writer is torn down, so disposing a store at
        // shutdown never loses the last write.
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
