using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Unreadable metadata must stay Unknown rather than SFW, because the All-Age filter relies on that difference
/// to keep R-18 works out.
/// </summary>
public sealed class AgeRatingTests
{
    [Fact]
    public void Unknown_is_not_sfw_and_not_nsfw()
    {
        Assert.False(AgeRatings.IsSfw(AgeRatings.Unknown));
        Assert.False(AgeRatings.IsNsfw(AgeRatings.Unknown));
        Assert.True(AgeRatings.IsUnknown(AgeRatings.Unknown));
    }

    [Fact]
    public void Sfw_and_nsfw_are_classified_exactly()
    {
        Assert.True(AgeRatings.IsSfw("SFW"));
        Assert.False(AgeRatings.IsUnknown("SFW"));

        Assert.True(AgeRatings.IsNsfw("NSFW"));
        Assert.False(AgeRatings.IsUnknown("NSFW"));
    }

    [Fact]
    public void Comparison_is_case_insensitive()
    {
        Assert.True(AgeRatings.IsSfw("sfw"));
        Assert.True(AgeRatings.IsNsfw("Nsfw"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unrated")]
    [InlineData("Unknown")]
    public void Anything_that_is_not_a_known_rating_is_unknown(string? value)
    {
        Assert.True(AgeRatings.IsUnknown(value));
        Assert.False(AgeRatings.IsSfw(value));
    }

    /// <summary>Guards against the Unknown fallback also catching pages that do carry a rating row.</summary>
    [Fact]
    public void A_page_with_an_all_ages_row_still_reports_sfw()
    {
        const string html = """
            <html><body>
            <table><tr><th>年齢指定</th><td><span class="label_allages">全年齢</span></td></tr></table>
            <h1 id="work_name">RJ000001</h1>
            </body></html>
            """;

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000001.html");

        Assert.Equal(AgeRatings.Sfw, result.AgeRating);
    }

    /// <summary>Guards that the Unknown fallback did not swallow R-18 detection.</summary>
    [Fact]
    public void A_page_with_an_r18_row_reports_nsfw()
    {
        const string html = """
            <html><body>
            <table><tr><th>年齢指定</th><td><span class="label_r18">R18</span></td></tr></table>
            <h1 id="work_name">RJ000001</h1>
            </body></html>
            """;

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000001.html");

        Assert.Equal(AgeRatings.Nsfw, result.AgeRating);
    }

    /// <summary>No age row means this is not a readable product page, so nothing is known about the rating.</summary>
    [Fact]
    public void A_page_without_an_age_row_reports_unknown()
    {
        const string html = """
            <html><body><h1 id="work_name">RJ000001</h1></body></html>
            """;

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000001.html");

        Assert.Equal(AgeRatings.Unknown, result.AgeRating);
    }
}
