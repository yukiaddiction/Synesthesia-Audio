namespace Synesthesia_Audio.Core;

/// <summary>The process-wide <see cref="SingleFlightGate"/> instance the app shares.</summary>
public static class SingleFlight
{
    private static readonly SingleFlightGate Gate = new();

    public static bool IsRunning => Gate.IsRunning;

    public static Task<bool> RunAsync(Func<Task> operation) => Gate.RunAsync(operation);
}
