using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class RateLimitCooldownTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InactiveByDefault()
    {
        var cooldown = new RateLimitCooldown();

        Assert.False(cooldown.IsActive(Now));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(Now));
    }

    [Fact]
    public void Trip_MakesItActiveUntilExpiry()
    {
        var cooldown = new RateLimitCooldown();

        cooldown.Trip(Now, TimeSpan.FromSeconds(30));

        Assert.True(cooldown.IsActive(Now));
        Assert.True(cooldown.IsActive(Now.AddSeconds(29)));
        Assert.False(cooldown.IsActive(Now.AddSeconds(30)));
    }

    [Fact]
    public void Remaining_CountsDownAndNeverGoesNegative()
    {
        var cooldown = new RateLimitCooldown();
        cooldown.Trip(Now, TimeSpan.FromSeconds(30));

        Assert.Equal(TimeSpan.FromSeconds(20), cooldown.Remaining(Now.AddSeconds(10)));
        Assert.Equal(TimeSpan.Zero, cooldown.Remaining(Now.AddSeconds(45)));
    }

    [Fact]
    public void Trip_ExtendsButNeverShortens()
    {
        var cooldown = new RateLimitCooldown();
        cooldown.Trip(Now, TimeSpan.FromSeconds(60));

        cooldown.Trip(Now, TimeSpan.FromSeconds(5));

        Assert.Equal(Now.AddSeconds(60), cooldown.Until);
    }

    [Fact]
    public void Trip_IgnoresNonPositiveDuration()
    {
        var cooldown = new RateLimitCooldown();

        cooldown.Trip(Now, TimeSpan.Zero);

        Assert.False(cooldown.IsActive(Now));
    }

    [Fact]
    public void Clear_ResetsState()
    {
        var cooldown = new RateLimitCooldown();
        cooldown.Trip(Now, TimeSpan.FromSeconds(30));

        cooldown.Clear();

        Assert.Null(cooldown.Until);
        Assert.False(cooldown.IsActive(Now));
    }
}
