namespace Synesthesia_Audio.Core;

/// <summary>
/// Turns the raw tail of a log file into the last lines of it.
///
/// The log viewer used to read every log file in full and split the result, so opening the viewer read megabytes
/// to display the last few hundred lines (PERF-11). Reading only the end is now the caller's job; deciding which
/// lines those are is this, kept pure so the edge cases can be tested.
/// </summary>
public static class LogTail
{
    /// <summary>
    /// The last <paramref name="maxLines"/> lines of <paramref name="text"/>.
    /// </summary>
    /// <param name="dropPartialFirstLine">
    /// True when <paramref name="text"/> starts mid-file (the caller seeked rather than reading from the
    /// beginning). The first line is then very likely truncated and is dropped, because showing half a timestamp
    /// is worse than showing one line fewer.
    /// </param>
    public static IReadOnlyList<string> LastLines(string? text, int maxLines, bool dropPartialFirstLine = false)
    {
        if (string.IsNullOrEmpty(text) || maxLines <= 0) return Array.Empty<string>();

        var lines = text.Replace("\r\n", "\n").Split('\n');

        var start = 0;
        if (dropPartialFirstLine && lines.Length > 1) start = 1;

        // A trailing newline produces one empty final element, which is not a line.
        var end = lines.Length;
        while (end > start && lines[end - 1].Length == 0) end--;

        var count = end - start;
        if (count <= 0) return Array.Empty<string>();

        if (count > maxLines) start = end - maxLines;

        var result = new string[end - start];
        Array.Copy(lines, start, result, 0, result.Length);
        return result;
    }
}
