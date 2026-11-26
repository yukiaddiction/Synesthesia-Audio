using System.Text;

namespace Synesthesia_Audio.Services.Logging;

public class LogService
{
    private const int DefaultTailLines = 500;

    public string GetLogFilePath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "logs", "app.log");
    }

    public async Task<string> ReadTailAsync(int lineCount = DefaultTailLines)
    {
        var logDir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        if (!Directory.Exists(logDir))
            return "No logs recorded yet.";

        // Read the end of the newest logs only. This used to read every file in full and split the result, so
        // opening the viewer read megabytes to show a few hundred lines (PERF-11).
        var files = GetSortedLogFiles(logDir).Reverse().ToList();
        var collected = new List<string>(lineCount);

        foreach (var file in files)
        {
            if (collected.Count >= lineCount) break;

            try
            {
                var chunk = await ReadTailChunkAsync(file, lineCount - collected.Count).ConfigureAwait(false);
                collected.InsertRange(0, chunk);
            }
            catch (IOException)
            {
                // A log the writer still has open: skip it rather than failing the whole view.
            }
        }

        return string.Join(Environment.NewLine, collected).TrimEnd();
    }

    /// <summary>How much of the end of a log file to read before trimming to whole lines.</summary>
    private const int TailChunkBytes = 256 * 1024;

    private static async Task<IReadOnlyList<string>> ReadTailChunkAsync(string path, int maxLines)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var length = stream.Length;
        var bytesToRead = (int)Math.Min(length, TailChunkBytes);
        var startedMidFile = bytesToRead < length;

        if (bytesToRead <= 0) return Array.Empty<string>();

        stream.Seek(length - bytesToRead, SeekOrigin.Begin);

        var buffer = new byte[bytesToRead];
        var read = await stream.ReadAsync(buffer).ConfigureAwait(false);

        var text = System.Text.Encoding.UTF8.GetString(buffer, 0, read);
        return Synesthesia_Audio.Core.LogTail.LastLines(text, maxLines, dropPartialFirstLine: startedMidFile);
    }

    public async Task<string> ReadAllLogsAsync()
    {
        var logDir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        if (!Directory.Exists(logDir))
            return "No logs recorded yet.";

        var sb = new StringBuilder();
        var files = GetSortedLogFiles(logDir);

        foreach (var file in files)
        {
            try
            {
                if (sb.Length > 0) sb.AppendLine();
                sb.AppendLine($"--- {Path.GetFileName(file)} ({new FileInfo(file).LastWriteTime:yyyy-MM-dd HH:mm}) ---");
                sb.Append(await File.ReadAllTextAsync(file));
            }
            catch (IOException)
            {
            }
        }

        return sb.ToString();
    }

    public async Task CopyLogsToClipboardAsync()
    {
        var content = await ReadAllLogsAsync();
        await Clipboard.Default.SetTextAsync(content);
    }

    public async Task OpenLogFileAsync()
    {
        var path = GetLogFilePath();
        if (File.Exists(path))
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest("Application Logs", new ReadOnlyFile(path)));
        }
    }

    public string GetLogSummary()
    {
        var logDir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        if (!Directory.Exists(logDir)) return "No logs";

        var files = Directory.GetFiles(logDir, "app*.log");
        long totalBytes = 0;
        int totalLines = 0;
        foreach (var f in files)
        {
            totalBytes += new FileInfo(f).Length;
                try { totalLines += File.ReadAllLines(f).Length; } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LogService: could not count lines in {f}: {ex.Message}"); }
        }

        var sizeText = totalBytes < 1024 * 1024
            ? $"{totalBytes / 1024} KB"
            : $"{totalBytes / (1024 * 1024.0):F1} MB";

        return $"{files.Length} file(s) · {sizeText} · {totalLines:N0} lines · {RetentionText()} retention";
    }

    private static string[] GetSortedLogFiles(string logDir)
    {
        var files = Directory.GetFiles(logDir, "app*.log");
        Array.Sort(files, (a, b) =>
        {
            var aNum = ExtractNumber(Path.GetFileNameWithoutExtension(a));
            var bNum = ExtractNumber(Path.GetFileNameWithoutExtension(b));
            return aNum.CompareTo(bNum);
        });
        return files;
    }

    private static int ExtractNumber(string name)
    {
        if (name == "app") return 0;
        var numPart = name.Replace("app.", "");
        return int.TryParse(numPart, out var n) ? n : 999;
    }

    private static string RetentionText()
    {
        var logDir = Path.Combine(FileSystem.AppDataDirectory, "logs");
        if (!Directory.Exists(logDir)) return "7d";

        var oldest = DateTime.MaxValue;
        foreach (var f in Directory.GetFiles(logDir, "app*.log"))
        {
            var t = new FileInfo(f).LastWriteTime;
            if (t < oldest) oldest = t;
        }

        if (oldest == DateTime.MaxValue) return "7d";
        var age = (DateTime.Now - oldest).Days;
        return age == 0 ? "today" : $"{age}d";
    }
}
