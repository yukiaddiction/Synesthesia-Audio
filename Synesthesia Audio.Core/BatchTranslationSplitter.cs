namespace Synesthesia_Audio.Core;

/// <summary>
/// Google/Bing are called with all the strings of a batch joined by newlines, and the reply is split back
/// into one translation per input. That only works if the reply keeps exactly one non-empty line per input,
/// which it does not always do: the service reflows, merges or drops lines, and an input that itself contains
/// a newline corrupts the count before the request is even made.
///
/// Both halves of that contract live here so they are testable, and so a caller always either gets a
/// correctly aligned list or is told to fall back — never a silently misaligned one, which would put one
/// work's tag on another work.
/// </summary>
public static class BatchTranslationSplitter
{
    /// <summary>Flattens line breaks inside one input so it cannot break the per-item line contract.</summary>
    public static string SanitizeInput(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    /// <summary>
    /// Splits a joined translation into one entry per input, or returns <c>null</c> when the reply cannot be
    /// aligned with certainty. Callers must fall back to per-item requests on <c>null</c>.
    /// </summary>
    public static IReadOnlyList<string>? TrySplit(string? translated, int expectedCount)
    {
        if (expectedCount <= 0) return Array.Empty<string>();
        if (string.IsNullOrEmpty(translated)) return null;

        var lines = translated
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        // The count must match exactly. Splitting with RemoveEmptyEntries would quietly accept a reply that
        // lost or merged a line, and then every translation after that point belongs to the wrong input.
        if (lines.Length != expectedCount) return null;

        var result = new string[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) return null;
            result[i] = line;
        }

        return result;
    }
}
