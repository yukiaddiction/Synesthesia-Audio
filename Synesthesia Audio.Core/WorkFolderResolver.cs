using System.Text.RegularExpressions;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Decides which folder is "the work" for a track.
///
/// A work is not always one flat folder: it may be split across disc folders (<c>CD1</c>, <c>Disc 2</c>,
/// <c>Vol3</c>) or across format folders (<c>mp3</c>, <c>wav</c>, <c>flac</c>). Those sub-folders belong to
/// the work above them, so they must resolve to that parent instead of becoming works of their own — which
/// is what made a two-disc album show up as two entries with half its tracks each.
/// </summary>
public static class WorkFolderResolver
{
    /// <summary>A DLsite product id, anywhere in a folder name.</summary>
    public static readonly Regex ProductIdPattern = new(@"RJ\d{6,8}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Disc, volume and side folders that hold one part of a single work. Every alternative the old
    /// per-branch regexes recognised is kept here, plus <c>disk</c>, and an optional separator and number.
    /// </summary>
    private static readonly Regex DiscFolderPattern = new(
        @"^(disc|cd|vol|volume|disk|bonus|track|side|part|effect|se)([\s_\-]*\d+)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Folders named after the audio format whose contents are the same work in another container.</summary>
    private static readonly Regex FormatFolderPattern = new(
        @"^(mp3|wav|flac|ogg|opus|m4a|mp4|aac)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsDiscFolder(string? folderName) =>
        !string.IsNullOrWhiteSpace(folderName) && DiscFolderPattern.IsMatch(folderName.Trim());

    public static bool IsFormatFolder(string? folderName) =>
        !string.IsNullOrWhiteSpace(folderName) && FormatFolderPattern.IsMatch(folderName.Trim());

    /// <summary>True when the folder is a sub-part of the work rather than the work itself.</summary>
    public static bool IsPartFolder(string? folderName) => IsDiscFolder(folderName) || IsFormatFolder(folderName);

    public static bool ContainsProductId(string? folderName) =>
        !string.IsNullOrEmpty(folderName) && ProductIdPattern.IsMatch(folderName);

    /// <summary>
    /// Resolves the work folder for <paramref name="filePath"/>. Cloud paths use <c>/</c>; local paths keep
    /// the separator the caller used. A trailing separator on <paramref name="basePath"/> is tolerated.
    /// </summary>
    public static string Resolve(string filePath, string basePath, bool isCloud)
    {
        var separator = isCloud ? '/' : Path.DirectorySeparatorChar;

        var normalizedBase = Normalize(basePath);
        var fileNormalized = Normalize(filePath);
        var directory = GetDirectory(fileNormalized);

        if (string.IsNullOrEmpty(directory)) return basePath;

        // Prefer the deepest folder carrying a product id, so "WorkA RJ123456/disc1" still lands on the work.
        var current = directory;
        while (!string.IsNullOrEmpty(current))
        {
            if (IsSame(current, normalizedBase)) break;
            if (ContainsProductId(GetLeaf(current))) return ToNative(current, separator);
            if (!IsUnder(current, normalizedBase)) break;
            current = GetDirectory(current);
        }

        if (IsSame(directory, normalizedBase)) return ToNative(normalizedBase, separator);

        var parent = GetDirectory(directory);
        if (string.IsNullOrEmpty(parent) || IsSame(parent, normalizedBase))
        {
            return ToNative(directory, separator);
        }

        if (IsPartFolder(GetLeaf(directory)))
        {
            return ToNative(parent, separator);
        }

        // Anything deeper than one folder under the library root belongs to that first folder: the library is
        // base/<work>/<sub-folder>/<file>, so a sub-folder with any other name (a bonus disc, "Scans", "おまけ",
        // a per-track directory) is a part of the work, not a work of its own. Without this a work whose
        // sub-folders are not named CD1/mp3/split into several albums, each showing one folder's tracks.
        var firstLevel = FirstLevelUnder(directory, normalizedBase);
        if (!string.IsNullOrEmpty(firstLevel)) return ToNative(firstLevel, separator);

        return ToNative(directory, separator);
    }

    /// <summary>
    /// The folder immediately below <paramref name="baseFolder"/> on the way to <paramref name="directory"/>, or null
    /// when the directory is already at the first level (or not under the base at all).
    /// </summary>
    private static string? FirstLevelUnder(string directory, string baseFolder)
    {
        if (string.IsNullOrEmpty(baseFolder)) return null;

        var candidate = directory;
        string? result = null;

        while (true)
        {
            var parent = GetDirectory(candidate);
            if (string.IsNullOrEmpty(parent)) return result;

            if (IsSame(parent, baseFolder))
            {
                // candidate is a direct child of the base: that child is the work only if we descended at least once.
                return IsSame(candidate, directory) ? null : candidate;
            }

            if (!IsUnder(parent, baseFolder)) return result;

            result = candidate;
            candidate = parent;

            if (IsSame(candidate, baseFolder)) return result;
        }
    }
    private static string Normalize(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;
        var normalized = path.Replace('\\', '/');
        // Keep a lone "/" intact; otherwise drop trailing separators so prefix comparisons are exact.
        if (normalized.Length > 1) normalized = normalized.TrimEnd('/');
        return normalized;
    }

    private static string ToNative(string normalized, char separator) =>
        separator == '/' ? normalized : normalized.Replace('/', separator);

    private static bool IsSame(string? a, string? b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string? path, string? baseFolder)
    {
        if (string.IsNullOrEmpty(baseFolder)) return true;
        return ScanListing.IsUnderOrEqual(path, baseFolder);
    }

    private static string GetDirectory(string normalizedPath)
    {
        var index = normalizedPath.LastIndexOf('/');
        if (index < 0) return string.Empty;
        // Preserve a leading "/" for rooted cloud paths.
        return index == 0 ? "/" : normalizedPath[..index];
    }

    private static string GetLeaf(string normalizedPath)
    {
        var index = normalizedPath.LastIndexOf('/');
        return index < 0 ? normalizedPath : normalizedPath[(index + 1)..];
    }
}
