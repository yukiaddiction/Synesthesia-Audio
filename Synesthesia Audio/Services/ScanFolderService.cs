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
        _failedFolders = new List<string>();
        var settings = _settingsService.CurrentSettings;
        bool isCloud = settings.StorageType == "WebDAV";
        _logger.LogInformation("Library scan started — isCloud: {IsCloud}, path: {BasePath}", isCloud, basePath);
        _scanStateService.MarkRunning("Scan Library Found 0 Work, 0 Audio Files");

        void Report(double pct, string msg)
        {
            progress?.Report((pct, msg));
            _scanStateService.UpdateProgress(pct, msg);
        }

        Report(0, "Scan Library Found 0 Work, 0 Audio Files");

        var workGroups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        List<string> allAudioFiles = new();

        if (isCloud)
        {
            if (!_cloudProvider.IsConfigured)
            {
                _scanStateService.MarkFailed("WebDAV is not configured. Add your server details in Settings.");
                return new ScanResult();
            }
            await ScanCloudDirectoryRecursiveAsync(basePath, basePath, allAudioFiles, workGroups, (worksCount, filesCount) =>
            {
                Report(0.15 * Math.Min(1.0, filesCount / 500.0),
                    $"Scan Library Found {worksCount} Work{(worksCount == 1 ? "" : "s")}, {filesCount} Audio File{(filesCount == 1 ? "" : "s")}");
            });
        }
        else
        {
            if (!Directory.Exists(basePath))
            {
                _scanStateService.MarkFailed($"The configured folder no longer exists: {basePath}");
                return new ScanResult();
            }
            await ScanLocalDirectoryRecursiveAsync(basePath, basePath, allAudioFiles, workGroups, (worksCount, filesCount) =>
            {
                Report(0.15 * Math.Min(1.0, filesCount / 500.0),
                    $"Scan Library Found {worksCount} Work{(worksCount == 1 ? "" : "s")}, {filesCount} Audio File{(filesCount == 1 ? "" : "s")}");
            });
        }

        int totalFoundWorks = workGroups.Count;
        int totalFoundFiles = allAudioFiles.Count;
        Report(0.15, $"Scan Library Found {totalFoundWorks} Work{(totalFoundWorks == 1 ? "" : "s")}, {totalFoundFiles} Audio File{(totalFoundFiles == 1 ? "" : "s")}");
        _logger.LogInformation("Scan discovery complete — {Folders} folders, {Files} files", totalFoundWorks, totalFoundFiles);

        var pendingWorks = new List<(WorkMetadata Work, string WorkPath, string? RjCode, List<string> Files)>();
        var readyWorks = new List<(WorkMetadata Work, List<string> Files)>();

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
            string? rjCode = null;

            if (cachedWork == null && match.Success)
            {
                rjCode = match.Groups[1].Value.ToUpper();
                cachedWork = _cacheService.GetWorkByProductId(rjCode);
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
                        var meta = await _dlsiteService.GetWorkMetadataAsync(rjCode!);
                        if (!string.IsNullOrEmpty(meta.Title))
                        {
                            work.Title = meta.Title;
                            work.TranslatedTitle = meta.TranslatedTitle;
                            work.Circle = meta.Circle;
                            work.VA = meta.VA;
                            work.AgeRating = meta.AgeRating;
                            work.Genre = meta.Genre;

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

                    var done = Interlocked.Increment(ref completedMeta);
                    Report(0.15 + 0.80 * done / totalMeta, $"scan: fetching metadata ({done}/{totalMeta})");
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

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

        _cacheService.ReplaceAll(finalWorks);

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

    private async Task ScanLocalDirectoryRecursiveAsync(
        string currentDir,
        string basePath,
        List<string> allAudioFiles,
        Dictionary<string, List<string>> workGroups,
        Action<int, int> onProgress)
    {
        try
        {
            var filesInDir = Directory.EnumerateFiles(currentDir, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => AppConstants.IsMediaFile(f))
                .ToList();

            if (filesInDir.Count > 0)
            {
                foreach (var file in filesInDir)
                {
                    var workFolder = DetermineWorkFolder(file, basePath, isCloud: false);
                    if (!workGroups.TryGetValue(workFolder, out var list))
                    {
                        list = new List<string>();
                        workGroups[workFolder] = list;
                    }
                    list.Add(file);
                    allAudioFiles.Add(file);
                }

                onProgress(workGroups.Count, allAudioFiles.Count);
                await Task.Delay(15);
            }

            var subDirs = Directory.GetDirectories(currentDir);
            foreach (var subDir in subDirs)
            {
                await ScanLocalDirectoryRecursiveAsync(subDir, basePath, allAudioFiles, workGroups, onProgress);
            }
        }
        catch (Exception ex)
        {
            _failedFolders.Add(currentDir);
            _logger.LogWarning(ex, "Local scan error at {Directory}", currentDir);
        }
    }

    private async Task ScanCloudDirectoryRecursiveAsync(
        string currentPath,
        string basePath,
        List<string> allAudioFiles,
        Dictionary<string, List<string>> workGroups,
        Action<int, int> onProgress)
    {
        try
        {
            var items = await _cloudProvider.ListFilesAsync(currentPath);
            var filesInDir = items.Where(i => !i.IsDirectory)
                .Select(i => i.Path)
                .Where(f => AppConstants.IsMediaFile(f) && !AppConstants.IsSystemFileOrDir(f))
                .ToList();

            if (filesInDir.Count > 0)
            {
                foreach (var file in filesInDir)
                {
                    var workFolder = DetermineWorkFolder(file, basePath, isCloud: true);
                    if (!workGroups.TryGetValue(workFolder, out var list))
                    {
                        list = new List<string>();
                        workGroups[workFolder] = list;
                    }
                    list.Add(file);
                    allAudioFiles.Add(file);
                }

                onProgress(workGroups.Count, allAudioFiles.Count);
                await Task.Delay(15);
            }

            var subDirs = items.Where(i => i.IsDirectory && !AppConstants.IsSystemFileOrDir(i.Path)).Select(i => i.Path);
            foreach (var subDir in subDirs)
            {
                await ScanCloudDirectoryRecursiveAsync(subDir, basePath, allAudioFiles, workGroups, onProgress);
            }
        }
        catch (Exception ex)
        {
            _failedFolders.Add(currentPath);
            _logger.LogWarning(ex, "Cloud scan error at {Path}", currentPath);
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