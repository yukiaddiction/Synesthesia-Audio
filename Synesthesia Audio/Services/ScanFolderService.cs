using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.IO;
using System.Net.Http;
using System.Linq;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public partial class ScanFolderService : IDisposable
{
    private readonly DLsiteService _dlsiteService;
    private readonly LibraryCacheService _cacheService;
    private readonly SettingsService _settingsService;
    private readonly ScanStateService _scanStateService;
    private readonly Services.Cloud.ICloudProvider _cloudProvider;
    private readonly HttpClient _httpClient;
    private readonly CoverThumbnailer _thumbnailer;
    private readonly ILogger<ScanFolderService> _logger;

    // A DLsite product code: "RJ" followed by 6-8 digits, anywhere in the name (case-insensitive).
    [System.Text.RegularExpressions.GeneratedRegex(@"(RJ\d{6,8})",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex RjCodePattern();

    public ScanFolderService(DLsiteService dlsiteService, LibraryCacheService cacheService, SettingsService settingsService, ScanStateService scanStateService, Services.Cloud.ICloudProvider cloudProvider, IHttpClientFactory httpClientFactory, CoverThumbnailer thumbnailer, ILogger<ScanFolderService> logger)
    {
        _dlsiteService = dlsiteService;
        _cacheService = cacheService;
        _settingsService = settingsService;
        _scanStateService = scanStateService;
        _cloudProvider = cloudProvider;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient(HttpClients.Cover);
        _thumbnailer = thumbnailer;
    }

    public async Task<(string? Title, string? TranslatedTitle, string? Circle, string? VA, string? AgeRating, string? Genre, string? CoverImagePath)>
        FetchDlsiteMetadataAsync(string rjCode, string folderPath)
    {
        var meta = await _dlsiteService.GetWorkMetadataAsync(rjCode);

        string? coverPath = null;
        if (!string.IsNullOrEmpty(meta.CoverUrl))
        {
            var localCoverPath = GetLocalCoverCachePath(folderPath, rjCode);
            if (await TryDownloadCoverAsync(meta.CoverUrl, localCoverPath).ConfigureAwait(false))
            {
                coverPath = localCoverPath;
            }
            else if (CoverUrlPolicy.IsAllowed(meta.CoverUrl))
            {
                // Only the download failed; the remote URL is still usable.
                coverPath = meta.CoverUrl;
            }
        }

        if (string.IsNullOrEmpty(coverPath) && !string.IsNullOrEmpty(folderPath) && !Synesthesia_Audio.Core.StorageTypes.IsWebDav(_settingsService.CurrentSettings.StorageType))
        {
            try
            {
                coverPath = CoverResolver.ChooseBestLocal(folderPath);
            }
            catch (Exception ex) { _logger.LogDebug(ex, "Local cover fallback failed"); }
        }

        return (meta.Title, meta.TranslatedTitle, meta.Circle, meta.VA, meta.AgeRating, meta.Genre, coverPath);
    }

    /// <summary>Quick top-level check for folders not yet in the cache, without a full rescan.</summary>
    public async Task<List<WorkMetadata>> CheckForNewWorksAsync(string basePath, IEnumerable<string> cachedPaths)
    {
        var settings = _settingsService.CurrentSettings;
        bool isCloud = Synesthesia_Audio.Core.StorageTypes.IsWebDav(settings.StorageType);
        _logger.LogInformation("Checking for new works — isCloud: {IsCloud}", isCloud);
        var newWorks = new List<WorkMetadata>();

        // Same trimming as the full scan, so both produce comparable work paths.
        basePath = NormalizeBasePath(basePath, isCloud);

        var cachedPathList = cachedPaths.Where(p => !string.IsNullOrEmpty(p)).ToList();

        // With nothing cached every folder looks new: this would become a second full scan racing the real one
        // and duplicating every work.
        if (cachedPathList.Count == 0 || IsScanRunning())
        {
            _logger.LogInformation("Check for new works skipped — {Reason}",
                cachedPathList.Count == 0 ? "library not scanned yet" : "a full scan is running");
            return newWorks;
        }

        List<(string Path, string? ETag)> folders;

        if (isCloud)
        {
            if (!_cloudProvider.IsConfigured) return newWorks;
            try
            {
                var items = await _cloudProvider.ListFilesAsync(basePath);
                folders = items
                    .Where(i => i.IsDirectory && !AppConstants.IsSystemFileOrDir(i.Path))
                    .Select(i => (i.Path, i.ETag))
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
                .Where(d => !string.IsNullOrEmpty(Path.GetFileName(d)) && !AppConstants.IsSystemFileOrDir(d))
                .Select(d => (d, (string?)null))
                .ToList();
        }

        // A folder that merely contains cached works (e.g. a circle folder of RJ works) is not new either.
        var newFolders = folders
            .Where(f => !cachedPathList.Any(cached => ScanListing.IsUnderOrEqual(cached, f.Path)))
            .ToList();

        if (newFolders.Count == 0) return newWorks;

        foreach (var (path, eTag) in newFolders)
        {
            try
            {
                var works = await CreateWorksFromFolderAsync(path, eTag, basePath, isCloud).ConfigureAwait(false);

                // A full scan that started meanwhile owns the cache; writing now would duplicate.
                if (IsScanRunning())
                {
                    _logger.LogInformation("Check for new works abandoned — a full scan started");
                    return new List<WorkMetadata>();
                }

                foreach (var work in works)
                {
                    _cacheService.UpdateWork(work);
                    newWorks.Add(work);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CheckForNewWorks: folder {Path} could not be processed", path);
            }
        }

        _logger.LogInformation("Check for new works complete — {Count} new works found", newWorks.Count);
        return newWorks;
    }

    /// <summary>
    /// Same rules as the full scan: the work is the RJ folder (or the top-level folder without a code) and every
    /// folder beneath it is a section.
    /// </summary>
    private async Task<List<WorkMetadata>> CreateWorksFromFolderAsync(string path, string? folderETag, string basePath, bool isCloud)
    {
        var works = new List<WorkMetadata>();

        // List audio recursively before any DLsite request, so a work whose files live only in subfolders (CD1/)
        // isn't fetched and then dropped on every launch.
        var listing = await ListAudioFilesRecursiveAsync(path, isCloud).ConfigureAwait(false);
        var audioFiles = listing.AudioFiles;
        if (audioFiles.Count == 0) return works;

        var eTags = new Dictionary<string, string>(listing.DirectoryETags, StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(folderETag)) eTags[ScanListing.NormalizePath(path)] = folderETag;

        var workOf = WorkFolderResolver.ResolveAll(audioFiles, basePath, isCloud);
        foreach (var group in audioFiles.GroupBy(f => workOf[f], StringComparer.OrdinalIgnoreCase))
        {
            var workPath = group.Key;
            var folderName = isCloud ? GetCloudFileName(workPath.TrimEnd('/')) : Path.GetFileName(workPath);
            if (string.IsNullOrEmpty(folderName)) folderName = workPath;

            var match = RjCodePattern().Match(folderName);
            string? rjCode = match.Success ? match.Groups[1].Value.ToUpper() : null;

            var work = new WorkMetadata
            {
                FolderPath = workPath,
                Title = folderName,
                AgeRating = AgeRatings.Unknown
            };

            if (rjCode != null)
            {
                var cachedByRj = _cacheService.GetWorkByProductId(rjCode);
                if (cachedByRj != null && !IsStaleCachedWork(cachedByRj))
                {
                    work.Title = cachedByRj.Title;
                    work.TranslatedTitle = cachedByRj.TranslatedTitle;
                    work.CoverImagePath = cachedByRj.CoverImagePath;
                    work.CoverUrl = cachedByRj.CoverUrl;
                    work.Circle = cachedByRj.Circle;
                    work.VA = cachedByRj.VA;
                    work.AgeRating = cachedByRj.AgeRating;
                    work.ProductId = cachedByRj.ProductId;
                    work.Genre = cachedByRj.Genre;
                }
                else
                {
                    work.ProductId = rjCode;
                    var meta = await FetchDlsiteMetadataAsync(rjCode, workPath).ConfigureAwait(false);

                    work.Title = !string.IsNullOrEmpty(meta.Title) ? meta.Title : folderName;
                    work.TranslatedTitle = meta.TranslatedTitle ?? "";
                    work.Circle = meta.Circle ?? "Unknown Circle";
                    work.VA = meta.VA ?? "";
                    work.AgeRating = meta.AgeRating ?? AgeRatings.Unknown;
                    work.Genre = meta.Genre ?? "";
                    work.CoverImagePath = meta.CoverImagePath ?? "";

                    // Retry a failed fetch later instead of freezing the folder name into the cache.
                    work.NeedsMetadataRefresh = string.IsNullOrEmpty(meta.Title);
                }
            }

            foreach (var subAlbum in BuildSubAlbums(group, workPath, isCloud, cachedWork: null, previous: null))
                work.SubAlbums.Add(subAlbum);

            if (isCloud) RememberSource(work, workPath, basePath, audioFiles, eTags);

            works.Add(work);
        }

        return works;
    }

    private async Task<ScanListingResult> ListAudioFilesRecursiveAsync(string path, bool isCloud)
    {
        IScanFileSource source = isCloud
            ? new CloudScanFileSource(_cloudProvider)
            : new LocalScanFileSource();

        return await ScanListing
            .CollectAudioFilesAsync(source, path, AppConstants.IsMediaFile, AppConstants.IsSystemFileOrDir)
            .ConfigureAwait(false);
    }

    public async Task<ScanResult> ScanLibraryAsync(string basePath, IProgress<(double, string)>? progress = null, CancellationToken cancellationToken = default)
    {
        // The cache must be fully read before deciding which works are stale or new.
        await _cacheService.Loaded.ConfigureAwait(false);

        // Local, not a field, so overlapping scans can't clobber each other's list.
        var failedFolders = new List<string>();
        var settings = _settingsService.CurrentSettings;
        bool isCloud = Synesthesia_Audio.Core.StorageTypes.IsWebDav(settings.StorageType);
        basePath = NormalizeBasePath(basePath, isCloud);
        _logger.LogInformation("Library scan started — isCloud: {IsCloud}, path: {BasePath}", isCloud, basePath);
        _scanStateService.MarkRunning("Scanning — 0 works, 0 audio files");

        DLsiteService.ResetScanDiagnostics();

        void Report(double pct, string msg)
        {
            progress?.Report((pct, msg));
            _scanStateService.UpdateProgress(pct, msg);
        }

        Report(0, "Scanning — 0 works, 0 audio files");

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

        // The failures this walk records are what stop a partial listing being saved as the whole library.
        IScanFileSource fileSource = isCloud
            ? new CloudScanFileSource(_cloudProvider)
            : new LocalScanFileSource();

        var discoveryClock = System.Diagnostics.Stopwatch.StartNew();
        ScanListingResult listing;
        try
        {
            // Discovery fills the first 15% of the bar, saturating at 500 files since the total is unknown yet.
            listing = await ScanListing.CollectAudioFilesAsync(
                fileSource,
                basePath,
                AppConstants.IsMediaFile,
                AppConstants.IsSystemFileOrDir,
                filesFound => Report(0.15 * Math.Min(1.0, filesFound / 500.0),
                    $"Scanning — {filesFound} audio file{(filesFound == 1 ? "" : "s")}"),
                cancellationToken,
                reuseSubtree: isCloud ? BuildSubtreeReuse() : null).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Never publish a partial library over a good cache.
            _logger.LogInformation("Library scan cancelled during discovery");
            _scanStateService.MarkInterrupted("Scan cancelled.");
            return new ScanResult();
        }

        failedFolders = listing.FailedPaths;
        _logger.LogInformation(
            "Scan listing took {Seconds:0.0}s — {Requests} request(s), whole tree in one: {Tree}, unchanged folders reused: {Reused}",
            discoveryClock.Elapsed.TotalSeconds, listing.ListRequests, listing.UsedTreeListing, listing.ReusedFolders);

        if (listing.RootFailed)
        {
            // Nothing is known about the library, so leave the cache untouched.
            var reason = $"The library folder could not be read: {basePath}. Your library was left unchanged.";
            _logger.LogError("Scan aborted — root listing failed for {BasePath}", basePath);
            _scanStateService.MarkFailed(reason, listing.FailedPaths.Count, listing.FailedPaths);
            return new ScanResult { FailedFolders = listing.FailedPaths };
        }

        allAudioFiles = listing.AudioFiles;
        var workOf = WorkFolderResolver.ResolveAll(allAudioFiles, basePath, isCloud);
        foreach (var file in allAudioFiles)
        {
            var workFolder = workOf[file];
            if (!workGroups.TryGetValue(workFolder, out var fileList))
            {
                fileList = new List<string>();
                workGroups[workFolder] = fileList;
            }
            fileList.Add(file);
        }

        int totalFoundWorks = workGroups.Count;
        int totalFoundFiles = allAudioFiles.Count;
        Report(0.15, $"Scanning — {totalFoundWorks} work{(totalFoundWorks == 1 ? "" : "s")}, {totalFoundFiles} audio file{(totalFoundFiles == 1 ? "" : "s")}");
        _logger.LogInformation("Scan discovery complete — {Folders} folders, {Files} files", totalFoundWorks, totalFoundFiles);

        // Taken now because metadata refresh overwrites cached works with untranslated copies before the
        // track loop reads them.
        var previousTranslations = SnapshotCachedTranslations();

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

            var match = RjCodePattern().Match(folderName);

            var work = new WorkMetadata
            {
                FolderPath = workPath,
                Title = folderName,
                AgeRating = AgeRatings.Unknown
            };

            var cachedWork = _cacheService.GetWork(workPath);

            // Stale entries (failed fetches, placeholder titles) are refetched so libraries heal themselves.
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
                _logger.LogInformation("Work has no RJ code and cannot be scraped: {Folder}", folderName);
                readyWorks.Add((work, files));
            }
        }
        Report(0.15, $"Fetching metadata — 0/{pendingWorks.Count}");
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
                    // Checked per work so cancel stops new requests without threading a token through the scraper.
                    if (cancellationToken.IsCancellationRequested) return;
                    var (work, workPath, rjCode, _) = pw;

                    try
                    {
                        // Assume failure until a title comes back, so an outage is retried next scan.
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
                                // Kept so the cover can be re-downloaded without scraping again.
                                work.CoverUrl = meta.CoverUrl;

                                var localCoverPath = GetLocalCoverCachePath(workPath, rjCode!);
                                if (await TryDownloadCoverAsync(meta.CoverUrl, localCoverPath).ConfigureAwait(false))
                                {
                                    work.CoverImagePath = localCoverPath;
                                }
                                else if (CoverUrlPolicy.IsAllowed(meta.CoverUrl))
                                {
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
                    // Metadata spans 15%..95% of the bar.
                    Report(0.15 + 0.80 * done / totalMeta, $"Fetching metadata — {done}/{totalMeta}");
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

            foreach (var pw in pendingWorks)
            {
                readyWorks.Add((pw.Work, pw.Files));
            }
        }

        var finalWorks = new List<WorkMetadata>();

        foreach (var (work, files) in readyWorks)
        {
            // This loop is the long tail of a scan, so a cancel has to land here.
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Library scan cancelled before writing results");
                _scanStateService.MarkInterrupted("Scan cancelled.");
                return new ScanResult { FailedFolders = failedFolders, TotalFiles = allAudioFiles.Count };
            }

            var workPath = work.FolderPath;

            var cachedWork = _cacheService.GetWork(workPath)
                          ?? _cacheService.GetWorkByProductId(work.ProductId);

            var previous = previousTranslations.TryGetValue(workPath, out var snapshot) ? snapshot : null;

            // A refetch that only produced the original title must not wipe the earlier translation.
            if (string.IsNullOrEmpty(work.TranslatedTitle) && !string.IsNullOrEmpty(previous?.WorkTranslatedTitle))
            {
                work.TranslatedTitle = previous.WorkTranslatedTitle!;
            }

            foreach (var subAlbum in BuildSubAlbums(files, workPath, isCloud, cachedWork, previous))
                work.SubAlbums.Add(subAlbum);

            // Shows in the log which folders became sections and which files were dropped as duplicate formats.
            var shown = work.SubAlbums.Sum(sa => sa.Tracks.Count);
            _logger.LogInformation("Album {Work}: {Files} file(s), {Tracks} track(s) shown in {Count} section(s): {Sections}",
                workPath, files.Count, shown, work.SubAlbums.Count,
                string.Join(" | ", work.SubAlbums.Select(sa => $"{sa.Title} ({sa.Tracks.Count})")));

            if (isCloud) RememberSource(work, workPath, basePath, allAudioFiles, listing.DirectoryETags);

            if (work.SubAlbums.Any())
            {
                if (await TryRestoreCachedCoverAsync(work, workPath))
                {
                    _logger.LogInformation("Restored missing cover for {Title}", work.Title);
                }

                // No per-work cache write: ReplaceAll below persists the whole list once.
                finalWorks.Add(work);
            }
        }

        if (LibraryCacheMergePolicy.CanReplaceWholeCache(listing))
        {
            _cacheService.ReplaceAll(finalWorks);
        }
        else
        {
            // Keep cached works under unreadable folders: they are probably still there, and a wholesale replace
            // would lose their metadata, covers and translations.
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

        Report(1.0, "Scan complete");
        _logger.LogInformation("Library scan complete — {Count} works, {AudioFiles} audio files, {Failures} failed folders", finalWorks.Count, allAudioFiles.Count, failedFolders.Count);

        if (failedFolders.Count > 0)
        {
            var failedMsg = $"Scan finished but {failedFolders.Count} folder{( failedFolders.Count == 1 ? "" : "s")} could not be accessed:\n{string.Join("\n", failedFolders.Take(5))}{( failedFolders.Count > 5 ? $"\n...and {failedFolders.Count - 5} more" : "")}";
            _scanStateService.MarkFailed(failedMsg, failedFolders.Count, failedFolders);
        }
        else
        {
            _scanStateService.MarkCompleted(finalWorks.Count, allAudioFiles.Count, failedFolders.Count);
        }

        return new ScanResult { Works = finalWorks, FailedFolders = failedFolders, TotalFiles = allAudioFiles.Count };
    }

    private async Task FindCoverImageFallbackAsync(WorkMetadata work, string workPath, bool isCloud, string rjCode)
    {
        if (isCloud)
        {
            try
            {
                var folderFiles = await _cloudProvider.ListFilesAsync(workPath);
                // Same ranking as a local folder, so "cover.jpg" beats a bigger screenshot.
                var remotePath = LocalCoverFinder.SelectBest(folderFiles
                    .Where(f => !f.IsDirectory)
                    .Select(f => (f.Path, f.Size)));
                var remoteImage = remotePath == null ? null : folderFiles.First(f => f.Path == remotePath);

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
            var best = CoverResolver.ChooseBestLocal(workPath);
            if (best != null) work.CoverImagePath = best;
        }
    }

    /// <summary>
    /// One section per folder (rules tested in Core's SectionBuilder); within a section, a track present in several
    /// formats keeps only the preferred copy. Shared by the full scan and the quick check so both group alike.
    /// </summary>
    private static List<SubAlbum> BuildSubAlbums(
        IEnumerable<string> files,
        string workPath,
        bool isCloud,
        WorkMetadata? cachedWork,
        CachedTranslations? previous)
    {
        var result = new List<SubAlbum>();

        foreach (var section in Synesthesia_Audio.Core.SectionBuilder.Build(files, workPath))
        {
            var subAlbum = new SubAlbum { Title = section.Title, FolderPath = workPath };
            var tracks = new List<Track>();

            foreach (var group in section.Files.GroupBy(TitleOf, StringComparer.OrdinalIgnoreCase))
            {
                var bestFile = group.OrderBy(f => GetExtensionPriority(Path.GetExtension(f.Replace('\\', '/')))).First();
                var trackTitle = TitleOf(bestFile);

                var cachedTrack = cachedWork?.SubAlbums
                    .FirstOrDefault(sa => sa.Title.Equals(section.Title, StringComparison.OrdinalIgnoreCase))?
                    .Tracks.FirstOrDefault(t => t.Title.Equals(trackTitle, StringComparison.OrdinalIgnoreCase));

                var translatedTitle = cachedTrack?.TranslatedTitle ?? string.Empty;
                if (translatedTitle.Length == 0 && previous != null)
                {
                    translatedTitle = previous.GetTrackTranslation(section.Title, trackTitle) ?? string.Empty;
                }

                tracks.Add(new Track { Title = trackTitle, FilePath = bestFile, TranslatedTitle = translatedTitle });
            }

            foreach (var track in tracks.OrderBy(t => t.Title)) subAlbum.Tracks.Add(track);
            result.Add(subAlbum);
        }

        return result;

        string TitleOf(string file) => isCloud
            ? Path.GetFileNameWithoutExtension(GetCloudFileName(file))
            : Path.GetFileNameWithoutExtension(file);
    }

    /// <summary>
    /// Stores the folder's ETag and every audio file beneath it so the next scan can skip an unchanged folder.
    /// Nested works' files are included since skipping the folder skips them too; the root is never skipped.
    /// </summary>
    private static void RememberSource(
        WorkMetadata work,
        string workPath,
        string basePath,
        List<string> allAudioFiles,
        IReadOnlyDictionary<string, string> directoryETags)
    {
        work.SourceETag = string.Empty;
        work.SourceFiles = new List<string>();

        if (ScanListing.IsSameDirectory(workPath, basePath)) return;
        if (!directoryETags.TryGetValue(ScanListing.NormalizePath(workPath), out var tag) || string.IsNullOrEmpty(tag)) return;

        work.SourceETag = tag;
        work.SourceFiles = allAudioFiles.Where(f => ScanListing.IsUnderOrEqual(f, workPath)).ToList();
    }

    /// <summary>
    /// Only trusted on servers whose folder ETag changes when anything beneath it does (Nextcloud, ownCloud, oCIS);
    /// elsewhere an ETag covers direct children only, so every folder is still listed.
    /// </summary>
    private Func<ScanEntry, IReadOnlyList<string>?> BuildSubtreeReuse()
    {
        var known = new Dictionary<string, WorkMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var work in _cacheService.GetAllWorks())
        {
            if (string.IsNullOrEmpty(work.FolderPath) || string.IsNullOrEmpty(work.SourceETag) ||
                work.SourceFiles == null || work.SourceFiles.Count == 0)
            {
                continue;
            }

            known[ScanListing.NormalizePath(work.FolderPath)] = work;
        }

        return entry =>
        {
            if (!_cloudProvider.PropagatesFolderETags || string.IsNullOrEmpty(entry.ETag)) return null;
            if (!known.TryGetValue(ScanListing.NormalizePath(entry.Path), out var work)) return null;

            return string.Equals(work.SourceETag, entry.ETag, StringComparison.Ordinal) ? work.SourceFiles : null;
        };
    }

    private sealed class CachedTranslations
    {
        public string? WorkTranslatedTitle { get; init; }

        private readonly Dictionary<string, string> _tracks = new(StringComparer.OrdinalIgnoreCase);

        public bool IsEmpty => _tracks.Count == 0 && string.IsNullOrEmpty(WorkTranslatedTitle);

        public void AddTrack(string subAlbumTitle, string trackTitle, string translated) =>
            _tracks[Key(subAlbumTitle, trackTitle)] = translated;

        public string? GetTrackTranslation(string subAlbumTitle, string trackTitle) =>
            _tracks.TryGetValue(Key(subAlbumTitle, trackTitle), out var value) ? value : null;

        // A control-character separator that won't appear in real titles keeps the composite key unambiguous.
        private static string Key(string subAlbumTitle, string trackTitle) =>
            subAlbumTitle + "\u0001" + trackTitle;
    }

    private Dictionary<string, CachedTranslations> SnapshotCachedTranslations()
    {
        var snapshot = new Dictionary<string, CachedTranslations>(StringComparer.OrdinalIgnoreCase);

        foreach (var cached in _cacheService.GetAllWorks())
        {
            if (string.IsNullOrEmpty(cached.FolderPath)) continue;

            var entry = new CachedTranslations { WorkTranslatedTitle = cached.TranslatedTitle };

            foreach (var subAlbum in cached.SubAlbums)
            {
                foreach (var track in subAlbum.Tracks)
                {
                    if (!string.IsNullOrEmpty(track.TranslatedTitle))
                    {
                        entry.AddTrack(subAlbum.Title, track.Title, track.TranslatedTitle);
                    }
                }
            }

            if (!entry.IsEmpty) snapshot[cached.FolderPath] = entry;
        }

        return snapshot;
    }

    private static string GetCloudFileName(string path) => WorkGrouper.GetRemoteFileName(path);

    /// <summary>
    /// Stale when the last fetch failed, or the title is one only a non-product page could produce (the site name,
    /// the parser's placeholder).
    /// </summary>
    private static bool IsStaleCachedWork(WorkMetadata work) =>
        work.NeedsMetadataRefresh ||
        string.Equals(work.Title, "DLsite", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(work.Title, "Unknown Title", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A trailing separator breaks relative-path comparisons. WebDAV listings have no leading "/" but the picker
    /// stores one, so cloud paths are trimmed at both ends (the server root becomes "").
    /// </summary>
    private static string NormalizeBasePath(string basePath, bool isCloud)
    {
        if (string.IsNullOrEmpty(basePath)) return basePath ?? string.Empty;
        if (isCloud) return basePath.Replace('\\', '/').Trim('/');
        if (basePath.Length <= 1) return basePath;

        var trimmed = basePath.TrimEnd('/', '\\');
        return trimmed.Length > 0 ? trimmed : basePath;
    }

    private bool IsScanRunning() => _scanStateService.Snapshot().Status == ScanStatus.Running;

    private static int GetExtensionPriority(string ext) => WorkGrouper.GetExtensionPriority(ext);

    /// <summary>
    /// The URL comes from a scraped page, so it is untrusted: https DLsite image host, image content type and a size
    /// cap are enforced, and a temp file keeps a rejected or truncated body from becoming the cover.
    /// </summary>
    private async Task<bool> TryDownloadCoverAsync(string? coverUrl, string localCoverPath)
    {
        if (!CoverUrlPolicy.IsAllowed(coverUrl))
        {
            _logger.LogWarning("Cover URL rejected — not an https DLsite image host: {Url}", LogRedactor.Url(coverUrl));
            return false;
        }

        var temporaryPath = localCoverPath + ".part";
        try
        {
            using var response = await _httpClient.GetAsync(coverUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return false;

            if (!CoverUrlPolicy.IsAcceptableLength(response.Content.Headers.ContentLength))
            {
                _logger.LogWarning("Cover rejected — declared size {Bytes} exceeds the cap",
                    response.Content.Headers.ContentLength);
                return false;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (!CoverUrlPolicy.IsAcceptableContentType(mediaType))
            {
                _logger.LogWarning("Cover rejected — content type {MediaType} is not an image", mediaType ?? "(none)");
                return false;
            }

            await using (var file = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write))
            {
                await response.Content.CopyToAsync(file).ConfigureAwait(false);
            }

            // A chunked response declares no length, so enforce the cap on what actually arrived.
            var downloaded = new FileInfo(temporaryPath).Length;
            if (downloaded == 0 || downloaded > CoverUrlPolicy.MaxCoverBytes)
            {
                File.Delete(temporaryPath);
                _logger.LogWarning("Cover rejected — {Bytes} bytes downloaded", downloaded);
                return false;
            }

            File.Move(temporaryPath, localCoverPath, overwrite: true);

            // Thumbnail once here rather than every time a view asks for the cover.
            _thumbnailer.EnsureThumbnails(localCoverPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cover download failed for {Url}", LogRedactor.Url(coverUrl));
            try
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
            catch
            {
                // A stray .part file is harmless.
            }
            return false;
        }
    }

    /// <summary>Lets exceptions escape: <see cref="ScanListing"/> records them as failed folders.</summary>
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

    /// <summary>Tries the whole WebDAV tree in one request; folder-by-folder only if the server refuses.</summary>
    private sealed class CloudScanFileSource : ITreeScanFileSource
    {
        private readonly Services.Cloud.ICloudProvider _cloudProvider;
        private readonly bool _tryTree;

        public CloudScanFileSource(Services.Cloud.ICloudProvider cloudProvider, bool tryTree = true)
        {
            _cloudProvider = cloudProvider;
            _tryTree = tryTree;
        }

        public async Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            var items = await _cloudProvider.ListFilesAsync(path).ConfigureAwait(false);
            return items
                .Select(item => new ScanEntry(item.Path, item.IsDirectory, item.ETag))
                .ToList();
        }

        public async Task<IReadOnlyList<ScanEntry>?> TryListTreeAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            if (!_tryTree) return null;

            var items = await _cloudProvider.TryListTreeAsync(rootPath, cancellationToken).ConfigureAwait(false);
            return items?
                .Select(item => new ScanEntry(item.Path, item.IsDirectory, item.ETag))
                .ToList();
        }
    }

    private static string GetLocalCoverCachePath(string workPath, string? productId) =>
        CoverResolver.GetCachePath(workPath, productId);

    /// <summary>
    /// Re-downloads a cached work's cover file that was deleted or cleared by the OS, from the stored URL: one
    /// request, no re-scrape, no "Reset RJ cache" needed.
    /// </summary>
    private async Task<bool> TryRestoreCachedCoverAsync(WorkMetadata work, string workPath)
    {
        if (!string.IsNullOrEmpty(work.CoverImagePath) && File.Exists(work.CoverImagePath))
            return false;

        // A remote URL is still usable; only a missing local file needs restoring.
        if (!string.IsNullOrEmpty(work.CoverImagePath) &&
            work.CoverImagePath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        work.CoverImagePath = string.Empty;

        if (string.IsNullOrEmpty(work.CoverUrl)) return false;

        var localCoverPath = GetLocalCoverCachePath(workPath, work.ProductId);
        if (!await TryDownloadCoverAsync(work.CoverUrl, localCoverPath).ConfigureAwait(false))
        {
            return false;
        }

        work.CoverImagePath = localCoverPath;
        return true;
    }

    /// <summary>The HTTP client is left alone: <c>IHttpClientFactory</c> owns its lifetime.</summary>
    public void Dispose()
    {
    }
}
