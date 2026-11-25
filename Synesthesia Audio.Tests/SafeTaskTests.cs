using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// PAT-08: a fire-and-forget call loses its exception entirely. The helper must observe it and hand it to a
/// reporter, only once, and must never let the reporter's own failure escape.
/// </summary>
public sealed class SafeTaskTests
{
    [Fact]
    public async Task A_faulted_task_reports_its_exception()
    {
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        await Task.Yield();
        Task.Run(() => throw new InvalidOperationException("boom"))
            .FireAndForget(ex => reported.TrySetResult(ex));

        var exception = await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsType<InvalidOperationException>(exception);
        Assert.Equal("boom", exception.Message);
    }

    /// <summary>A task that faulted before the call must still be reported, not silently dropped.</summary>
    [Fact]
    public void An_already_faulted_task_reports_synchronously()
    {
        Exception? captured = null;

        // Genuinely already complete: a ContinueWith would never run for it, so this is the case that needs the
        // synchronous branch.
        Task.FromException(new InvalidOperationException("already failed"))
            .FireAndForget(ex => captured = ex);

        Assert.IsType<InvalidOperationException>(captured);
    }

    [Fact]
    public async Task A_successful_task_reports_nothing()
    {
        var reporterCalled = false;

        Task.CompletedTask.FireAndForget(_ => reporterCalled = true);
        Task.Run(() => { }).FireAndForget(_ => reporterCalled = true);

        await Task.Delay(100);

        Assert.False(reporterCalled);
    }

    [Fact]
    public void A_null_task_is_a_no_op()
    {
        Task? nothing = null;

        // Must not throw.
        nothing.FireAndForget(_ => throw new InvalidOperationException("should not be called"));
    }

    [Fact]
    public async Task A_cancelled_task_is_not_reported_as_a_failure()
    {
        var reporterCalled = false;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Task.FromCanceled(cts.Token).FireAndForget(_ => reporterCalled = true);

        await Task.Delay(100);

        Assert.False(reporterCalled);
    }

    /// <summary>The reporter is diagnostic code; if it throws, the faulted operation must not be worsened.</summary>
    [Fact]
    public async Task A_throwing_reporter_does_not_escape()
    {
        var reported = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task.Run(() =>
        {
            try
            {
                Task.Run(() => throw new InvalidOperationException("boom"))
                    .FireAndForget(_ => throw new ApplicationException("reporter failed"));

                reported.TrySetResult(true);
            }
            catch
            {
                reported.TrySetResult(false);
            }
        });

        var didNotThrow = await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);

        Assert.True(didNotThrow);
    }

    [Fact]
    public void An_aggregate_failure_is_unwrapped_so_the_cause_is_visible()
    {
        Exception? captured = null;

        Task.WhenAll(
                Task.FromException(new InvalidOperationException("first")),
                Task.FromException(new ArgumentException("second")))
            .FireAndForget(ex => captured = ex);

        // Two distinct causes stay aggregated rather than being reduced to one.
        Assert.IsType<AggregateException>(captured);
        Assert.Equal(2, ((AggregateException)captured!).InnerExceptions.Count);
    }
}
