namespace Synesthesia_Audio.Core;

/// <summary>
/// Decides which files in the covers folder may be deleted.
///
/// Cover files were written and then never removed, so the folder grew with every re-scan and every cover that
/// changed (PERF-12). Deleting files on a schedule is the kind of thing that must be provably safe, so the
/// selection is a pure function here and is tested: a file is only deletable when nothing references it, it is
/// old enough that a scan or the UI cannot be about to use it, and it is actually an image.
/// </summary>
public static class CoverCleanupPolicy
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    /// <summary>True for a file name that looks like cover artwork rather than, say, a settings file.</summary>
    public static bool IsCoverFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var extension = Path.GetExtension(fileName);
        return ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the candidate file names that are safe to delete.
    /// </summary>
    /// <param name="candidateFileNames">File names (not paths) present in the covers folder.</param>
    /// <param name="referencedFileNames">File names still referenced by a cached work.</param>
    /// <param name="lastWriteUtc">When each candidate was last written; a missing entry is treated as unknown and kept.</param>
    /// <param name="nowUtc">The current time, passed in so the rule is testable.</param>
    /// <param name="minAge">
    /// How old a file must be. This is the guard against deleting a cover a scan has just written but not yet
    /// attached to a work.
    /// </param>
    public static IReadOnlyList<string> SelectDeletable(
        IEnumerable<string> candidateFileNames,
        IEnumerable<string> referencedFileNames,
        Func<string, DateTimeOffset?> lastWriteUtc,
        DateTimeOffset nowUtc,
        TimeSpan minAge)
    {
        ArgumentNullException.ThrowIfNull(candidateFileNames);
        ArgumentNullException.ThrowIfNull(referencedFileNames);
        ArgumentNullException.ThrowIfNull(lastWriteUtc);

        var referenced = new HashSet<string>(
            referencedFileNames.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => Path.GetFileName(n)),
            StringComparer.OrdinalIgnoreCase);

        var deletable = new List<string>();

        foreach (var candidate in candidateFileNames)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;

            var name = Path.GetFileName(candidate);

            if (referenced.Contains(name)) continue;
            if (!IsCoverFileName(name)) continue;

            // Unknown timestamp means we cannot prove it is old, so it stays.
            if (lastWriteUtc(candidate) is not { } written) continue;

            if (nowUtc - written < minAge) continue;

            deletable.Add(candidate);
        }

        return deletable;
    }
}
