namespace Synesthesia_Audio.Core;

/// <summary>One folder of a work: its title and the files that sit directly in it.</summary>
public sealed record AlbumSection(string Title, IReadOnlyList<string> Files)
{
    /// <summary>True for the section that holds files sitting directly in the work folder.</summary>
    public bool IsMain { get; init; }
}

/// <summary>
/// Splits a work's audio files into sections, one per folder.
///
/// The rule the user asked for: a work can contain many folders, each folder is shown as its own section, and
/// files sitting directly in the work folder form a "Main folder" section. Sections are keyed by the folder's path
/// relative to the work, so nesting depth does not matter and two folders at the same level stay separate - the
/// previous rule keyed off a single leaf name and special-cased some names away, which is how folders went missing.
/// </summary>
public static class SectionBuilder
{
    public const string MainSectionTitle = "Main folder";

    /// <summary>
    /// Sections for <paramref name="files"/>, ordered main-first then by path. <paramref name="isCloud"/> selects
    /// which separator to compare on; both are normalised internally.
    /// </summary>
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
            .OrderBy(g => g.Key.Length == 0 ? 0 : 1)              // the main folder first
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)  // then folders alphabetically
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

    /// <summary>The folder path below the work folder, or empty when the file sits in the work folder itself.</summary>
    private static string RelativeTo(string directory, string work)
    {
        if (string.IsNullOrEmpty(work)) return string.Empty;
        if (string.Equals(directory, work, StringComparison.OrdinalIgnoreCase)) return string.Empty;

        var prefix = work + "/";
        if (directory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return directory[prefix.Length..];

        // Not under the work at all: keep the last segment so the caller still gets a usable section name.
        var index = directory.LastIndexOf('/');
        return index < 0 ? directory : directory[(index + 1)..];
    }
}
