namespace Synesthesia_Audio.Core;

/// <summary>
/// Drops calls that arrive while one is in flight, for double taps and double clicks where queueing would replay
/// the same navigation. Instantiable so tests do not share in-flight state.
/// </summary>
public sealed class SingleFlightGate
{
    private int _inFlight;

    public bool IsRunning => Volatile.Read(ref _inFlight) != 0;

    /// <summary>The gate is released before an exception propagates, so a failed attempt cannot wedge it shut.</summary>
    public async Task<bool> RunAsync(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return false;

        try
        {
            await operation().ConfigureAwait(false);
            return true;
        }
        finally
        {
            Volatile.Write(ref _inFlight, 0);
        }
    }
}
