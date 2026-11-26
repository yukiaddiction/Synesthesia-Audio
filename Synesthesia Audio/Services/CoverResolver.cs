using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Where a work's cover lives on disk and which local file is the best candidate.
///
/// These two decisions used to sit inside <see cref="ScanFolderService"/>, which meant the scan pipeline and the
/// cover rules were the same 1000-line class; the audit asks for a CoverResolver for exactly that reason (PAT-09).
/// The ranking itself is <see cref="LocalCoverFinder"/> in Core, which is where it can be tested.
/// </summary>
public static class CoverResolver
{
    /// <summary>
    /// The cached cover path for a work, keyed by product id when there is one.
    ///
    /// Covers live under AppData rather than Cache: the OS may clear the cache directory at any time, and a cleared
    /// cover left the cached work with an empty path and no way to fetch it again.
    /// </summary>
    public static string GetCachePath(string workPath, string? productId)
    {
        var coversDirectory = Path.Combine(FileSystem.AppDataDirectory, "covers");
        Directory.CreateDirectory(coversDirectory);

        if (!string.IsNullOrEmpty(productId))
        {
            return Path.Combine(coversDirectory, $"{productId}_cover.jpg");
        }

        var cleanPath = string.Concat(workPath.Split(Path.GetInvalidFileNameChars()));
        if (cleanPath.Length > 50) cleanPath = cleanPath.Substring(cleanPath.Length - 50);
        return Path.Combine(coversDirectory, $"cover_{cleanPath}.jpg");
    }

    /// <summary>Best cover image in a local album folder, ranked by <see cref="LocalCoverFinder"/>.</summary>
    public static string? ChooseBestLocal(string folderPath)
    {
        try
        {
            var candidates = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                .Select(f =>
                {
                    long size = 0;
                    try { size = new FileInfo(f).Length; } catch { /* size is only a tie-break */ }
                    return (Path: f, Length: size);
                });

            return LocalCoverFinder.SelectBest(candidates);
        }
        catch
        {
            // An unreadable folder simply has no local cover.
            return null;
        }
    }
}
