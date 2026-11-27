using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Logging;

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

        // Never write inline: logging from the UI thread must not touch the disk.
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
            // Full ToString() so the stack trace is kept; the message alone never shows where it was thrown.
            sb.Append(" | Exception: ");
            sb.Append(exception);
        }

        return sb.ToString();
    }

    public static void PurgeOldLogs() => FileLogWriter.PurgeOldLogs(FileLogWriter.RetentionDays);

    // The writer is process-wide, so there is nothing per-instance to release.
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
