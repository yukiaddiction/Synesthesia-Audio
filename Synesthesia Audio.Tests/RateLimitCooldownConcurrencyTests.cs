using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-22: the shared "translation services are busy" clock is read and written by concurrent translation
/// calls. An unsynchronised read-modify-write in <c>Trip</c> could let a shorter expiry overwrite the longer
/// one another request had already paid for, so a user could be retried too early and rate-limited again.
/// </summary>
public sealed class RateLimitCooldownConcurrencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_newer_expiry_extends_the_cooldown()
    {
        var cooldown = new RateLimitCooldown();

        cooldown.Trip(Now, TimeSpan.FromSeconds(10));
        cooldown.Trip(Now, TimeSpan.FromSeconds(30));

        Assert.Equal(Now.AddSeconds(30), cooldown.Until);
    }

    [Fact]
    public void An_earlier_expiry_does_not_shorten_an_existing_cooldown()
    {
        var cooldown = new RateLimitCooldown();

        cooldown.Trip(Now, TimeSpan.FromSeconds(30));
        cooldown.Trip(Now, TimeSpan.FromSeconds(5));

        Assert.Equal(Now.AddSeconds(30), cooldown.Until);
    }

    [Fact]
    public void A_non_positive_duration_is_ignored()
    {
        var cooldown = new RateLimitCooldown();

        cooldown.Trip(Now, TimeSpan.Zero);
        cooldown.Trip(Now, TimeSpan.FromSeconds(-5));

        Assert.Null(cooldown.Until);
        Assert.False(cooldown.IsActive(Now));
    }

    [Fact]
    public void Active_and_remaining_reflect_the_clock()
    {
        var cooldown = new RateLimitCooldown();
        cooldown.Trip(Now, TimeSpan.FromSeconds(20));

        Assert.True(cooldown.IsActive(Now.AddSeconds(5)));
        Assert.Equal(TimeSpan.FromSeconds(15), cooldown.Remaining(Now.AddSeconds(5)));

        Assert.False(cooldown.IsActive(Now.AddSeconds(21)));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(Now.AddSeconds(21)));
    }

    [Fact]
    public void Clear_releases_the_cooldown()
    {
        var cooldown = new RateLimitCooldown();
        cooldown.Trip(Now, TimeSpan.FromSeconds(20));

        cooldown.Clear();

        Assert.Null(cooldown.Until);
        Assert.False(cooldown.IsActive(Now));
    }

    /// <summary>
    /// The concurrency case: many trips at once must leave the longest duration in place. A lost update would
    /// show up as a shorter expiry than the longest one requested.
    /// </summary>
    [Fact]
    public async Task Concurrent_trips_keep_the_longest_expiry()
    {
        var cooldown = new RateLimitCooldown();
        var longest = TimeSpan.FromSeconds(60);

        var trips = Enumerable.Range(1, 64)
            .Select(seconds => Task.Run(() => cooldown.Trip(Now, TimeSpan.FromSeconds(seconds % 60 + 1))))
            .ToList();

        cooldown.Trip(Now, longest);

        await Task.WhenAll(trips);

        Assert.Equal(Now.Add(longest), cooldown.Until);
    }

    [Fact]
    public async Task Concurrent_reads_and_writes_do_not_throw()
    {
        var cooldown = new RateLimitCooldown();

        var work = Enumerable.Range(0, 8).Select(workerIndex => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                cooldown.Trip(Now, TimeSpan.FromMilliseconds(i % 100 + 1));
                _ = cooldown.IsActive(Now);
                _ = cooldown.Remaining(Now);
                _ = cooldown.Until;
            }

            Assert.True(workerIndex >= 0);
        }));

        await Task.WhenAll(work);

        Assert.True(cooldown.IsActive(Now));
    }
}
