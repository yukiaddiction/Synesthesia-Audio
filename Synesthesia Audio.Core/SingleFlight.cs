namespace Synesthesia_Audio.Core;

/// <summary>
/// The process-wide gate for operations that must not overlap. See <see cref="SingleFlightGate"/> for the
/// behaviour; this type only supplies the one instance the app shares.
/// </summary>
public static class SingleFlight
{
    private static readonly SingleFlightGate Gate = new();

    /// <summary>True while an operation is running.</summary>
    public static bool IsRunning => Gate.IsRunning;

    /// <summary>Runs <paramref name="operation"/> unless one is already running.</summary>
    public static Task<bool> RunAsync(Func<Task> operation) => Gate.RunAsync(operation);
}
