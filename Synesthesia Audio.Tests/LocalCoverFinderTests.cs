using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// DUP-04: cover discovery used "the first jpg/png the directory returned", which could pick a screenshot
/// instead of the cover. This is the ranking that replaces it.
/// </summary>
public sealed class LocalCoverFinderTests
{
    [Fact]
    public void A_conventional_cover_name_beats_a_larger_screenshot()
    {
        var best = LocalCoverFinder.SelectBest([
            ("/album/screenshot.png", 900_000),
            ("/album/cover.jpg", 40_000)
        ]);

        Assert.Equal("/album/cover.jpg", best);
    }

    [Theory]
    [InlineData("/a/folder.jpg")]
    [InlineData("/a/front.png")]
    [InlineData("/a/jacket.jpg")]
    [InlineData("/a/package.png")]
    [InlineData("/a/cover_alt.jpg")]
    [InlineData("/a/COVER.jpg")]
    public void The_conventional_names_are_all_recognised(string path)
    {
        var best = LocalCoverFinder.SelectBest([("/a/random.png", 5_000_000), (path, 1_000)]);

        Assert.Equal(path, best);
    }

    [Fact]
    public void With_no_preferred_name_the_largest_image_wins()
    {
        var best = LocalCoverFinder.SelectBest([
            ("/a/small.jpg", 10_000),
            ("/a/large.png", 500_000),
            ("/a/medium.jpg", 100_000)
        ]);

        Assert.Equal("/a/large.png", best);
    }

    [Theory]
    [InlineData("readme.txt")]
    [InlineData("audio.mp3")]
    [InlineData("notes.md")]
    public void A_non_image_is_never_chosen(string name)
    {
        Assert.Null(LocalCoverFinder.SelectBest([($"/a/{name}", 1_000)]));
    }

    [Fact]
    public void No_candidates_means_no_cover()
    {
        Assert.Null(LocalCoverFinder.SelectBest([]));
    }

    [Fact]
    public void Empty_paths_are_skipped()
    {
        Assert.Equal("/a/cover.jpg", LocalCoverFinder.SelectBest([("", 9), ("/a/cover.jpg", 1)]));
    }

    /// <summary>Same rank and size must not depend on enumeration order, or the cover could change between runs.</summary>
    [Fact]
    public void A_tie_is_broken_deterministically()
    {
        var forward = LocalCoverFinder.SelectBest([("/a/bbb.jpg", 5_000), ("/a/aaa.jpg", 5_000)]);
        var reverse = LocalCoverFinder.SelectBest([("/a/aaa.jpg", 5_000), ("/a/bbb.jpg", 5_000)]);

        Assert.Equal(forward, reverse);
        Assert.Equal("/a/aaa.jpg", forward);
    }

    [Fact]
    public void Preference_is_found_regardless_of_case_and_extension()
    {
        var best = LocalCoverFinder.SelectBest([("/a/z.jpg", 1), ("/a/FRONT.PNG", 1)]);

        Assert.Equal("/a/FRONT.PNG", best);
    }

    [Theory]
    [InlineData("a.jpg", true)]
    [InlineData("a.JPEG", true)]
    [InlineData("a.png", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.gif", false)]
    [InlineData("a", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Image_extensions_are_recognised(string? path, bool expected)
    {
        Assert.Equal(expected, LocalCoverFinder.IsImage(path));
    }
}
