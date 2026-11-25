using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Logging;

/// <summary>
/// Writes the app log to one file.
///
/// Every category gets its own <see cref="FileLogger"/>, but they all append to the same <c>app.log</c>.
/// Each instance used to own its own <see cref="SemaphoreSlim"/>, so the lock only serialised writes within a
/// single category: two categories writing at once interleaved into the same file, and rotation could race an
/// append. Writes now go through <see cref="FileLogWriter"/>, which is one queue and one consumer for the whole
/// process (BUG-m-22).
/// </summary>
public class FileLogger : ILogger, IDisposable
{
    private readonly string _categoryName;

    public FileLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var entry = FormatLogEntry(logLevel, message, exception);

        // Enqueue, never write inline: logging from the UI thread must not touch the disk, and a slow write
        // must not stall the caller. The queue is bounded, so a log storm drops old lines instead of growing.
        FileLogWriter.Enqueue(entry);
    }

    private string FormatLogEntry(LogLevel logLevel, string message, Exception? exception)
    {
        var sb = new StringBuilder();
        sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.Append(" [");
        sb.Append(logLevel.ToString().ToUpperInvariant().PadRight(5));
        sb.Append("] [");
        sb.Append(_categoryName);
        sb.Append("] ");
        sb.Append(message);

        if (exception != null)
        {
            // Full ToString(): message, inner exception chain and stack trace. Logging only the
            // message made a recurring crash (14 occurrences of "MauiContext is null") impossible
            // to locate from the app log, because the throw site was never recorded.
            sb.Append(" | Exception: ");
            sb.Append(exception);
        }

        return sb.ToString();
    }

    public static void PurgeOldLogs() => FileLogWriter.PurgeOldLogs(FileLogWriter.RetentionDays);

    public void Dispose()
    {
        // The writer is process-wide and lives for the lifetime of the app; there is nothing per-instance
        // left to release.
        GC.SuppressFinalize(this);
    }
}
