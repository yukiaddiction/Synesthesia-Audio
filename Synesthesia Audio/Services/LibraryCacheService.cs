using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public class LibraryCacheService : IDisposable
{
    private readonly JsonFileStore<List<WorkMetadata>> _store;
    private readonly ILogger<LibraryCacheService> _logger;
    private readonly object _loadLock = new();
    private volatile ConcurrentDictionary<string, WorkMetadata> _cache = new(StringComparer.OrdinalIgnoreCase);

    // ProductId -> FolderPath, so an RJ-code lookup doesn't scan every cached work.
    private readonly ConcurrentDictionary<string, string> _pathByProductId = new(StringComparer.OrdinalIgnoreCase);
    private Task? _loaded;

    public LibraryCacheService(ILogger<LibraryCacheService> logger)
    {
        _logger = logger;
        _cacheFilePath = Path.Combine(FileSystem.AppDataDirectory, "library_cache.json");
        _store = new JsonFileStore<List<WorkMetadata>>(_cacheFilePath, saveDebounceMs: 500);
        _store.LoadFailed += ex => _logger.LogError(ex, "Library cache could not be read — the previous file was kept as .bak");
        _store.SaveFailed += ex => _logger.LogError(ex, "Library cache save failed");
    }

    private readonly string _cacheFilePath;

    public string CacheFilePath => _cacheFilePath;

    /// <summary>Startup must await this before reading the cache, or readers race the load and see an empty library.</summary>
    public Task Loaded
    {
        get
        {
            lock (_loadLock)
            {
                return _loaded ??= LoadIntoMemoryAsync();
            }
        }
    }

    private async Task LoadIntoMemoryAsync()
    {
        try
        {
            var list = await _store.Loaded.ConfigureAwait(false);

            foreach (var work in list)
            {
                if (string.IsNullOrEmpty(work.FolderPath)) continue;

                RepairMissingCover(work);

                // TryAdd: a work cached during the load is newer than the file copy.
                if (_cache.TryAdd(work.FolderPath, work)) IndexProductId(work);
            }

            _logger.LogInformation("Library cache loaded — {Count} works", _cache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadCache failed");
        }
    }

    /// <summary>
    /// Runs only after a full replace, when the referenced set is known. Conservative: recent files, non-images and
    /// files with unreadable timestamps are kept.
    /// </summary>
    private void CleanupOrphanedCovers()
    {
        try
        {
            var directory = Path.Combine(FileSystem.AppDataDirectory, "covers");
            if (!System.IO.Directory.Exists(directory)) return;

            var candidates = System.IO.Directory.GetFiles(directory).Select(Path.GetFileName).Where(n => n != null).Select(n => n!).ToList();
            if (candidates.Count == 0) return;

            var referenced = _cache.Values
                .Select(w => w.CoverImagePath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => Path.GetFileName(path))
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList();

            var deletable = CoverCleanupPolicy.SelectDeletable(
                candidates,
                referenced,
                name =>
                {
                    try
                    {
                        var info = new FileInfo(Path.Combine(directory, name));
                        return info.Exists ? new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero) : null;
                    }
                    catch
                    {
                        return null;
                    }
                },
                DateTimeOffset.UtcNow,
                TimeSpan.FromHours(6));

            foreach (var name in deletable)
            {
                try
                {
                    File.Delete(Path.Combine(directory, name));
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Deleting an orphaned cover failed");
                }
            }

            if (deletable.Count > 0)
                _logger.LogInformation("Removed {Count} orphaned cover files", deletable.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cover cleanup failed");
        }
    }
    /// <summary>Blanks a dead cover path so the next scan refetches it.</summary>
    private static void RepairMissingCover(WorkMetadata work)
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

    public void SaveCache()
    {
        _store.Replace(_cache.Values.ToList());
        SavePending();
    }

    private void SavePending()
    {
        try
        {
            _store.FlushAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveCache failed");
        }
    }

    /// <summary>Called on window stop/destroy so a debounced change isn't lost.</summary>
    public void FlushPendingSave() => SavePending();

    public WorkMetadata? GetWork(string folderPath)
    {
        return _cache.TryGetValue(folderPath, out var work) ? work : null;
    }

    public WorkMetadata? GetWorkByProductId(string productId)
    {
        if (string.IsNullOrEmpty(productId)) return null;

        if (_pathByProductId.TryGetValue(productId, out var indexedPath) &&
            _cache.TryGetValue(indexedPath, out var indexed))
        {
            return indexed;
        }

        // The index can miss a work cached before it was indexed in this process.
        return _cache.Values.FirstOrDefault(w =>
            !string.IsNullOrEmpty(w.ProductId) &&
            w.ProductId.Equals(productId, StringComparison.OrdinalIgnoreCase));
    }

    private void IndexProductId(WorkMetadata work)
    {
        if (string.IsNullOrEmpty(work.ProductId)) return;
        _pathByProductId[work.ProductId] = work.FolderPath;
    }

    public List<WorkMetadata> GetAllWorks()
    {
        return _cache.Values.ToList();
    }

    public void UpdateWork(WorkMetadata work)
    {
        if (work == null || string.IsNullOrEmpty(work.FolderPath)) return;
        _cache[work.FolderPath] = work;
        IndexProductId(work);
        UpsertInStore(work);
    }

    public void UpdateWorks(IEnumerable<WorkMetadata> works)
    {
        var changed = false;
        foreach (var work in works)
        {
            if (work == null || string.IsNullOrEmpty(work.FolderPath)) continue;
            _cache[work.FolderPath] = work;
            UpsertInStore(work);
            changed = true;
        }

        if (changed) SavePending();
    }

    private void UpsertInStore(WorkMetadata work)
    {
        // Through the store, so a write made before the load finishes is replayed onto it rather than lost.
        _store.Update(list =>
        {
            var index = list.FindIndex(w => string.Equals(w.FolderPath, work.FolderPath, StringComparison.OrdinalIgnoreCase));
            if (index >= 0) list[index] = work;
            else list.Add(work);
            return list;
        });
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
        _pathByProductId.Clear();
        foreach (var work in newCache.Values) IndexProductId(work);
        _store.Replace(newCache.Values.ToList());
        CleanupOrphanedCovers();
        SavePending();
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
            _store.Replace(_cache.Values.ToList());
            SavePending();
        }

        _logger.LogInformation("RJ works cache reset — {Count} cached works removed", keysToRemove.Count);

        foreach (var key in keysToRemove)
        {
            foreach (var pair in _pathByProductId.Where(p => string.Equals(p.Value, key, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                _pathByProductId.TryRemove(pair.Key, out _);
            }
        }
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
        _store.Replace(_cache.Values.ToList());
        SavePending();
        _logger.LogInformation("Genre clean and re-map complete");
    }

    public void Dispose()
    {
        try
        {
            _store.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Library cache dispose failed");
        }
    }
}
