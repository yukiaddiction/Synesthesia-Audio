namespace Synesthesia_Audio.Core;

public sealed record AlbumSection(string Title, IReadOnlyList<string> Files)
{
    public bool IsMain { get; init; }
}

/// <summary>
/// One section per folder, keyed by path relative to the work, so nesting depth does not matter and same-named
/// folders at different places stay separate. Files directly in the work folder form the "Main folder" section.
/// </summary>
public static class SectionBuilder
{
    public const string MainSectionTitle = "Main folder";

    public static IReadOnlyList<AlbumSection> Build(IEnumerable<string> files, string workFolder)
    {
        var work = Normalize(workFolder);
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            if (string.IsNullOrEmpty(file)) continue;

            var directory = GetDirectory(Normalize(file));
            var relative = RelativeTo(directory, work);
            var key = relative.Length == 0 ? string.Empty : relative;

            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<string>();
                groups[key] = list;
                titles[key] = key.Length == 0 ? MainSectionTitle : key;
            }

            list.Add(file);
        }

        return groups
            .OrderBy(g => g.Key.Length == 0 ? 0 : 1)
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new AlbumSection(titles[g.Key], g.Value) { IsMain = g.Key.Length == 0 })
            .ToList();
    }

    private static string Normalize(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');

    private static string GetDirectory(string normalizedPath)
    {
        var index = normalizedPath.LastIndexOf('/');
        return index < 0 ? string.Empty : normalizedPath[..index];
    }

    private static string RelativeTo(string directory, string work)
    {
        if (string.IsNullOrEmpty(work)) return string.Empty;
        if (string.Equals(directory, work, StringComparison.OrdinalIgnoreCase)) return string.Empty;

        var prefix = work + "/";
        if (directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return directory[prefix.Length..];

        // Not under the work at all: keep the last segment so the section still has a usable name.
        var index = directory.LastIndexOf('/');
        return index < 0 ? directory : directory[(index + 1)..];
    }
}
