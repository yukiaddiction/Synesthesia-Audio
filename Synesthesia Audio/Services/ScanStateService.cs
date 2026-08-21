using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

public class ScanStateService : IDisposable
{
    private readonly string _filePath;
    private readonly ILogger<ScanStateService> _logger;
    private readonly object _lock = new();
    private ScanState _state = new();
    private Timer? _saveTimer;
    private const int SaveDebounceMs = 400;

    public ScanStateService(ILogger<ScanStateService> logger)
    {
        _logger = logger;
        _filePath = Path.Combine(FileSystem.AppDataDirectory, "scan_state.json");
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
                var loaded = JsonSerializer.Deserialize<ScanState>(json);
                if (loaded != null)
                {
                    lock (_lock) _state = loaded;
                    _logger.LogInformation("Scan state loaded — status: {Status}", loaded.Status);
                }
            }
            else
            {
                _logger.LogInformation("Scan state file not found, starting fresh");
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "LoadScanState failed"); }
    }

    public ScanState Snapshot()
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(_state);
            return JsonSerializer.Deserialize<ScanState>(json) ?? new ScanState();
        }
    }

    public void Update(Action<ScanState> mutate)
    {
        lock (_lock)
        {
            mutate(_state);
            RequestSave();
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

    public void MarkCompleted(int works, int files) => Update(s =>
    {
        s.Status = ScanStatus.Completed;
        s.Progress = 1.0;
        s.ProgressText = string.Empty;
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

    public void FlushPendingSave()
    {
        lock (_lock)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        SaveState();
    }

    private void RequestSave()
    {
        lock (_lock)
        {
            _saveTimer ??= new Timer(_ => SaveState(), null, Timeout.Infinite, Timeout.Infinite);
            _saveTimer.Change(SaveDebounceMs, Timeout.Infinite);
        }
    }

    private void SaveState()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_state);
                var tempPath = _filePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            catch (Exception ex) { _logger.LogError(ex, "SaveState failed"); }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
    }
}
