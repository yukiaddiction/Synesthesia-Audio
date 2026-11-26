using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// This type exists because the Retry-After value used to be stored on the DelegatingHandler instance, which the
/// HTTP factory forced to be transient — so the value was written to one instance and read from another. The rules
/// here are what make one shared instance correct, and a regression in them is exactly the kind of bug that only
/// shows up as "rate limiting mysteriously does not work".
/// </summary>
public sealed class RetryAfterStateTests
{
    [Fact]
    public void A_fresh_state_has_no_value()
    {
        Assert.Null(new RetryAfterState().Last);
    }

    [Fact]
    public void A_captured_value_is_readable()
    {
        var state = new RetryAfterState();

        state.Record(TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(30), state.Last);
    }

    /// <summary>
    /// The important rule: a rate-limited response without a Retry-After header must not blank out a value an
    /// earlier one supplied, or the backoff silently becomes "retry immediately".
    /// </summary>
    [Fact]
    public void A_response_without_a_value_does_not_erase_the_last_one()
    {
        var state = new RetryAfterState();
        state.Record(TimeSpan.FromSeconds(45));

        state.Record(null);

        Assert.Equal(TimeSpan.FromSeconds(45), state.Last);
    }

    [Fact]
    public void A_later_value_replaces_the_earlier_one()
    {
        var state = new RetryAfterState();
        state.Record(TimeSpan.FromSeconds(10));

        state.Record(TimeSpan.FromSeconds(70));

        Assert.Equal(TimeSpan.FromSeconds(70), state.Last);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_zero_or_negative_value_is_not_a_cooldown(int seconds)
    {
        var state = new RetryAfterState();
        state.Record(TimeSpan.FromSeconds(20));

        state.Record(TimeSpan.FromSeconds(seconds));

        Assert.Equal(TimeSpan.FromSeconds(20), state.Last);
    }

    [Fact]
    public void Reset_clears_the_value()
    {
        var state = new RetryAfterState();
        state.Record(TimeSpan.FromSeconds(30));

        state.Reset();

        Assert.Null(state.Last);
    }

    /// <summary>Concurrent writers must not corrupt or lose the value (translation calls run in parallel).</summary>
    [Fact]
    public async Task Concurrent_records_leave_a_readable_value()
    {
        var state = new RetryAfterState();

        await Task.WhenAll(Enumerable.Range(0, 32).Select(i =>
            Task.Run(() => state.Record(TimeSpan.FromSeconds(i + 1)))));

        Assert.NotNull(state.Last);
        Assert.True(state.Last!.Value > TimeSpan.Zero);
    }
}
