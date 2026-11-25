namespace Synesthesia_Audio.Core;

/// <summary>
/// Runs at most one operation at a time, dropping any call that arrives while one is in flight.
///
/// This is for "the second attempt is the same attempt" situations — a double tap that fires two handlers,
/// a double click on a button that pushes a page. Queueing would be wrong there: the queued call would show
/// the page that is already on its way in.
/// </summary>
public static class SingleFlight
{
    private static int _inFlight;

    /// <summary>True while an operation is running.</summary>
    public static bool IsRunning => Volatile.Read(ref _inFlight) != 0;

    /// <summary>
    /// Runs <paramref name="operation"/> unless one is already running. Returns true when it ran. A throw
    /// from the operation propagates after the gate is released, so a failed attempt cannot wedge it shut.
    /// </summary>
    public static async Task<bool> RunAsync(Func<Task> operation)
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
