namespace Synesthesia_Audio.Core;

/// <summary>
/// Picks the image in an album folder that is most likely to be its cover.
///
/// Two places did this with "the first .jpg or .png the directory happened to return" (DUP-04). Directory order is
/// arbitrary, so that could just as easily pick a screenshot of the track list as the cover. This prefers the
/// names publishers actually use, then the largest image, and is a pure function so the ranking can be tested.
/// </summary>
public static class LocalCoverFinder
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    /// <summary>Stems that conventionally mean "this is the cover".</summary>
    private static readonly string[] PreferredStems = ["cover", "folder", "front", "jacket", "package"];

    /// <summary>
    /// The best cover among <paramref name="candidates"/>, or null when there is no usable image.
    /// </summary>
    /// <param name="candidates">File paths (or remote paths) with their size in bytes; unknown size is 0.</param>
    public static string? SelectBest(IEnumerable<(string Path, long Length)> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        string? best = null;
        var bestRank = int.MaxValue;
        long bestLength = -1;

        foreach (var (path, length) in candidates)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            if (!IsImage(path)) continue;

            var rank = Rank(path);

            // A preferred name always beats a non-preferred one; otherwise the larger file wins. Ties are broken
            // by ordinal path comparison so the choice is stable between runs and platforms.
            var better = rank < bestRank
                || (rank == bestRank && length > bestLength)
                || (rank == bestRank && length == bestLength && best != null && string.CompareOrdinal(path, best) < 0);

            if (best == null || better)
            {
                best = path;
                bestRank = rank;
                bestLength = length;
            }
        }

        return best;
    }

    /// <summary>True when the path has an extension this class will consider cover artwork.</summary>
    public static bool IsImage(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 0 for a conventional cover name, 1 for any other image. Checked against the start of the stem so
    /// "cover_alt.jpg" and "folder.jpg" both count.
    /// </summary>
    private static int Rank(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);

        return PreferredStems.Any(prefix => stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? 0 : 1;
    }
}
