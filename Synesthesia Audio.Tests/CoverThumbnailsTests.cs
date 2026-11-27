using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>The thumbnail name is the only contract between the generator and every view that asks for one.</summary>
public sealed class CoverThumbnailsTests
{
    [Fact]
    public void A_thumbnail_keeps_the_stem_and_adds_the_edge()
    {
        var result = CoverThumbnails.For("/data/covers/abc.jpg", CoverThumbnails.CardEdge);

        // Compared by name and folder because Path.Combine rewrites separators per platform.
        Assert.Equal("abc.256.jpg", Path.GetFileName(result));
        Assert.Equal("covers", Path.GetFileName(Path.GetDirectoryName(result)));
    }

    [Fact]
    public void The_small_size_is_distinct_from_the_card_size()
    {
        var card = CoverThumbnails.For("/c/x.png", CoverThumbnails.CardEdge);
        var small = CoverThumbnails.For("/c/x.png", CoverThumbnails.SmallEdge);

        Assert.NotEqual(card, small);
        Assert.EndsWith("x.64.png", small);
    }

    [Fact]
    public void The_original_extension_is_preserved()
    {
        Assert.EndsWith(".webp", CoverThumbnails.For("/c/x.webp", 256));
    }

    [Fact]
    public void A_path_without_an_extension_defaults_to_jpg()
    {
        Assert.EndsWith(".256.jpg", CoverThumbnails.For("/c/x", 256));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_path_has_no_thumbnail(string? path)
    {
        Assert.Equal(string.Empty, CoverThumbnails.For(path, 256));
    }

    [Fact]
    public void A_bare_file_name_stays_bare()
    {
        Assert.Equal("abc.256.jpg", CoverThumbnails.For("abc.jpg", 256));
    }

    [Theory]
    [InlineData("abc.256.jpg", true)]
    [InlineData("abc.64.jpg", true)]
    [InlineData("abc.jpg", false)]
    [InlineData("abc.999.jpg", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Thumbnails_are_recognisable_by_name(string? name, bool expected)
    {
        Assert.Equal(expected, CoverThumbnails.IsThumbnail(name));
    }

    [Fact]
    public void A_generated_name_is_recognised_as_a_thumbnail()
    {
        Assert.True(CoverThumbnails.IsThumbnail(Path.GetFileName(CoverThumbnails.For("/c/x.png", 64))));
    }

    [Fact]
    public void The_original_is_not_mistaken_for_a_thumbnail()
    {
        Assert.False(CoverThumbnails.IsThumbnail(Path.GetFileName("/c/x.64.png.jpg")));
    }
}
