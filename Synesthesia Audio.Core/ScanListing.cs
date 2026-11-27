namespace Synesthesia_Audio.Core;

public sealed class ScanEntry
{
    public ScanEntry(string path, bool isDirectory, string? eTag = null)
    {
        Path = path;
        IsDirectory = isDirectory;
        ETag = eTag;
    }

    public string Path { get; }
    public bool IsDirectory { get; }

    /// <summary>WebDAV <c>getetag</c>, or <c>getlastmodified</c> when absent; null on local disk.</summary>
    public string? ETag { get; }
}

/// <summary>
/// One implementation per backend (local, WebDAV) so the walk and its failure bookkeeping exist once and are
/// testable without either.
/// </summary>
public interface IScanFileSource
{
    Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default);
}

public interface ITreeScanFileSource : IScanFileSource
{
    /// <summary>WebDAV <c>Depth: infinity</c>. Null means the server refused, so the walk falls back to per-folder listing; it is not an error.</summary>
    Task<IReadOnlyList<ScanEntry>?> TryListTreeAsync(string rootPath, CancellationToken cancellationToken = default);
}

/// <summary>Records what could not be read, because an incomplete walk must never be treated as an empty library.</summary>
public sealed class ScanListingResult
{
    public List<string> AudioFiles { get; } = new();

    public List<string> FailedPaths { get; } = new();

    public bool RootFailed { get; set; }

    public bool IsComplete => !RootFailed && FailedPaths.Count == 0;

    public Dictionary<string, string> DirectoryETags { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool UsedTreeListing { get; set; }

    public int ReusedFolders { get; set; }

    public int ListRequests { get; set; }
}

public static class ScanListing
{
    public const int ListAttempts = 3;

    /// <summary>
    /// Breadth-first walk that records failing folders and skips them instead of aborting; only a failed root is fatal.
    /// Folders are listed <paramref name="maxParallelism"/> at a time because WebDAV round trips dominate a cloud scan.
    /// A whole-tree listing is tried first when supported, and <paramref name="reuseSubtree"/> lets the caller supply
    /// a folder's files from the previous scan instead of listing it.
    /// </summary>
    public static async Task<ScanListingResult> CollectAudioFilesAsync(
        IScanFileSource source,
        string rootPath,
        Func<string, bool> isAudioFile,
        Func<string, bool>? isIgnoredPath = null,
        Action<int>? onAudioFileCount = null,
        CancellationToken cancellationToken = default,
        int maxParallelism = 4,
        Func<ScanEntry, IReadOnlyList<string>?>? reuseSubtree = null,
        TimeSpan? retryDelay = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(isAudioFile);
        if (maxParallelism < 1) maxParallelism = 1;

        var result = new ScanListingResult();

        if (source is ITreeScanFileSource treeSource)
        {
            IReadOnlyList<ScanEntry>? tree;
            try
            {
                result.ListRequests++;
                tree = await treeSource.TryListTreeAsync(rootPath, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Any failure here only means "list it the slow way"; the walk reports real failures.
                tree = null;
            }

            if (tree != null)
            {
                result.UsedTreeListing = true;
                AddTree(result, tree, rootPath, isAudioFile, isIgnoredPath);
                onAudioFileCount?.Invoke(result.AudioFiles.Count);
                return result;
            }
        }

        var pending = new Queue<string>();
        // A server that echoes the folder itself, or lists one folder twice, must not loop the walk.
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NormalizePath(rootPath) };
        pending.Enqueue(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // `pending` is only touched by this thread; only the listing calls run in parallel.
            var batch = new List<string>(maxParallelism);
            while (batch.Count < maxParallelism && pending.Count > 0) batch.Add(pending.Dequeue());

            result.ListRequests += batch.Count;
            var listings = await Task.WhenAll(batch.Select(ListOneAsync)).ConfigureAwait(false);

            foreach (var listing in listings)
            {
                if (listing.Error != null)
                {
                    result.FailedPaths.Add(listing.Directory);
                    if (IsSameDirectory(listing.Directory, rootPath)) result.RootFailed = true;
                    continue;
                }

                foreach (var entry in listing.Entries)
                {
                    if (isIgnoredPath != null && isIgnoredPath(entry.Path)) continue;

                    if (entry.IsDirectory)
                    {
                        if (!visited.Add(NormalizePath(entry.Path))) continue;

                        if (!string.IsNullOrEmpty(entry.ETag))
                            result.DirectoryETags[NormalizePath(entry.Path)] = entry.ETag!;

                        var known = reuseSubtree?.Invoke(entry);
                        if (known != null)
                        {
                            result.AudioFiles.AddRange(known);
                            result.ReusedFolders++;
                            continue;
                        }

                        pending.Enqueue(entry.Path);
                    }
                    else if (isAudioFile(entry.Path))
                    {
                        result.AudioFiles.Add(entry.Path);
                    }
                }
            }

            // Reported per batch so a slow WebDAV library still shows movement.
            onAudioFileCount?.Invoke(result.AudioFiles.Count);
        }

        return result;

        // Loaded WebDAV servers drop the odd connection mid-answer (pCloud: "response ended prematurely"), so retry
        // before recording a folder as unreadable and leaving its work out of the library.
        async Task<(string Directory, IReadOnlyList<ScanEntry> Entries, Exception? Error)> ListOneAsync(string directory)
        {
            var delay = retryDelay ?? TimeSpan.FromSeconds(1);
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var entries = await source.ListAsync(directory, cancellationToken).ConfigureAwait(false);
                    return (directory, entries, null);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    if (attempt >= ListAttempts) return (directory, Array.Empty<ScanEntry>(), ex);
                    lock (result) result.ListRequests++;
                }

                // Linear backoff: wait delay x attempt (1s, 2s, ...) between tries.
                if (delay > TimeSpan.Zero) await Task.Delay(delay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// The ignore rule looks at names, so every folder between root and entry is checked: a file inside "__MACOSX/x"
    /// is skipped even though its own name is fine.
    /// </summary>
    private static void AddTree(
        ScanListingResult result,
        IReadOnlyList<ScanEntry> tree,
        string rootPath,
        Func<string, bool> isAudioFile,
        Func<string, bool>? isIgnoredPath)
    {
        var root = NormalizePath(rootPath);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in tree)
        {
            var path = NormalizePath(entry.Path);
            if (IsSameDirectory(path, root) || !IsUnderOrEqual(path, root)) continue;
            if (!seen.Add(path)) continue;
            if (isIgnoredPath != null && HasIgnoredSegment(path, root, isIgnoredPath)) continue;

            if (entry.IsDirectory)
            {
                if (!string.IsNullOrEmpty(entry.ETag)) result.DirectoryETags[path] = entry.ETag!;
            }
            else if (isAudioFile(entry.Path))
            {
                result.AudioFiles.Add(entry.Path);
            }
        }
    }

    private static bool HasIgnoredSegment(string path, string root, Func<string, bool> isIgnoredPath)
    {
        var current = path;
        while (current.Length > 0 && !IsSameDirectory(current, root))
        {
            if (isIgnoredPath(current)) return true;
            var index = current.LastIndexOf('/');
            if (index <= 0) break;
            current = current[..index];
        }
        return false;
    }

    public static string NormalizePath(string? path) => (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');

    public static bool IsSameDirectory(string? a, string? b) =>
        string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);

    public static bool IsUnderOrEqual(string? path, string? folder)
    {
        var candidate = NormalizePath(path);
        var parent = NormalizePath(folder);
        if (parent.Length == 0) return true;
        if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)) return true;
        return candidate.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Stops an offline or half-broken listing from wiping metadata, covers and translations the user already has.</summary>
public static class LibraryCacheMergePolicy
{
    public static bool CanReplaceWholeCache(ScanListingResult listing) => listing.IsComplete;

    /// <summary>
    /// Keeps cached works under folders the scan could not read: they may still exist, only their re-listing failed.
    /// </summary>
    public static List<T> Merge<T>(
        IEnumerable<T> scannedWorks,
        IEnumerable<T> cachedWorks,
        IReadOnlyList<string> failedPaths,
        Func<T, string> folderPathOf)
    {
        ArgumentNullException.ThrowIfNull(scannedWorks);
        ArgumentNullException.ThrowIfNull(cachedWorks);
        ArgumentNullException.ThrowIfNull(folderPathOf);

        var merged = new List<T>(scannedWorks);
        if (failedPaths == null || failedPaths.Count == 0) return merged;

        var known = new HashSet<string>(merged.Select(folderPathOf), StringComparer.OrdinalIgnoreCase);

        foreach (var cached in cachedWorks)
        {
            var path = folderPathOf(cached);
            if (string.IsNullOrEmpty(path) || known.Contains(path)) continue;

            // Already represented by a scanned work (e.g. cached under an older rule as its sub-folder); keeping it would duplicate.
            if (known.Any(k => ScanListing.IsUnderOrEqual(path, k) || ScanListing.IsUnderOrEqual(k, path))) continue;

            // Either way round: a work under an unreadable folder, or an unreadable folder inside the work
            // ("RJ123/RJ123" failing leaves RJ123 with no files this time).
            if (failedPaths.Any(failed => ScanListing.IsUnderOrEqual(path, failed) || ScanListing.IsUnderOrEqual(failed, path)))
            {
                merged.Add(cached);
                known.Add(path);
            }
        }

        return merged;
    }
}
