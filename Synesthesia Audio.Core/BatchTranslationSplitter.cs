namespace Synesthesia_Audio.Core;

/// <summary>
/// Batched Google/Bing translation joins inputs with newlines and splits the reply. The service may reflow, merge
/// or drop lines, so callers get either a correctly aligned list or a signal to fall back, never a misaligned one.
/// </summary>
public static class BatchTranslationSplitter
{
    /// <summary>An embedded newline would break the one-line-per-input contract.</summary>
    public static string SanitizeInput(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text
            .Replace("\r\n", " ", StringComparison.Ordinal)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    /// <summary>Returns null when the reply cannot be aligned with certainty; callers then fall back to per-item requests.</summary>
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
