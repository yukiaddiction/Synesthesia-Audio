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

        var files = GetSortedLogFiles(logDir);
        var lines = new StringBuilder();

        foreach (var file in files)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                lines.Append(content);
            }
            catch (IOException)
            {
            }
        }

        var allText = lines.ToString();
        var allSplit = allText.Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.None);

        if (allSplit.Length <= lineCount)
            return allText.TrimEnd();

        var tail = new StringBuilder();
        for (int i = Math.Max(0, allSplit.Length - lineCount); i < allSplit.Length; i++)
        {
            tail.AppendLine(allSplit[i]);
        }

        return tail.ToString().TrimEnd();
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
            try { totalLines += File.ReadAllLines(f).Length; } catch { }
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
