namespace Synesthesia_Audio.Core;

/// <summary>
/// Starts a task that nobody awaits, but observes its failure instead of letting it vanish.
///
/// A fire-and-forget call written as <c>_ = SomethingAsync();</c> loses its exception entirely: the task faults,
/// nothing reads <c>Exception</c>, and the failure surfaces — if at all — as an unobserved-task event long after
/// the context that would have explained it is gone. That is how a player can silently stop advancing a queue,
/// or a session save can silently stop saving (PAT-08).
/// </summary>
public static class SafeTask
{
    /// <summary>
    /// Observes <paramref name="task"/>, reporting a fault to <paramref name="onError"/>. A null task is a no-op,
    /// an already-completed task reports immediately, and a throwing reporter never escapes.
    /// </summary>
    public static void FireAndForget(this Task? task, Action<Exception>? onError = null)
    {
        if (task is null) return;

        if (task.IsCompleted)
        {
            // Already finished: report now, because there is no continuation left to run.
            if (task.IsFaulted) Report(task.Exception, onError);
            return;
        }

        _ = task.ContinueWith(
            static (completed, state) => Report(completed.Exception, (Action<Exception>?)state),
            onError,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static void Report(AggregateException? exception, Action<Exception>? onError)
    {
        if (exception == null) return;

        var flattened = exception.Flatten();
        var single = flattened.InnerExceptions.Count == 1 ? flattened.InnerExceptions[0] : flattened;

        if (onError == null)
        {
            System.Diagnostics.Debug.WriteLine($"Unobserved background task failed: {single}");
            return;
        }

        try
        {
            onError(single);
        }
        catch
        {
            // A failure reporter must never take down the operation it was only supposed to describe.
        }
    }
}
