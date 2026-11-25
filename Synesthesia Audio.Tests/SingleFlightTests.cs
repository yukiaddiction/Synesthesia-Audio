using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-15: a double tap fires two handlers, and both pushed a page. The single-flight gate drops the second
/// so only one page is ever pushed.
/// </summary>
public sealed class SingleFlightTests
{
    /// <summary>The gate is process-wide, so each test must start from a known state.</summary>
    private static async Task WaitUntilIdleAsync()
    {
        for (var i = 0; i < 100 && SingleFlight.IsRunning; i++)
        {
            await Task.Delay(10);
        }

        Assert.False(SingleFlight.IsRunning, "the gate should be idle before the test");
    }

    [Fact]
    public async Task One_operation_runs()
    {
        await WaitUntilIdleAsync();

        var ran = await SingleFlight.RunAsync(() => Task.CompletedTask);

        Assert.True(ran);
    }

    /// <summary>The exact double-tap shape: the second call arrives while the first is mid-flight.</summary>
    [Fact]
    public async Task A_second_call_while_one_is_running_is_dropped()
    {
        await WaitUntilIdleAsync();

        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = SingleFlight.RunAsync(() => release.Task);

        // The first call has claimed the gate.
        var secondRan = await SingleFlight.RunAsync(() => Task.CompletedTask);

        release.SetResult(true);
        Assert.True(await first);

        Assert.False(secondRan, "a push that arrives mid-flight must be dropped, not queued");
    }

    [Fact]
    public async Task The_gate_reopens_after_success()
    {
        await WaitUntilIdleAsync();

        Assert.True(await SingleFlight.RunAsync(() => Task.CompletedTask));
        Assert.False(SingleFlight.IsRunning);
        Assert.True(await SingleFlight.RunAsync(() => Task.CompletedTask));
    }

    /// <summary>A failed navigation must not wedge the gate shut for the rest of the session.</summary>
    [Fact]
    public async Task The_gate_reopens_after_a_failure()
    {
        await WaitUntilIdleAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SingleFlight.RunAsync(() => throw new InvalidOperationException("navigation failed")));

        Assert.False(SingleFlight.IsRunning);
        Assert.True(await SingleFlight.RunAsync(() => Task.CompletedTask));
    }

    [Fact]
    public async Task Two_concurrent_calls_run_exactly_one()
    {
        await WaitUntilIdleAsync();

        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = 0;

        async Task Body()
        {
            Interlocked.Increment(ref counter);
            await release.Task;
        }

        var a = SingleFlight.RunAsync(Body);
        var b = SingleFlight.RunAsync(Body);

        release.SetResult(true);
        await Task.WhenAll(a, b);

        Assert.Equal(1, counter);
    }
}
