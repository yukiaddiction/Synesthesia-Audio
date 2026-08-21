using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public class LibraryCacheService : IDisposable
{
    private readonly string _cacheFilePath;
    private readonly ILogger<LibraryCacheService> _logger;
    private volatile ConcurrentDictionary<string, WorkMetadata> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _saveLock = new();
    private Timer? _saveTimer;
    private const int SaveDebounceMs = 500;

    public LibraryCacheService(ILogger<LibraryCacheService> logger)
    {
        _logger = logger;
        _cacheFilePath = Path.Combine(FileSystem.AppDataDirectory, "library_cache.json");
        _ = LoadCacheAsync();
    }

    private async Task LoadCacheAsync()
    {
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                var json = await File.ReadAllTextAsync(_cacheFilePath).ConfigureAwait(false);
                var list = JsonSerializer.Deserialize<List<WorkMetadata>>(json);
                if (list != null)
                {
                    foreach (var work in list)
                    {
                        if (work.CoverImagePath == "placeholder_cover.jpg" || 
                            work.CoverImagePath == "placeholder_cover.png" || 
                            (!string.IsNullOrEmpty(work.CoverImagePath) && 
                             !work.CoverImagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase) && 
                             !File.Exists(work.CoverImagePath)))
                        {
                            work.CoverImagePath = string.Empty;
                        }
                    }
                    _cache.Clear();
                    foreach (var work in list)
                    {
                        if (!string.IsNullOrEmpty(work.FolderPath))
                            _cache[work.FolderPath] = work;
                    }
                    _logger.LogInformation("Library cache loaded — {Count} works", _cache.Count);
                }
            }
            else
            {
                _logger.LogInformation("Library cache file not found, starting with empty cache");
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "LoadCache failed"); }
    }

    public void SaveCache()
    {
        lock (_saveLock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_cache.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
                var tempPath = _cacheFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _cacheFilePath, overwrite: true);
                _logger.LogDebug("Library cache saved — {Count} works", _cache.Count);
            }
            catch (Exception ex) { _logger.LogError(ex, "SaveCache failed"); }
        }
    }

    private void RequestSave()
    {
        lock (_saveLock)
        {
            _saveTimer ??= new Timer(_ => SaveCache(), null, Timeout.Infinite, Timeout.Infinite);
            _saveTimer.Change(SaveDebounceMs, Timeout.Infinite);
        }
    }

    public void FlushPendingSave()
    {
        lock (_saveLock)
        {
            _saveTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
        SaveCache();
    }

    public WorkMetadata? GetWork(string folderPath)
    {
        return _cache.TryGetValue(folderPath, out var work) ? work : null;
    }

    public WorkMetadata? GetWorkByProductId(string productId)
    {
        if (string.IsNullOrEmpty(productId)) return null;
        return _cache.Values.FirstOrDefault(w =>
            !string.IsNullOrEmpty(w.ProductId) &&
            w.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    public List<WorkMetadata> GetAllWorks()
    {
        return _cache.Values.ToList();
    }

    public void UpdateWork(WorkMetadata work)
    {
        if (work == null || string.IsNullOrEmpty(work.FolderPath)) return;
        _cache[work.FolderPath] = work;
        RequestSave();
    }

    public void UpdateWorks(IEnumerable<WorkMetadata> works)
    {
        foreach (var work in works)
        {
            if (work != null && !string.IsNullOrEmpty(work.FolderPath))
            {
                _cache[work.FolderPath] = work;
            }
        }
        SaveCache();
    }

    public void ReplaceAll(IEnumerable<WorkMetadata> works)
    {
        var newCache = new ConcurrentDictionary<string, WorkMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var work in works)
        {
            if (work != null && !string.IsNullOrEmpty(work.FolderPath))
            {
                newCache[work.FolderPath] = work;
            }
        }
        _cache = newCache;
        SaveCache();
    }

    public void ResetRjCache()
    {
        var keysToRemove = _cache
            .Where(kvp => !string.IsNullOrEmpty(kvp.Value.ProductId))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            SaveCache();
        }

        _logger.LogInformation("RJ works cache reset — {Count} cached works removed", keysToRemove.Count);
    }

    public void CleanAndReMapAllWorkGenres(TagMappingService tagMappingService)
    {
        _logger.LogInformation("Cleaning and re-mapping genres for {Count} cached works", _cache.Count);
        tagMappingService.CleanBadMappings();
        foreach (var work in _cache.Values)
        {
            if (!string.IsNullOrEmpty(work.Genre))
            {
                work.Genre = tagMappingService.MapGenres(work.Genre);
            }
        }
        SaveCache();
        _logger.LogInformation("Genre clean and re-map complete");
    }

    public void Dispose()
    {
        lock (_saveLock)
        {
            _saveTimer?.Dispose();
            _saveTimer = null;
        }
    }
}
