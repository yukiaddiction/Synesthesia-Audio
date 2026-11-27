using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Logging;

/// <summary>
/// Logger source for classes the framework constructs (Android services, activity helpers, players), which cannot
/// take one by injection. Before <see cref="Initialize"/> runs it hands out null loggers, so early logging is silent.
/// </summary>
public static class AppLog
{
    private static ILoggerFactory? _factory;

    public static void Initialize(ILoggerFactory factory) => _factory = factory;

    public static ILogger<T> Get<T>() where T : class =>
        _factory?.CreateLogger<T>() ?? NullLogger<T>.Instance;
}

public class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}

public class NullLogger<T> : ILogger<T>
{
    public static readonly NullLogger<T> Instance = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}