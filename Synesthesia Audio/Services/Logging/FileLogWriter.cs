using System.Threading.Channels;

namespace Synesthesia_Audio.Services.Logging;

/// <summary>
/// The single writer for the app log.
///
/// One bounded queue and one consumer for the whole process, so every category appends to the same file in
/// order and rotation can never race an append. Logging from the UI thread only enqueues; the disk work
/// happens on the consumer's thread.
/// </summary>
internal static class FileLogWriter
{
    public const int RetentionDays = 7;

    private const long MaxFileSizeBytes = 1 * 1024 * 1024;
    private const int MaxBackupFiles = 3;

    /// <summary>
    /// Bounded on purpose: a runaway log source must not turn into unbounded memory growth. Old entries are
    /// dropped rather than the caller being blocked.
    /// </summary>
    private static readonly Channel<string> Channel = System.Threading.Channels.Channel.CreateBounded<string>(
        new BoundedChannelOptions(4096)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    private static readonly Lazy<bool> ConsumerStarted = new(StartConsumer, isThreadSafe: true);

    private static bool StartConsumer()
    {
        _ = Task.Run(ConsumeAsync);
        return true;
    }

    public static void Enqueue(string line)
    {
        _ = ConsumerStarted.Value;
        Channel.Writer.TryWrite(line);
    }

    private static async Task ConsumeAsync()
    {
        var reader = Channel.Reader;
        var batch = new List<string>(64);

        try
        {
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                // Batch whatever is already queued so a burst costs one file open instead of one per line.
                while (batch.Count < 256 && reader.TryRead(out var line))
                {
                    batch.Add(line);
                }

                if (batch.Count == 0) continue;

                await FlushAsync(batch).ConfigureAwait(false);
                batch.Clear();
            }
        }
        catch (Exception ex)
        {
            // The logger must never take the app down, and there is nowhere left to report to.
            System.Diagnostics.Debug.WriteLine($"FileLogWriter consumer stopped: {ex.Message}");
        }
    }

    private static async Task FlushAsync(List<string> lines)
    {
        try
        {
            var logDirectory = Path.Combine(FileSystem.AppDataDirectory, "logs");
            Directory.CreateDirectory(logDirectory);

            var logPath = Path.Combine(logDirectory, "app.log");
            RotateIfNeeded(logDirectory, logPath);

            await File.AppendAllTextAsync(logPath, string.Join(Environment.NewLine, lines) + Environment.NewLine)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileLogWriter write failed: {ex.Message}");
        }
    }

    private static void RotateIfNeeded(string logDirectory, string logPath)
    {
        try
        {
            if (!File.Exists(logPath)) return;
            if (new FileInfo(logPath).Length < MaxFileSizeBytes) return;

            for (var i = MaxBackupFiles - 1; i >= 1; i--)
            {
                var oldFile = Path.Combine(logDirectory, $"app.{i}.log");
                var newFile = Path.Combine(logDirectory, $"app.{i + 1}.log");
                if (!File.Exists(oldFile)) continue;

                if (File.Exists(newFile)) File.Delete(newFile);
                File.Move(oldFile, newFile);
            }

            var backupPath = Path.Combine(logDirectory, "app.1.log");
            if (File.Exists(backupPath)) File.Delete(backupPath);
            File.Move(logPath, backupPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileLogWriter rotate failed: {ex.Message}");
        }
    }

    public static void PurgeOldLogs(int retentionDays)
    {
        try
        {
            var logDirectory = Path.Combine(FileSystem.AppDataDirectory, "logs");
            if (!Directory.Exists(logDirectory)) return;

            var cutoff = DateTime.Now.AddDays(-retentionDays);
            foreach (var file in Directory.GetFiles(logDirectory, "app*.log"))
            {
                var info = new FileInfo(file);
                if (info.LastWriteTime < cutoff) info.Delete();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileLogWriter purge failed: {ex.Message}");
        }
    }
}
