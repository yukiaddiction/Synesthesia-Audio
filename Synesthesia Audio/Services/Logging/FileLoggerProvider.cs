using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Logging;

public class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name));
    }

    public void Dispose()
    {
        foreach (var logger in _loggers.Values)
        {
            // Teardown must not throw, and with no working logger left, debug output is the only destination.
            try { logger.Dispose(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"FileLoggerProvider: disposing a logger failed: {ex.Message}"); }
        }
        _loggers.Clear();
    }
}
