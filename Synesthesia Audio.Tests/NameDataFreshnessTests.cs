using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-01: the app re-downloaded the name table on every launch because it compared the published date
/// against the bundled one, which only changes when a new build ships. The comparison must be against the
/// revision already applied.
/// </summary>
public sealed class NameDataFreshnessTests
{
    [Fact]
    public void A_different_published_revision_is_new()
    {
        Assert.True(NameDataFreshness.IsNewRevision("2026-11-24", "2026-11-17"));
    }

    [Fact]
    public void The_revision_already_applied_is_not_new()
    {
        Assert.False(NameDataFreshness.IsNewRevision("2026-11-24", "2026-11-24"));
    }

    /// <summary>First run, or the user cleared app data: nothing has been applied yet.</summary>
    [Fact]
    public void Nothing_applied_yet_means_take_the_published_revision()
    {
        Assert.True(NameDataFreshness.IsNewRevision("2026-11-24", null));
        Assert.True(NameDataFreshness.IsNewRevision("2026-11-24", ""));
        Assert.True(NameDataFreshness.IsNewRevision("2026-11-24", "   "));
    }

    [Fact]
    public void Nothing_published_means_nothing_to_fetch()
    {
        Assert.False(NameDataFreshness.IsNewRevision(null, "2026-11-17"));
        Assert.False(NameDataFreshness.IsNewRevision("", null));
        Assert.False(NameDataFreshness.IsNewRevision("  ", "  "));
    }

    [Fact]
    public void Surrounding_whitespace_does_not_count_as_a_new_revision()
    {
        Assert.False(NameDataFreshness.IsNewRevision(" 2026-11-24 ", "2026-11-24"));
    }

    /// <summary>
    /// The regression this finding is about: after applying a revision, a further launch must not fetch it
    /// again — with the old comparison the bundled date differed, so this returned true forever.
    /// </summary>
    [Fact]
    public void A_second_launch_after_applying_does_not_refetch()
    {
        const string published = "2026-11-24";
        var applied = published; // written to the sidecar when the download was applied

        Assert.False(NameDataFreshness.IsNewRevision(published, applied));
    }
}
