namespace Synesthesia_Audio.Core;

/// <summary>
/// <c>_ = SomethingAsync();</c> loses its exception, so a player could silently stop advancing its queue. This
/// observes the fault and reports it instead.
/// </summary>
public static class SafeTask
{
    /// <summary>A throwing reporter never escapes.</summary>
    public static void FireAndForget(this Task? task, Action<Exception>? onError = null)
    {
        if (task is null) return;

        if (task.IsCompleted)
        {
            // No continuation would ever run on a finished task, so report now.
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
            // A failure reporter must never take down the operation it only describes.
        }
    }
}
