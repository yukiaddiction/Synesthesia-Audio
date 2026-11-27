namespace Synesthesia_Audio.Core;

/// <summary>One entry returned while walking a library folder.</summary>
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

    /// <summary>
    /// The server's change tag for this entry (WebDAV <c>getetag</c>, or <c>getlastmodified</c> when there is none).
    /// Null when the source has no such thing (local disk).
    /// </summary>
    public string? ETag { get; }
}

/// <summary>
/// Lists one folder. Implemented once for the local disk and once for WebDAV so the walk itself — and
/// its failure bookkeeping — exists in exactly one place and can be tested without touching either.
/// </summary>
public interface IScanFileSource
{
    Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>A source that can also list a whole tree in one request (WebDAV <c>Depth: infinity</c>).</summary>
public interface ITreeScanFileSource : IScanFileSource
{
    /// <summary>
    /// Every entry beneath <paramref name="rootPath"/>, or null when the server will not answer such a request.
    /// A null answer makes the walk fall back to listing folder by folder; it is not an error.
    /// </summary>
    Task<IReadOnlyList<ScanEntry>?> TryListTreeAsync(string rootPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a walk found, and — just as important — what it could not read. A caller must never treat an
/// incomplete walk as an empty library.
/// </summary>
public sealed class ScanListingResult
{
    public List<string> AudioFiles { get; } = new();

    /// <summary>Folders that could not be listed (offline share, permission denied, server error).</summary>
    public List<string> FailedPaths { get; } = new();

    /// <summary>True when the root folder itself could not be listed, so nothing is known about the library.</summary>
    public bool RootFailed { get; set; }

    public bool IsComplete => !RootFailed && FailedPaths.Count == 0;

    /// <summary>Change tag of every folder seen, keyed by its normalised path.</summary>
    public Dictionary<string, string> DirectoryETags { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the whole tree came back from one request.</summary>
    public bool UsedTreeListing { get; set; }

    /// <summary>Folders whose contents were taken from the previous scan because their change tag had not moved.</summary>
    public int ReusedFolders { get; set; }

    /// <summary>Listing requests made, for the log.</summary>
    public int ListRequests { get; set; }
}

public static class ScanListing
{
    /// <summary>How many times one folder is asked for before it is recorded as unreadable.</summary>
    public const int ListAttempts = 3;

    /// <summary>
    /// Walks <paramref name="rootPath"/> breadth-first, collecting audio files and remembering every
    /// folder that failed. A failing subtree is recorded and skipped; it never aborts the whole walk
    /// (unless it is the root itself, which is reported through <see cref="ScanListingResult.RootFailed"/>).
    ///
    /// Folders are listed <paramref name="maxParallelism"/> at a time. A WebDAV library spent the whole walk
    /// waiting on one round trip per folder, which is where most of a cloud scan's time went (PERF-02); the
    /// cap keeps the fan-out polite to the server. Local disk listing is effectively unaffected.
    ///
    /// When the source can list a whole tree in one request (<see cref="ITreeScanFileSource"/>) that is tried
    /// first, and a refusal falls back to the walk. <paramref name="reuseSubtree"/> lets the caller skip a folder
    /// whose contents it already knows: when it returns a file list for a directory entry, those files are used and
    /// the folder is not listed.
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

            // Take a batch of folders and list them together. `pending` is only touched by this thread.
            var batch = new List<string>(maxParallelism);
            while (batch.Count < maxParallelism && pending.Count > 0) batch.Add(pending.Dequeue());

            result.ListRequests += batch.Count;
            var listings = await Task.WhenAll(batch.Select(ListOneAsync)).ConfigureAwait(false);

            foreach (var listing in listings)
            {
                if (listing.Error != null)
                {
                    // Record it and keep walking: one unreadable folder must not decide the whole scan.
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

            // Report as folders are walked so a slow (WebDAV) library still shows movement.
            onAudioFileCount?.Invoke(result.AudioFiles.Count);
        }

        return result;

        // A WebDAV server under load drops the odd connection mid-answer (pCloud: "response ended prematurely").
        // Retrying that folder twice, a moment later, keeps one flaky answer from leaving a work out of the library.
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

                if (delay > TimeSpan.Zero) await Task.Delay(delay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Takes a whole-tree listing. The ignore rule looks at names, so every folder between the root and an entry is
    /// checked: a file inside "__MACOSX/x" is skipped even though its own name is fine.
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

    /// <summary>Forward-slash, no trailing separator — the form comparisons below expect.</summary>
    public static string NormalizePath(string? path) => (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');

    public static bool IsSameDirectory(string? a, string? b) =>
        string.Equals(NormalizePath(a), NormalizePath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="path"/> is the folder itself or lives beneath it.</summary>
    public static bool IsUnderOrEqual(string? path, string? folder)
    {
        var candidate = NormalizePath(path);
        var parent = NormalizePath(folder);
        if (parent.Length == 0) return true;
        if (string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)) return true;
        return candidate.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Decides what may be written back to the library cache after a scan. This is what stops an offline
/// or half-broken listing from wiping metadata, covers and translations the user already has.
/// </summary>
public static class LibraryCacheMergePolicy
{
    /// <summary>
    /// Replacing the whole cache is only safe when the walk saw everything. If the root failed, nothing
    /// is known and the cache must be left alone.
    /// </summary>
    public static bool CanReplaceWholeCache(ScanListingResult listing) => listing.IsComplete;

    /// <summary>
    /// Builds the list to persist: everything the scan found, plus cached works that live under a folder
    /// the scan could not read, since those may still exist on disk and only their re-listing failed.
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

            // A work the scan found already stands for this folder (an entry cached under an older rule, such as a
            // work's own sub-folder): keeping it would show the same work twice.
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
