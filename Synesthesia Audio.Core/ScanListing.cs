namespace Synesthesia_Audio.Core;

/// <summary>One entry returned while walking a library folder.</summary>
public sealed class ScanEntry
{
    public ScanEntry(string path, bool isDirectory)
    {
        Path = path;
        IsDirectory = isDirectory;
    }

    public string Path { get; }
    public bool IsDirectory { get; }
}

/// <summary>
/// Lists one folder. Implemented once for the local disk and once for WebDAV so the walk itself — and
/// its failure bookkeeping — exists in exactly one place and can be tested without touching either.
/// </summary>
public interface IScanFileSource
{
    Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default);
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
}

public static class ScanListing
{
    /// <summary>
    /// Walks <paramref name="rootPath"/> breadth-first, collecting audio files and remembering every
    /// folder that failed. A failing subtree is recorded and skipped; it never aborts the whole walk
    /// (unless it is the root itself, which is reported through <see cref="ScanListingResult.RootFailed"/>).
    /// </summary>
    public static async Task<ScanListingResult> CollectAudioFilesAsync(
        IScanFileSource source,
        string rootPath,
        Func<string, bool> isAudioFile,
        Func<string, bool>? isIgnoredPath = null,
        Action<int>? onAudioFileCount = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(isAudioFile);

        var result = new ScanListingResult();
        var pending = new Queue<string>();
        pending.Enqueue(rootPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directory = pending.Dequeue();
            IReadOnlyList<ScanEntry> entries;

            try
            {
                entries = await source.ListAsync(directory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Record it and keep walking: one unreadable folder must not decide the whole scan.
                result.FailedPaths.Add(directory);
                if (IsSameDirectory(directory, rootPath)) result.RootFailed = true;
                continue;
            }

            foreach (var entry in entries)
            {
                if (isIgnoredPath != null && isIgnoredPath(entry.Path)) continue;

                if (entry.IsDirectory)
                {
                    pending.Enqueue(entry.Path);
                }
                else if (isAudioFile(entry.Path))
                {
                    result.AudioFiles.Add(entry.Path);
                }
            }

            // Report as folders are walked so a slow (WebDAV) library still shows movement.
            onAudioFileCount?.Invoke(result.AudioFiles.Count);
        }

        return result;
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

            if (failedPaths.Any(failed => ScanListing.IsUnderOrEqual(path, failed)))
            {
                merged.Add(cached);
                known.Add(path);
            }
        }

        return merged;
    }
}
