namespace Synesthesia_Audio.Core;

/// <summary>
/// Picks the last lines out of a log tail the caller read by seeking, so the viewer need not read whole files.
/// </summary>
public static class LogTail
{
    /// <param name="dropPartialFirstLine">
    /// Set when the caller seeked mid-file: the first line is likely truncated, and half a timestamp is worse
    /// than one line fewer.
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
