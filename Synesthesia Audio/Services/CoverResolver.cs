using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Where a work's cover lives on disk and which local file is the best candidate. The ranking rule itself is
/// <see cref="LocalCoverFinder"/> in Core, where it can be tested.
/// </summary>
public static class CoverResolver
{
    /// <summary>
    /// Under AppData rather than Cache: the OS may clear the cache at any time, leaving a cached work with a dead
    /// cover path and no reason to fetch it again.
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
            return null;
        }
    }
}
