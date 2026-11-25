using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-M-13: metadata that could not be read must not be reported as all-ages. A work whose rating is
/// unknown has to be distinguishable from a work known to be SFW, because the library filter relies on
/// that difference to keep R-18 works out of the All-Age list.
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

    /// <summary>
    /// A page with a rating row still resolves normally, so the change only affects pages where the row
    /// is genuinely absent rather than every page.
    /// </summary>
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

    /// <summary>The R-18 marker must still be detected, otherwise the safety fix would hide R-18 works.</summary>
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

    /// <summary>A page with no age row at all is not a readable product page, so the rating stays unknown.</summary>
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
