using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services.Logging;

/// <summary>
/// A logger for the classes the framework constructs, which cannot take one by constructor injection: Android
/// services, activity helpers, custom players.
///
/// It holds an explicitly initialised factory rather than reaching into a service locator (PAT-01). Until
/// <see cref="Initialize"/> runs, every call returns a null logger, so logging before startup is silent rather
/// than throwing.
/// </summary>
public static class AppLog
{
    private static ILoggerFactory? _factory;

    /// <summary>Called once at startup with the app's logger factory.</summary>
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