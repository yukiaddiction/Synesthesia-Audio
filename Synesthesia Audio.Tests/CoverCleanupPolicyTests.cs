using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// PERF-12: cover files were never deleted. Because this policy decides what may be deleted from disk, every
/// reason it might be wrong gets a test.
/// </summary>
public sealed class CoverCleanupPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan MinAge = TimeSpan.FromHours(6);

    private static IReadOnlyList<string> Select(
        string[] candidates, string[] referenced, Dictionary<string, DateTimeOffset?> timestamps) =>
        CoverCleanupPolicy.SelectDeletable(
            candidates, referenced, name => timestamps.TryGetValue(name, out var t) ? t : null, Now, MinAge);

    [Fact]
    public void An_unreferenced_old_image_is_deletable()
    {
        var result = Select(
            ["old.jpg"], ["current.jpg"], new() { ["old.jpg"] = Now.AddDays(-3) });

        Assert.Equal(["old.jpg"], result);
    }

    [Fact]
    public void A_referenced_cover_is_never_deletable_however_old()
    {
        var result = Select(
            ["current.jpg"], ["current.jpg"], new() { ["current.jpg"] = Now.AddYears(-1) });

        Assert.Empty(result);
    }

    [Fact]
    public void Reference_matching_ignores_case()
    {
        var result = Select(
            ["Cover.JPG"], ["cover.jpg"], new() { ["Cover.JPG"] = Now.AddDays(-3) });

        Assert.Empty(result);
    }

    [Fact]
    public void A_reference_stored_as_a_full_path_protects_the_file()
    {
        var result = Select(
            ["cover.jpg"], [Path.Combine("/x/y", "cover.jpg")], new() { ["cover.jpg"] = Now.AddDays(-3) });

        Assert.Empty(result);
    }

    [Fact]
    public void A_recently_written_file_is_kept_even_when_unreferenced()
    {
        // The scan may have just written it and not yet attached it to a work.
        var result = Select(
            ["fresh.jpg"], [], new() { ["fresh.jpg"] = Now.AddMinutes(-5) });

        Assert.Empty(result);
    }

    [Fact]
    public void A_file_of_unknown_age_is_kept()
    {
        var result = Select(["mystery.jpg"], [], new());

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("settings.json")]
    [InlineData("data.tsv.gz")]
    [InlineData("noextension")]
    public void A_non_image_file_is_never_selected(string name)
    {
        var result = Select([name], [], new() { [name] = Now.AddDays(-30) });

        Assert.Empty(result);
    }

    [Fact]
    public void A_missing_timestamp_entry_does_not_delete_the_whole_list()
    {
        var result = Select(
            ["old.jpg", "unknown.jpg", "new.jpg"],
            [],
            new() { ["old.jpg"] = Now.AddDays(-2), ["new.jpg"] = Now.AddMinutes(-1) });

        Assert.Equal(["old.jpg"], result);
    }

    [Fact]
    public void Empty_inputs_are_handled()
    {
        Assert.Empty(Select([], [], new()));
    }
}
