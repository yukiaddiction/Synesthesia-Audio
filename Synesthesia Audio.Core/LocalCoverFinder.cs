namespace Synesthesia_Audio.Core;

/// <summary>
/// Directory order is arbitrary, so "first image found" could pick a track-list screenshot. This prefers
/// conventional cover names, then the largest image.
/// </summary>
public static class LocalCoverFinder
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    private static readonly string[] PreferredStems = ["cover", "folder", "front", "jacket", "package"];

    /// <param name="candidates">Unknown size is 0.</param>
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

    public static bool IsImage(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>Prefix match, so "cover_alt.jpg" counts as a cover name too.</summary>
    private static int Rank(string path)
    {
        var stem = Path.GetFileNameWithoutExtension(path);

        return PreferredStems.Any(prefix => stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ? 0 : 1;
    }
}
