using System.Text.RegularExpressions;

namespace Synesthesia_Audio.Core;

/// <summary>
/// Decides which folder is "the work" for a track. Disc folders (<c>CD1</c>, <c>Disc 2</c>) and format folders
/// (<c>mp3</c>, <c>wav</c>) resolve to the work above them, so a two-disc album is one entry, not two halves.
/// </summary>
public static class WorkFolderResolver
{
    // "RJ" plus 6-8 digits anywhere in the name, e.g. "RJ01234567"; case-insensitive.
    public static readonly Regex ProductIdPattern = new(@"RJ\d{6,8}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Whole name is a part keyword, optionally followed by spaces/_/- and a number: "CD1", "Disc 2", "vol_3", "bonus".
    private static readonly Regex DiscFolderPattern = new(
        @"^(disc|cd|vol|volume|disk|bonus|track|side|part|effect|se)([\s_\-]*\d+)?$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Whole name is exactly an audio format: the same work in another container.
    private static readonly Regex FormatFolderPattern = new(
        @"^(mp3|wav|flac|ogg|opus|m4a|mp4|aac)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsDiscFolder(string? folderName) =>
        !string.IsNullOrWhiteSpace(folderName) && DiscFolderPattern.IsMatch(folderName.Trim());

    public static bool IsFormatFolder(string? folderName) =>
        !string.IsNullOrWhiteSpace(folderName) && FormatFolderPattern.IsMatch(folderName.Trim());

    public static bool IsPartFolder(string? folderName) => IsDiscFolder(folderName) || IsFormatFolder(folderName);

    public static bool ContainsProductId(string? folderName) =>
        !string.IsNullOrEmpty(folderName) && ProductIdPattern.IsMatch(folderName);

    /// <summary>Cloud paths use <c>/</c>; local paths keep the caller's separator.</summary>
    public static string Resolve(string filePath, string basePath, bool isCloud) =>
        ResolveAll(new[] { filePath }, basePath, isCloud)[filePath];

    // Whole name is a sorting label, never a work: SFW/NSFW, R18/R-18, R15, "All Ages"/"all_age", adult, 全年齢, 成人向(け).
    private static readonly Regex CategoryFolderPattern = new(
        @"^(n?sfw|r-?18|r-?15|all[\s_\-]*ages?|adult|全年齢|成人向け?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsCategoryFolder(string? folderName) =>
        !string.IsNullOrWhiteSpace(folderName) && CategoryFolderPattern.IsMatch(folderName.Trim());

    /// <summary>
    /// Resolves all files at once because which folder is a work depends on its neighbours. The outermost folder with a
    /// product id is the work, and every folder above one is a category. A work without an id is the first folder below
    /// the deepest category, so "SFW/Other/Some Work/本編/01.mp3" is work "Some Work" with section "本編".
    /// </summary>
    public static Dictionary<string, string> ResolveAll(IEnumerable<string> filePaths, string basePath, bool isCloud)
    {
        var separator = isCloud ? '/' : Path.DirectorySeparatorChar;
        var files = filePaths.Distinct(StringComparer.Ordinal).ToList();
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        // Pass 1 resolves product-id works and records the folders above them as categories.
        var containers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plain = new List<(string File, string Directory, string Base)>();
        var sharedParents = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var fileNormalized = Normalize(file);
            var directory = GetDirectory(fileNormalized);
            var normalizedBase = AlignBase(Normalize(basePath), fileNormalized);

            if (string.IsNullOrEmpty(directory))
            {
                result[file] = basePath;
                continue;
            }

            var productFolder = OutermostProductFolder(directory, normalizedBase);
            if (productFolder != null)
            {
                if (!sharedParents.TryGetValue(productFolder, out var shared))
                {
                    shared = SharedParentOfFormatCopies(productFolder, normalizedBase, files);
                    sharedParents[productFolder] = shared;
                }
                productFolder = shared ?? productFolder;
                result[file] = ToNative(productFolder, separator);
                for (var up = GetDirectory(productFolder); IsStrictlyUnder(up, normalizedBase); up = GetDirectory(up))
                {
                    if (!containers.Add(up)) break;
                }
                continue;
            }

            plain.Add((file, directory, normalizedBase));
        }

        // Pass 2 resolves the rest, now that the categories are known.
        foreach (var (file, directory, normalizedBase) in plain)
        {
            if (IsSame(directory, normalizedBase) || !IsUnder(directory, normalizedBase))
            {
                result[file] = ToNative(directory, separator);
                continue;
            }

            var chain = new List<string>();
            for (var current = directory; IsStrictlyUnder(current, normalizedBase); current = GetDirectory(current))
            {
                chain.Add(current);
                if (IsSame(GetDirectory(current), current)) break;
            }
            chain.Reverse();

            var work = chain.FirstOrDefault(folder => !containers.Contains(folder) && !IsCategoryFolder(GetLeaf(folder)));
            result[file] = ToNative(work ?? directory, separator);
        }

        return result;
    }

    /// <summary>
    /// "Title/RJ123456_mp3" beside "Title/RJ123456_wav" would each be a one-folder album; when a parent (not the
    /// library, not a category) holds two or more folders with the same code, the parent is the work.
    /// </summary>
    private static string? SharedParentOfFormatCopies(string productFolder, string normalizedBase, IReadOnlyList<string> files)
    {
        var parent = GetDirectory(productFolder);
        if (!IsStrictlyUnder(parent, normalizedBase) || IsCategoryFolder(GetLeaf(parent))) return null;
        if (ContainsProductId(GetLeaf(parent))) return null;

        var code = ProductIdPattern.Match(GetLeaf(productFolder)).Value;
        var prefix = parent + "/";
        var siblings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var normalized = Normalize(file);
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var rest = normalized[prefix.Length..];
            var slash = rest.IndexOf('/');
            if (slash < 0) continue;

            var child = rest[..slash];
            if (ProductIdPattern.Match(child).Value.Equals(code, StringComparison.OrdinalIgnoreCase)) siblings.Add(child);
            if (siblings.Count >= 2) return parent;
        }

        return null;
    }

    private static string? OutermostProductFolder(string directory, string normalizedBase)
    {
        string? outermost = null;
        for (var current = directory; IsStrictlyUnder(current, normalizedBase); current = GetDirectory(current))
        {
            if (ContainsProductId(GetLeaf(current))) outermost = current;
            if (IsSame(GetDirectory(current), current)) break;
        }
        return outermost;
    }

    private static bool IsStrictlyUnder(string? path, string normalizedBase) =>
        !string.IsNullOrEmpty(path) && path != "/" && !IsSame(path, normalizedBase) && IsUnder(path, normalizedBase);

    /// <summary>
    /// The folder picker stores "/Voice Work" while WebDAV answers "Voice Work/…"; unaligned, nothing appeared inside
    /// the library and every leaf folder became its own work.
    /// </summary>
    private static string AlignBase(string normalizedBase, string normalizedFile)
    {
        if (normalizedBase.Length == 0 || normalizedBase == "/") return normalizedFile.StartsWith('/') ? "/" : string.Empty;
        if (normalizedBase.StartsWith('/') && !normalizedFile.StartsWith('/')) return normalizedBase.TrimStart('/');
        if (!normalizedBase.StartsWith('/') && normalizedFile.StartsWith('/') && !IsDriveRooted(normalizedBase))
            return "/" + normalizedBase;
        return normalizedBase;
    }

    private static bool IsDriveRooted(string path) => path.Length >= 2 && path[1] == ':';
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
