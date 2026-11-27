namespace Synesthesia_Audio.Core;

/// <summary>
/// Pure, tested selection of covers to delete: a file goes only when it is unreferenced, an image, and old enough
/// that a scan or the UI cannot be about to use it.
/// </summary>
public static class CoverCleanupPolicy
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"];

    public static bool IsCoverFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var extension = Path.GetExtension(fileName);
        return ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    /// <param name="minAge">Guards against deleting a cover a scan has just written but not yet attached to a work.</param>
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
