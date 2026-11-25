using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-15: a double tap fires two handlers, and both pushed a page. The single-flight gate drops the second
/// so only one page is ever pushed.
///
/// These tests use a fresh <see cref="SingleFlightGate"/> each time rather than the process-wide
/// <see cref="SingleFlight"/> facade: xUnit runs test classes in parallel, so sharing the static instance made
/// one test's in-flight operation drop another test's call — which is how this file was intermittently red.
/// </summary>
public sealed class SingleFlightTests
{
    [Fact]
    public async Task One_operation_runs()
    {
        var gate = new SingleFlightGate();

        Assert.True(await gate.RunAsync(() => Task.CompletedTask));
    }

    /// <summary>The exact double-tap shape: the second call arrives while the first is mid-flight.</summary>
    [Fact]
    public async Task A_second_call_while_one_is_running_is_dropped()
    {
        var gate = new SingleFlightGate();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = gate.RunAsync(() => release.Task);

        var secondRan = await gate.RunAsync(() => Task.CompletedTask);

        release.SetResult(true);
        Assert.True(await first);

        Assert.False(secondRan, "a push that arrives mid-flight must be dropped, not queued");
    }

    [Fact]
    public async Task The_gate_reopens_after_success()
    {
        var gate = new SingleFlightGate();

        Assert.True(await gate.RunAsync(() => Task.CompletedTask));
        Assert.False(gate.IsRunning);
        Assert.True(await gate.RunAsync(() => Task.CompletedTask));
    }

    /// <summary>A failed navigation must not wedge the gate shut for the rest of the session.</summary>
    [Fact]
    public async Task The_gate_reopens_after_a_failure()
    {
        var gate = new SingleFlightGate();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => gate.RunAsync(() => throw new InvalidOperationException("navigation failed")));

        Assert.False(gate.IsRunning);
        Assert.True(await gate.RunAsync(() => Task.CompletedTask));
    }

    [Fact]
    public async Task Two_concurrent_calls_run_exactly_one()
    {
        var gate = new SingleFlightGate();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var counter = 0;

        async Task Body()
        {
            Interlocked.Increment(ref counter);
            await release.Task;
        }

        var a = gate.RunAsync(Body);
        var b = gate.RunAsync(Body);

        release.SetResult(true);
        await Task.WhenAll(a, b);

        Assert.Equal(1, counter);
    }

    [Fact]
    public async Task IsRunning_reflects_an_operation_in_flight()
    {
        var gate = new SingleFlightGate();
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var running = gate.RunAsync(() => release.Task);
        Assert.True(gate.IsRunning);

        release.SetResult(true);
        await running;

        Assert.False(gate.IsRunning);
    }

    /// <summary>The facade the app actually calls must behave the same way.</summary>
    [Fact]
    public async Task The_process_wide_facade_still_runs_an_operation()
    {
        Assert.True(await SingleFlight.RunAsync(() => Task.CompletedTask));
    }
}
