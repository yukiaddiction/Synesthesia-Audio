using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class TranslationBackoffTests
{
    private static readonly TimeSpan Base = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Max = TimeSpan.FromSeconds(300);

    [Fact]
    public void ServerRetryAfterWins()
    {
        var delay = TranslationBackoff.Next(1, TimeSpan.FromSeconds(12), Base, Max);

        Assert.Equal(TimeSpan.FromSeconds(12), delay);
    }

    [Fact]
    public void ServerRetryAfterIsCapped()
    {
        var delay = TranslationBackoff.Next(1, TimeSpan.FromSeconds(9999), Base, Max);

        Assert.Equal(Max, delay);
    }

    [Fact]
    public void IgnoresNonPositiveServerValue()
    {
        var delay = TranslationBackoff.Next(1, TimeSpan.Zero, Base, Max);

        Assert.Equal(Base, delay);
    }

    [Fact]
    public void FirstRateLimitUsesBaseDelay()
    {
        Assert.Equal(Base, TranslationBackoff.Next(1, null, Base, Max));
    }

    [Fact]
    public void ConsecutiveRateLimitsDouble()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), TranslationBackoff.Next(2, null, Base, Max));
        Assert.Equal(TimeSpan.FromSeconds(120), TranslationBackoff.Next(3, null, Base, Max));
        Assert.Equal(TimeSpan.FromSeconds(240), TranslationBackoff.Next(4, null, Base, Max));
    }

    [Fact]
    public void DoublingIsCappedAndNeverOverflows()
    {
        Assert.Equal(Max, TranslationBackoff.Next(5, null, Base, Max));
        Assert.Equal(Max, TranslationBackoff.Next(1000, null, Base, Max));
    }
}
