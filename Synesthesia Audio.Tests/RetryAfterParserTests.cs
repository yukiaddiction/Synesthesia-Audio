using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public sealed class RetryAfterParserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_delta_is_used_directly()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), RetryAfterParser.TryParse(TimeSpan.FromSeconds(30), null, Now));
    }

    [Fact]
    public void A_delta_wins_over_a_date()
    {
        var result = RetryAfterParser.TryParse(
            TimeSpan.FromSeconds(5), Now.AddMinutes(10), Now);

        Assert.Equal(TimeSpan.FromSeconds(5), result);
    }

    [Fact]
    public void A_future_date_becomes_the_wait_from_now()
    {
        var result = RetryAfterParser.TryParse(null, Now.AddSeconds(45), Now);

        Assert.Equal(TimeSpan.FromSeconds(45), result);
    }

    [Fact]
    public void A_past_date_is_not_a_negative_wait()
    {
        Assert.Null(RetryAfterParser.TryParse(null, Now.AddSeconds(-30), Now));
    }

    [Fact]
    public void A_date_exactly_now_is_nothing_to_wait_for()
    {
        Assert.Null(RetryAfterParser.TryParse(null, Now, Now));
    }

    [Fact]
    public void A_zero_delta_is_nothing_to_wait_for()
    {
        Assert.Null(RetryAfterParser.TryParse(TimeSpan.Zero, null, Now));
    }

    [Fact]
    public void A_negative_delta_is_nothing_to_wait_for()
    {
        Assert.Null(RetryAfterParser.TryParse(TimeSpan.FromSeconds(-5), null, Now));
    }

    [Fact]
    public void No_header_at_all_means_no_wait()
    {
        Assert.Null(RetryAfterParser.TryParse(null, null, Now));
    }
}
