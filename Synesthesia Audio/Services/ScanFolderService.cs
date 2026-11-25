using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public class ScanFolderService : IDisposable
{
    private readonly DLsiteService _dlsiteService;
    private readonly LibraryCacheService _cacheService;
    private readonly SettingsService _settingsService;
    private readonly ScanStateService _scanStateService;
    private readonly Services.Cloud.ICloudProvider _cloudProvider;
    private readonly HttpClient _httpClient = new();
    private List<string> _failedFolders = new();
    private readonly ILogger<ScanFolderService> _logger;

    public ScanFolderService(DLsiteService dlsiteService, LibraryCacheService cacheService, SettingsService settingsService, ScanStateService scanStateService, Services.Cloud.ICloudProvider cloudProvider, ILogger<ScanFolderService> logger)
    {
        _dlsiteService = dlsiteService;
        _cacheService = cacheService;
        _settingsService = settingsService;
        _scanStateService = scanStateService;
        _cloudProvider = cloudProvider;
        _logger = logger;
    }

    /// <summary>
    /// Lightweight fetch for a single new work 窶・used during quick background checks.
    /// </summary>
    public async Task<(string? Title, string? TranslatedTitle, string? Circle, string? VA, string? AgeRating, string? Genre, string? CoverImagePath)>
        FetchDlsiteMetadataAsync(string rjCode, string folderPath)
    {
        var meta = await _dlsiteService.GetWorkMetadataAsync(rjCode);

        string? coverPath = null;
        if (!string.IsNullOrEmpty(meta.CoverUrl))
        {
            var localCoverPath = GetLocalCoverCachePath(folderPath, rjCode);
            try
            {
                using var response = await _httpClient.GetAsync(meta.CoverUrl);
                if (response.IsSuccessStatusCode)
                {
                    using var fs = new FileStream(localCoverPath, FileMode.Create, FileAccess.Write);
                    await response.Content.CopyToAsync(fs);
                    coverPath = localCoverPath;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cover download failed for {RjCode}, falling back to remote URL", rjCode);
                coverPath = meta.CoverUrl;
            }
        }

        // Fallback: local cover image
        if (string.IsNullOrEmpty(coverPath) && !string.IsNullOrEmpty(folderPath) && !_settingsService.CurrentSettings.StorageType.Contains("WebDAV"))
        {
            try
            {
                var localImages = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (localImages.Any())
                    coverPath = localImages.First();
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Local cover fallback failed"); }
        }

        return (meta.Title, meta.TranslatedTitle, meta.Circle, meta.VA, meta.AgeRating, meta.Genre, coverPath);
    }

    /// <summary>
    /// Quick top-level folder check for new works not yet in cache.
    /// Handles both local and cloud modes. Returns newly discovered works.
    /// </summary>
    public async Task<List<WorkMetadata>> CheckForNewWorksAsync(string basePath, IEnumerable<string> cachedPaths)
    {
        var settings = _settingsService.CurrentSettings;
        bool isCloud = settings.StorageType == "WebDAV";
        _logger.LogInformation("Checking for new works — isCloud: {IsCloud}", isCloud);
        var newWorks = new List<WorkMetadata>();
        var cachedPathSet = new HashSet<string>(cachedPaths, StringComparer.OrdinalIgnoreCase);

        List<(string Path, string FolderName)> folders;

        if (isCloud)
        {
            if (!_cloudProvider.IsConfigured) return newWorks;
            try
            {
                var items = await _cloudProvider.ListFilesAsync(basePath);
                folders = items
                    .Where(i => i.IsDirectory && !AppConstants.IsSystemFileOrDir(i.Path))
                    .Select(i =>
                    {
                        var name = i.Name;
                        if (string.IsNullOrEmpty(name))
                            name = i.Path.Replace('\\', '/').TrimEnd('/').Split('/').Last();
                        return (i.Path, name);
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CheckForNewWorks: cloud folder listing failed for {BasePath}", basePath);
                return newWorks;
            }
        }
        else
        {
            if (!Directory.Exists(basePath)) return newWorks;
            folders = Directory.GetDirectories(basePath)
                .Select(d => (d, Path.GetFileName(d)))
                .Where(f => !string.IsNullOrEmpty(f.Item2))
                .ToList();
        }

        var newFolders = folders
            .Where(f => !cachedPathSet.Contains(f.Path))
            .ToList();

        if (newFolders.Count == 0) return newWorks;

        foreach (var (path, folderName) in newFolders)
        {
            var match = Regex.Match(folderName, @"(RJ\d{6,8})", RegexOptions.IgnoreCase);
            string? rjCode = match.Success ? match.Groups[1].Value.ToUpper() : null;

            var work = new WorkMetadata
            {
                FolderPath = path,
                Title = folderName,
                AgeRating = "SFW"
            };

            if (rjCode != null)
            {
                var cachedByRj = _cacheService.GetWorkByProductId(rjCode);
                if (cachedByRj != null)
                {
                    work.Title = cachedByRj.Title;
                    work.TranslatedTitle = cachedByRj.TranslatedTitle;
                    work.CoverImagePath = cachedByRj.CoverImagePath;
                    work.Circle = cachedByRj.Circle;
                    work.VA = cachedByRj.VA;
                    work.AgeRating = cachedByRj.AgeRating;
                    work.ProductId = cachedByRj.ProductId;
                    work.Genre = cachedByRj.Genre;
                }
                else
                {
                    work.ProductId = rjCode;
                    var meta = await FetchDlsiteMetadataAsync(rjCode, path);
                    work.Title = !string.IsNullOrEmpty(meta.Title) ? meta.Title : folderName;
                    work.TranslatedTitle = meta.TranslatedTitle ?? "";
                    work.Circle = meta.Circle ?? "Unknown Circle";
                    work.VA = meta.VA ?? "";
                    work.AgeRating = meta.AgeRating ?? "SFW";
                    work.Genre = meta.Genre ?? "";
                    work.CoverImagePath = meta.CoverImagePath ?? "";
                }
            }

            List<string> audioFiles;
            if (isCloud)
            {
                try
                {
                    var cloudItems = await _cloudProvider.ListFilesAsync(path);
                    audioFiles = cloudItems
                        .Where(i => !i.IsDirectory)
                        .Select(i => i.Path)
                        .Where(f => AppConstants.IsMediaFile(f) && !AppConstants.IsSystemFileOrDir(f))
                        .ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "CheckForNewWorks: cloud audio listing failed for {Path}", path);
                    continue;
                }
            }
            else
            {
                audioFiles = Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => AppConstants.IsMediaFile(f))
                    .ToList();
            }

            if (audioFiles.Count > 0)
            {
                var subAlbum = new SubAlbum { Title = "Main", FolderPath = path };
                var tracks = audioFiles
                    .OrderBy(f => f)
                    .Select(f => new Track
                    {
                        Title = isCloud
                            ? Path.GetFileNameWithoutExtension(GetCloudFileName(f))
                            : Path.GetFileNameWithoutExtension(f),
                        FilePath = f
                    });

                foreach (var t in tracks) subAlbum.Tracks.Add(t);
                work.SubAlbums.Add(subAlbum);

                _cacheService.UpdateWork(work);
                newWorks.Add(work);
            }
        }

        _logger.LogInformation("Check for new works complete — {Count} new works found", newWorks.Count);
        return newWorks;
    }

    public async Task<ScanResult> ScanLibraryAsync(string basePath, IProgress<(double, string)>? progress = null)
    {
        // The cache reads lazily; finish that read before deciding which works are stale or new.
        await _cacheService.Loaded.ConfigureAwait(false);

        _failedFolders = new List<string>();
        var settings = _settingsService.CurrentSettings;
        bool isCloud = settings.StorageType == "WebDAV";
        _logger.LogInformation("Library scan started — isCloud: {IsCloud}, path: {BasePath}", isCloud, basePath);
        _scanStateService.MarkRunning("Scan Library Found 0 Work, 0 Audio Files");

        // One detailed scraper warning per scan instead of one per work.
        DLsiteService.ResetScanDiagnostics();

        void Report(double pct, string msg)
        {
            progress?.Report((pct, msg));
            _scanStateService.UpdateProgress(pct, msg);
        }

        Report(0, "Scan Library Found 0 Work, 0 Audio Files");

        var workGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        List<string> allAudioFiles = new();

        if (isCloud && !_cloudProvider.IsConfigured)
        {
            _scanStateService.MarkFailed("WebDAV is not configured. Add your server details in Settings.");
            return new ScanResult();
        }

        if (!isCloud && !Directory.Exists(basePath))
        {
            _scanStateService.MarkFailed($"The configured folder no longer exists: {basePath}");
            return new ScanResult();
        }

        // One walk for both storage types: the failures it records are what stops a partial listing
        // from being written back as "the whole library".
        IScanFileSource fileSource = isCloud
            ? new CloudScanFileSource(_cloudProvider)
            : new LocalScanFileSource();

        var listing = await ScanListing.CollectAudioFilesAsync(
            fileSource,
            basePath,
            AppConstants.IsMediaFile,
            AppConstants.IsSystemFileOrDir,
            filesFound => Report(0.15 * Math.Min(1.0, filesFound / 500.0),
                $"Scan Library Found {filesFound} Audio File{(filesFound == 1 ? "" : "s")}"));

        _failedFolders = listing.FailedPaths;

        if (listing.RootFailed)
        {
            // Nothing is known about the library, so the cache must not be touched at all.
            var reason = $"The library folder could not be read: {basePath}. Your library was left unchanged.";
            _logger.LogError("Scan aborted — root listing failed for {BasePath}", basePath);
            _scanStateService.MarkFailed(reason, listing.FailedPaths.Count, listing.FailedPaths);
            return new ScanResult { FailedFolders = listing.FailedPaths };
        }

        allAudioFiles = listing.AudioFiles;
        foreach (var file in allAudioFiles)
        {
            var workFolder = DetermineWorkFolder(file, basePath, isCloud);
            if (!workGroups.TryGetValue(workFolder, out var fileList))
            {
                fileList = new List<string>();
                workGroups[workFolder] = fileList;
            }
            fileList.Add(file);
        }

        int totalFoundWorks = workGroups.Count;
        int totalFoundFiles = allAudioFiles.Count;
        Report(0.15, $"Scan Library Found {totalFoundWorks} Work{(totalFoundWorks == 1 ? "" : "s")}, {totalFoundFiles} Audio File{(totalFoundFiles == 1 ? "" : "s")}");
        _logger.LogInformation("Scan discovery complete — {Folders} folders, {Files} files", totalFoundWorks, totalFoundFiles);

        var pendingWorks = new List<(WorkMetadata Work, string WorkPath, string? RjCode, List<string> Files)>();
        var readyWorks = new List<(WorkMetadata Work, List<string> Files)>();
        int failedMetadata = 0;

        foreach (var kvp in workGroups)
        {
            var workPath = kvp.Key;
            var files = kvp.Value;

            string folderName;
            if (isCloud)
            {
                var cleanPath = workPath.Replace('\\', '/').TrimEnd('/');
                var lastSlash = cleanPath.LastIndexOf('/');
                folderName = lastSlash >= 0 ? cleanPath.Substring(lastSlash + 1) : cleanPath;
                if (string.IsNullOrEmpty(folderName)) folderName = "Root";
            }
            else
            {
                folderName = Path.GetFileName(workPath);
                if (string.IsNullOrEmpty(folderName))
                    folderName = new DirectoryInfo(workPath).Name;
            }

            var match = Regex.Match(folderName, @"(RJ\d{6,8})", RegexOptions.IgnoreCase);

            var work = new WorkMetadata
            {
                FolderPath = workPath,
                Title = folderName,
                AgeRating = "SFW"
            };

            var cachedWork = _cacheService.GetWork(workPath);

            // A failed fetch must not be frozen into the cache, and entries already carrying the
            // old placeholder titles are treated as stale so existing libraries heal by themselves.
            if (cachedWork != null && IsStaleCachedWork(cachedWork))
                cachedWork = null;

            string? rjCode = null;

            if (cachedWork == null && match.Success)
            {
                rjCode = match.Groups[1].Value.ToUpper();
                var cachedByRj = _cacheService.GetWorkByProductId(rjCode);
                if (cachedByRj != null && !IsStaleCachedWork(cachedByRj))
                    cachedWork = cachedByRj;
            }

            if (cachedWork != null)
            {
                work.Title = cachedWork.Title;
                work.TranslatedTitle = cachedWork.TranslatedTitle;
                work.CoverImagePath = cachedWork.CoverImagePath;
                work.Circle = cachedWork.Circle;
                work.VA = cachedWork.VA;
                work.AgeRating = cachedWork.AgeRating;
                work.ProductId = cachedWork.ProductId;
                work.Genre = cachedWork.Genre;
                readyWorks.Add((work, files));
            }
            else if (match.Success)
            {
                rjCode ??= match.Groups[1].Value.ToUpper();
                work.ProductId = rjCode;
                pendingWorks.Add((work, workPath, rjCode, files));
            }
            else
            {
                // No RJ code in the folder path means there is nothing to look up, which is a
                // legitimate reason for empty metadata - say so instead of failing silently.
                _logger.LogInformation("Work has no RJ code and cannot be scraped: {Folder}", folderName);
                readyWorks.Add((work, files));
            }
        }
        Report(0.15, $"scan: fetching metadata (0/{pendingWorks.Count})");
        _logger.LogInformation("Fetching DLsite metadata for {Count} works", pendingWorks.Count);

        if (pendingWorks.Count > 0)
        {
            var semaphore = new SemaphoreSlim(AppConstants.MaxParallelDlsiteRequests);
            int totalMeta = pendingWorks.Count;
            int completedMeta = 0;
            var tasks = pendingWorks.Select(async pw =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var (work, workPath, rjCode, _) = pw;

                    try
                    {
                        // Assume failure until a validated title comes back, so an outage is
                        // retried on the next scan instead of being cached forever.
                        work.NeedsMetadataRefresh = true;
                        var meta = await _dlsiteService.GetWorkMetadataAsync(rjCode!);
                        if (!string.IsNullOrEmpty(meta.Title))
                        {
                            work.Title = meta.Title;
                            work.TranslatedTitle = meta.TranslatedTitle;
                            work.Circle = meta.Circle;
                            work.VA = meta.VA;
                            work.AgeRating = meta.AgeRating;
                            work.Genre = meta.Genre;
                            work.NeedsMetadataRefresh = false;

                            if (!string.IsNullOrEmpty(meta.CoverUrl))
                            {
                                var localCoverPath = GetLocalCoverCachePath(workPath, rjCode!);
                                try
                                {
                                    using var response = await _httpClient.GetAsync(meta.CoverUrl);
                                    if (response.IsSuccessStatusCode)
                                    {
                                        using var fs = new FileStream(localCoverPath, FileMode.Create, FileAccess.Write);
                                        await response.Content.CopyToAsync(fs);
                                        work.CoverImagePath = localCoverPath;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Cover download failed for {RjCode}, falling back to remote URL", rjCode);
                                    work.CoverImagePath = meta.CoverUrl;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(work.CoverImagePath))
                        {
                            await FindCoverImageFallbackAsync(work, workPath, isCloud, rjCode!);
                        }

                        _cacheService.UpdateWork(work);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Metadata fetch failed for {RjCode}, keeping folder name", rjCode);
                    }

                    if (work.NeedsMetadataRefresh)
                        Interlocked.Increment(ref failedMetadata);

                    var done = Interlocked.Increment(ref completedMeta);
                    Report(0.15 + 0.80 * done / totalMeta, $"scan: fetching metadata ({done}/{totalMeta})");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            if (failedMetadata > 0)
            {
                _logger.LogWarning(
                    "Metadata could not be fetched for {Failed} of {Total} works; they will be retried on the next scan",
                    failedMetadata, pendingWorks.Count);
            }

            // Move pending works to ready
            foreach (var pw in pendingWorks)
            {
                readyWorks.Add((pw.Work, pw.Files));
            }
        }

        var finalWorks = new List<WorkMetadata>();

        foreach (var (work, files) in readyWorks)
        {
            var workPath = work.FolderPath;

            var cachedWork = _cacheService.GetWork(workPath)
                          ?? _cacheService.GetWorkByProductId(work.ProductId);

            var subAlbumGroups = files.GroupBy(f =>
            {
                if (isCloud)
                {
                    var fileDir = GetCloudDirectoryName(f);
                    if (fileDir == null || fileDir.Equals(workPath, StringComparison.OrdinalIgnoreCase)) return "Main";
                    var relPath = fileDir.Substring(workPath.Length).TrimStart('/', '\\');
                    return string.IsNullOrEmpty(relPath) ? "Main" : relPath;
                }
                else
                {
                    var fileDir = Path.GetDirectoryName(f);
                    if (fileDir == null || fileDir.Equals(workPath, StringComparison.OrdinalIgnoreCase)) return "Main";
                    var relPath = fileDir.Substring(workPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return string.IsNullOrEmpty(relPath) ? "Main" : relPath;
                }
            });

            foreach (var subGroup in subAlbumGroups)
            {
                var subAlbum = new SubAlbum { Title = subGroup.Key, FolderPath = workPath };

                var tracksGrouped = subGroup.GroupBy(f => isCloud ? GetCloudFileName(f) : Path.GetFileNameWithoutExtension(f));
                foreach (var tg in tracksGrouped)
                {
                    var bestFile = tg.OrderBy(f => GetExtensionPriority(Path.GetExtension(f.Replace('\\', '/')))).First();
                    var trackTitle = isCloud ? Path.GetFileNameWithoutExtension(GetCloudFileName(bestFile)) : Path.GetFileNameWithoutExtension(bestFile);

                    var cachedTrack = cachedWork?.SubAlbums
                        .FirstOrDefault(sa => sa.Title.Equals(subGroup.Key, StringComparison.OrdinalIgnoreCase))?
                        .Tracks.FirstOrDefault(t => t.Title.Equals(trackTitle, StringComparison.OrdinalIgnoreCase));

                    var track = new Track
                    {
                        Title = trackTitle,
                        FilePath = bestFile,
                        TranslatedTitle = cachedTrack?.TranslatedTitle ?? string.Empty
                    };

                    subAlbum.Tracks.Add(track);
                }

                var sortedTracks = subAlbum.Tracks.OrderBy(t => t.Title).ToList();
                subAlbum.Tracks.Clear();
                foreach (var t in sortedTracks) subAlbum.Tracks.Add(t);

                work.SubAlbums.Add(subAlbum);
            }

            if (work.SubAlbums.Any())
            {
                finalWorks.Add(work);
                _cacheService.UpdateWork(work);
            }
        }

        if (LibraryCacheMergePolicy.CanReplaceWholeCache(listing))
        {
            _cacheService.ReplaceAll(finalWorks);
        }
        else
        {
            // Part of the library could not be listed. Keep the cached works that live under those
            // folders: they are probably still on disk, and replacing the cache wholesale would throw
            // away their metadata, covers and translations.
            var merged = LibraryCacheMergePolicy.Merge(
                finalWorks,
                _cacheService.GetAllWorks(),
                listing.FailedPaths,
                work => work.FolderPath);

            _logger.LogWarning("Scan incomplete — {Failed} folder(s) unreadable; kept {Kept} cached work(s) under them",
                listing.FailedPaths.Count, merged.Count - finalWorks.Count);

            _cacheService.ReplaceAll(merged);
            finalWorks = merged;
        }

        Report(1.0, "scan: done");
        _logger.LogInformation("Library scan complete — {Count} works, {AudioFiles} audio files, {Failures} failed folders", finalWorks.Count, allAudioFiles.Count, _failedFolders.Count);

        if (_failedFolders.Count > 0)
        {
            var failedMsg = $"Scan finished but {_failedFolders.Count} folder{( _failedFolders.Count == 1 ? "" : "s")} could not be accessed:\n{string.Join("\n", _failedFolders.Take(5))}{( _failedFolders.Count > 5 ? $"\n...and {_failedFolders.Count - 5} more" : "")}";
            _scanStateService.MarkFailed(failedMsg, _failedFolders.Count, _failedFolders);
        }
        else
        {
            _scanStateService.MarkCompleted(finalWorks.Count, allAudioFiles.Count);
        }

        return new ScanResult { Works = finalWorks, FailedFolders = _failedFolders, TotalFiles = allAudioFiles.Count };
    }

    private async Task FindCoverImageFallbackAsync(WorkMetadata work, string workPath, bool isCloud, string rjCode)
    {
        if (isCloud)
        {
            try
            {
                var folderFiles = await _cloudProvider.ListFilesAsync(workPath);
                var remoteImage = folderFiles.FirstOrDefault(f => !f.IsDirectory &&
                    (f.Path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     f.Path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)));

                if (remoteImage != null)
                {
                    var localCoverPath = GetLocalCoverCachePath(workPath, rjCode);
                    using var stream = await _cloudProvider.GetFileStreamAsync(remoteImage.Path);
                    using var fs = new FileStream(localCoverPath, FileMode.Create, FileAccess.Write);
                    await stream.CopyToAsync(fs);
                    work.CoverImagePath = localCoverPath;
                }
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Local cover fallback failed"); }
        }
        else
        {
            var localImages = Directory.GetFiles(workPath, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (localImages.Any())
            {
                work.CoverImagePath = localImages.First();
            }
        }
    }

    private string? GetCloudDirectoryName(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var cleanPath = path.Replace('\\', '/').TrimEnd('/');
        var lastSlash = cleanPath.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            return cleanPath.Substring(0, lastSlash);
        }
        return string.Empty;
    }

    private string GetCloudFileName(string path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        var cleanPath = path.Replace('\\', '/');
        var lastSlash = cleanPath.LastIndexOf('/');
        if (lastSlash >= 0)
        {
            return cleanPath.Substring(lastSlash + 1);
        }
        return cleanPath;
    }

    /// <summary>
    /// A cached work is stale when its last fetch failed, or when it still carries a title that
    /// only a non-product page could have produced (the site name, or the parser's placeholder).
    /// </summary>
    private static bool IsStaleCachedWork(WorkMetadata work) =>
        work.NeedsMetadataRefresh ||
        string.Equals(work.Title, "DLsite", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(work.Title, "Unknown Title", StringComparison.OrdinalIgnoreCase);

    private string DetermineWorkFolder(string filePath, string basePath, bool isCloud)
    {
        if (isCloud)
        {
            var dir = GetCloudDirectoryName(filePath);
            var current = dir;
            var baseNorm = basePath.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(baseNorm)) baseNorm = "/";

            while (current != null && current.Length >= baseNorm.Length)
            {
                var folderName = GetCloudFileName(current);
                if (Regex.IsMatch(folderName, @"(RJ\d{6,8})", RegexOptions.IgnoreCase))
                {
                    return current;
                }
                current = GetCloudDirectoryName(current);
            }

            if (dir == null || string.Equals(dir, baseNorm, StringComparison.OrdinalIgnoreCase))
            {
                return basePath;
            }

            var parent = GetCloudDirectoryName(dir);
            if (parent == null || string.Equals(parent, baseNorm, StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }

            var dirName = GetCloudFileName(dir);
            if (Regex.IsMatch(dirName, @"^(disc|cd|vol|volume|bonus|track|side|part|effect|se)\b", RegexOptions.IgnoreCase))
            {
                return parent;
            }

            return dir;
        }
        else
        {
            var dir = Path.GetDirectoryName(filePath);
            var current = dir;

            while (current != null && current.Length >= basePath.Length && current.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                var folderName = Path.GetFileName(current);
                if (Regex.IsMatch(folderName, @"(RJ\d{6,8})", RegexOptions.IgnoreCase))
                {
                    return current;
                }
                current = Path.GetDirectoryName(current);
            }

            if (dir == null || dir.Equals(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return basePath;
            }

            var parent = Path.GetDirectoryName(dir);
            if (parent == null || parent.Equals(basePath, StringComparison.OrdinalIgnoreCase))
            {
                return dir;
            }

            var dirName = Path.GetFileName(dir);
            if (Regex.IsMatch(dirName, @"^(disc|cd|vol|volume|bonus|track|side|part|effect|se)\b", RegexOptions.IgnoreCase))
            {
                return parent;
            }

            return dir;
        }
    }

    private int GetExtensionPriority(string ext)
    {
        return ext.ToLower() switch
        {
            ".flac" => 1,
            ".wav" => 2,
            ".mp3" => 3,
            ".m4a" => 4,
            ".mp4" => 5,
            _ => 99
        };
    }

    /// <summary>Walks a folder on the local disk. Listing failures surface as exceptions and are
    /// recorded by <see cref="ScanListing"/>.</summary>
    private sealed class LocalScanFileSource : IScanFileSource
    {
        public Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            var entries = new List<ScanEntry>();

            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                entries.Add(new ScanEntry(directory, isDirectory: true));
            }

            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly))
            {
                entries.Add(new ScanEntry(file, isDirectory: false));
            }

            return Task.FromResult<IReadOnlyList<ScanEntry>>(entries);
        }
    }

    /// <summary>Walks a WebDAV folder through the configured cloud provider.</summary>
    private sealed class CloudScanFileSource : IScanFileSource
    {
        private readonly Services.Cloud.ICloudProvider _cloudProvider;

        public CloudScanFileSource(Services.Cloud.ICloudProvider cloudProvider)
        {
            _cloudProvider = cloudProvider;
        }

        public async Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            var items = await _cloudProvider.ListFilesAsync(path).ConfigureAwait(false);
            return items
                .Select(item => new ScanEntry(item.Path, item.IsDirectory))
                .ToList();
        }
    }

    private string GetLocalCoverCachePath(string workPath, string? productId)
    {
        if (!string.IsNullOrEmpty(productId))
        {
            return Path.Combine(FileSystem.CacheDirectory, $"{productId}_cover.jpg");
        }
        else
        {
            var cleanPath = string.Concat(workPath.Split(Path.GetInvalidFileNameChars()));
            if (cleanPath.Length > 50) cleanPath = cleanPath.Substring(cleanPath.Length - 50);
            return Path.Combine(FileSystem.CacheDirectory, $"cover_{cleanPath}.jpg");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}