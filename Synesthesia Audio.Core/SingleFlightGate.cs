namespace Synesthesia_Audio.Core;

/// <summary>
/// Runs at most one operation at a time, dropping any call that arrives while one is in flight.
///
/// This is for "the second attempt is the same attempt" situations — a double tap that fires two handlers,
/// a double click on a button that pushes a page. Queueing would be wrong there: the queued call would show
/// the page that is already on its way in.
///
/// Instantiable rather than static so the behaviour can be tested without one test's in-flight operation
/// leaking into another's; <see cref="SingleFlight"/> provides the process-wide instance the app uses.
/// </summary>
public sealed class SingleFlightGate
{
    private int _inFlight;

    /// <summary>True while an operation is running.</summary>
    public bool IsRunning => Volatile.Read(ref _inFlight) != 0;

    /// <summary>
    /// Runs <paramref name="operation"/> unless one is already running. Returns true when it ran. A throw
    /// from the operation propagates after the gate is released, so a failed attempt cannot wedge it shut.
    /// </summary>
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
