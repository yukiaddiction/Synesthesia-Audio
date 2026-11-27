using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>The limiter owns both the cooldown clock and the backoff counter so the two cannot drift apart.</summary>
public sealed class RateLimiterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(60);

    private static RateLimiter NewLimiter() => new(Base, Max);

    [Fact]
    public void A_fresh_limiter_blocks_nothing()
    {
        var limiter = NewLimiter();

        Assert.False(limiter.IsBlocked(Now));
        Assert.Equal(TimeSpan.Zero, limiter.Remaining(Now));
        Assert.Equal(0, limiter.ConsecutiveLimits);
    }

    [Fact]
    public void The_first_limit_uses_the_base_delay()
    {
        var limiter = NewLimiter();

        var delay = limiter.RecordRateLimit(Now, null);

        Assert.Equal(Base, delay);
        Assert.True(limiter.IsBlocked(Now));
        Assert.Equal(Base, limiter.Remaining(Now));
    }

    [Fact]
    public void Consecutive_limits_double_the_delay()
    {
        var limiter = NewLimiter();

        Assert.Equal(Base, limiter.RecordRateLimit(Now, null));
        Assert.Equal(Base * 2, limiter.RecordRateLimit(Now, null));
        Assert.Equal(Base * 4, limiter.RecordRateLimit(Now, null));
        Assert.Equal(3, limiter.ConsecutiveLimits);
    }

    [Fact]
    public void The_delay_is_capped()
    {
        var limiter = NewLimiter();

        TimeSpan delay = TimeSpan.Zero;
        for (var i = 0; i < 20; i++) delay = limiter.RecordRateLimit(Now, null);

        Assert.Equal(Max, delay);
    }

    [Fact]
    public void A_server_retry_after_wins_over_the_backoff()
    {
        var limiter = NewLimiter();

        var delay = limiter.RecordRateLimit(Now, TimeSpan.FromSeconds(42));

        Assert.Equal(TimeSpan.FromSeconds(42), delay);
        Assert.Equal(TimeSpan.FromSeconds(42), limiter.Remaining(Now));
    }

    [Fact]
    public void A_server_retry_after_is_still_capped()
    {
        var limiter = NewLimiter();

        var delay = limiter.RecordRateLimit(Now, TimeSpan.FromHours(1));

        Assert.Equal(Max, delay);
    }

    [Fact]
    public void A_success_resets_the_count()
    {
        var limiter = NewLimiter();
        limiter.RecordRateLimit(Now, null);
        limiter.RecordRateLimit(Now, null);

        limiter.RecordSuccess();

        Assert.Equal(0, limiter.ConsecutiveLimits);
        Assert.Equal(Base, limiter.RecordRateLimit(Now, null));
    }

    /// <summary>A cooldown already granted is paid for, so a shorter later one must not cut it short.</summary>
    [Fact]
    public void A_shorter_later_limit_does_not_shorten_the_cooldown()
    {
        var limiter = NewLimiter();
        limiter.RecordRateLimit(Now, TimeSpan.FromSeconds(30));

        // A second request hits a limit with no Retry-After, which would compute a much shorter delay.
        var delay = limiter.RecordRateLimit(Now, null);

        Assert.True(delay < TimeSpan.FromSeconds(30));
        Assert.Equal(TimeSpan.FromSeconds(30), limiter.Remaining(Now));
    }

    [Fact]
    public void The_cooldown_expires()
    {
        var limiter = NewLimiter();
        limiter.RecordRateLimit(Now, null);

        Assert.False(limiter.IsBlocked(Now + Base));
        Assert.Equal(TimeSpan.Zero, limiter.Remaining(Now + Base));
    }

    [Fact]
    public void Construction_rejects_impossible_settings()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(TimeSpan.Zero, Max));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)));
    }

    /// <summary>Concurrent limits must not all compute the same step from a torn counter.</summary>
    [Fact]
    public async Task Concurrent_limits_advance_the_step_once_each()
    {
        var limiter = NewLimiter();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => limiter.RecordRateLimit(Now, null))));

        Assert.Equal(8, limiter.ConsecutiveLimits);
    }
}
