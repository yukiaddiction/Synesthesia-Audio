using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

public class ScanStateService : IDisposable
{
    private readonly JsonFileStore<ScanState> _store;
    private readonly ILogger<ScanStateService> _logger;
    private readonly object _lock = new();
    private ScanState _state = new();
    private Task? _loaded;

    public ScanStateService(ILogger<ScanStateService> logger)
    {
        _logger = logger;
        _store = new JsonFileStore<ScanState>(
            Path.Combine(FileSystem.AppDataDirectory, "scan_state.json"),
            saveDebounceMs: 400);
        _store.LoadFailed += ex => _logger.LogError(ex, "Scan state could not be read — the previous file was kept as .bak");
        _store.SaveFailed += ex => _logger.LogError(ex, "Scan state save failed");
    }

    /// <summary>
    /// Completes once the persisted scan status has been read. Startup awaits this so a scan that was
    /// running when the app was killed is seen as Interrupted instead of Idle.
    /// </summary>
    public Task Loaded
    {
        get
        {
            lock (_lock)
            {
                return _loaded ??= LoadIntoMemoryAsync();
            }
        }
    }

    private async Task LoadIntoMemoryAsync()
    {
        try
        {
            // The store replays any status written while this read was in flight, so the value here is
            // the merged one — never a stale file copy that would rewind a scan that already started.
            var persisted = await _store.Loaded.ConfigureAwait(false);
            lock (_lock)
            {
                _state = persisted;
            }
            _logger.LogInformation("Scan state loaded — status: {Status}", persisted.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadScanState failed");
        }
    }

    /// <summary>
    /// A copy, built field by field. This used to serialise and deserialise the whole state on every poll -
    /// four times a second while a scan runs (PERF-12).
    /// </summary>
    public ScanState Snapshot()
    {
        lock (_lock)
        {
            return new ScanState
            {
                Status = _state.Status,
                Progress = _state.Progress,
                ProgressText = _state.ProgressText,
                WorksFound = _state.WorksFound,
                TotalFiles = _state.TotalFiles,
                FailedFolderCount = _state.FailedFolderCount,
                FailedFolders = new List<string>(_state.FailedFolders),
                ErrorMessage = _state.ErrorMessage,
                StartedAt = _state.StartedAt,
                FinishedAt = _state.FinishedAt,
                AlertShown = _state.AlertShown
            };
        }
    }

    public void Update(Action<ScanState> mutate)
    {
        lock (_lock)
        {
            // Mutating through the store keeps the persisted document and the in-memory snapshot the
            // same instance, so a load that finishes later cannot discard this write.
            _store.Update(state =>
            {
                mutate(state);
                _state = state;
                return state;
            });
        }
    }

    public void MarkRunning(string text) => Update(s =>
    {
        s.Status = ScanStatus.Running;
        s.Progress = 0;
        s.ProgressText = text;
        s.WorksFound = 0;
        s.TotalFiles = 0;
        s.FailedFolderCount = 0;
        s.FailedFolders = new List<string>();
        s.ErrorMessage = null;
        s.AlertShown = false;
        s.StartedAt = DateTimeOffset.Now;
        s.FinishedAt = null;
    });

    public void UpdateProgress(double pct, string text) => Update(s =>
    {
        s.Progress = pct;
        s.ProgressText = text;
    });

    public void MarkCompleted(int works, int files, int failedFolderCount = 0) => Update(s =>
    {
        s.Status = ScanStatus.Completed;
        s.Progress = 1.0;
        // A scan that skipped folders used to say only "Completed", so the gaps were invisible (UX-05).
        s.ProgressText = failedFolderCount > 0
            ? $"Completed with {failedFolderCount} warning{(failedFolderCount == 1 ? "" : "s")}"
            : string.Empty;
        s.FailedFolderCount = failedFolderCount;
        s.WorksFound = works;
        s.TotalFiles = files;
        s.FinishedAt = DateTimeOffset.Now;
    });

    public void MarkFailed(string error, int failedFolderCount = 0, IReadOnlyList<string>? failedFolders = null) => Update(s =>
    {
        s.Status = ScanStatus.Failed;
        s.Progress = 1.0;
        s.ProgressText = string.Empty;
        s.ErrorMessage = error;
        s.FailedFolderCount = failedFolderCount;
        if (failedFolders != null) s.FailedFolders = failedFolders.ToList();
        s.FinishedAt = DateTimeOffset.Now;
    });

    public void MarkInterrupted(string reason) => Update(s =>
    {
        s.Status = ScanStatus.Interrupted;
        s.ErrorMessage = reason;
        s.AlertShown = false;
    });

    public void MarkAlertShown() => Update(s => s.AlertShown = true);

    /// <summary>Writes any debounced status change now. Called on window stop/destroy.</summary>
    public void FlushPendingSave()
    {
        try
        {
            _store.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveState failed");
        }
    }

    public void Dispose()
    {
        try
        {
            _store.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Scan state dispose failed");
        }
    }
}
