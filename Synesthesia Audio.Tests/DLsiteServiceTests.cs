using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class DLsiteParseWorkPageHtmlTests
{

    private static string WrapInDlsiteShell(string bodyContent, string headExtras = "")
    {
        return $@"<!DOCTYPE html>
<html lang=""ja"">
<head>
<meta charset=""utf-8"">
{headExtras}
</head>
<body>
<div id=""wrapper"">
{bodyContent}
</div>
</body>
</html>";
    }

    [Fact]
    public void ParsesTitleFromEnglishPageWithOriginalJapaneseTitle()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">Sweet Lullaby</h1>
            <table><tr>
                <th>Original title</th>
                <td>甘い子守唄</td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("甘い子守唄", result.OriginalTitleRaw);
        Assert.Equal("Sweet Lullaby", result.ParsedTitle);
        Assert.True(result.IsEngPage);
        Assert.True(result.TitleFound);
    }

    [Fact]
    public void ParsesTitleFromEnglishPageWithoutOriginalTitle()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">Sweet Lullaby</h1>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("Sweet Lullaby", result.ParsedTitle);
        Assert.Equal("", result.OriginalTitleRaw);
        Assert.True(result.IsEngPage);
    }

    [Fact]
    public void ParsesTitleFromManiaxPage_Japanese()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">甘い子守唄</h1>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ999999.html");

        Assert.Equal("甘い子守唄", result.ParsedTitle);
        Assert.False(result.IsEngPage);
        Assert.True(result.TitleFound);
    }

    [Fact]
    public void ParsesTitleFromHomePage_Japanese()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">癒しのボイス</h1>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/home/work/=/product_id/RJ888888.html");

        Assert.Equal("癒しのボイス", result.ParsedTitle);
        Assert.False(result.IsEngPage);
    }

    [Fact]
    public void TitleMissing_ReturnsUnknownTitle()
    {
        var html = WrapInDlsiteShell(@"
            <p>No work name here</p>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000000.html");

        Assert.Equal("Unknown Title", result.ParsedTitle);
        Assert.False(result.TitleFound);
    }

    [Fact]
    public void TitleWithHtmlEntities_Decoded()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">Sweet &amp; Salty &quot;Voice&quot; &lt;3</h1>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000001.html");

        Assert.Equal("Sweet & Salty \"Voice\" <3", result.ParsedTitle);
    }

    [Fact]
    public void ParsesCircleName()
    {
        var html = WrapInDlsiteShell(@"
            <span class=""maker_name""><a href=""/circle/123"">Whisp</a></span>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000001.html");

        Assert.Equal("Whisp", result.Circle);
        Assert.True(result.CircleFound);
    }

    [Fact]
    public void CircleMissing_ReturnsUnknownCircle()
    {
        var html = WrapInDlsiteShell(@"
            <p>no maker span here</p>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000001.html");

        Assert.Equal("Unknown Circle", result.Circle);
        Assert.False(result.CircleFound);
    }

    [Fact]
    public void CircleNameWithHtmlEntities()
    {
        var html = WrapInDlsiteShell(@"
            <span class=""maker_name""><a href=""/circle/1"">Studio &amp; Co.</a></span>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000001.html");

        Assert.Equal("Studio & Co.", result.Circle);
    }

    [Fact]
    public void ParsesCoverUrl_Itemprop()
    {
        var html = WrapInDlsiteShell(@"
            <meta itemprop=""image"" content=""//img.dlsite.jp/modpub/images2/work/books/BJ123000/BJ123456_img_main.jpg"">
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("https://img.dlsite.jp/modpub/images2/work/books/BJ123000/BJ123456_img_main.jpg", result.CoverUrl);
        Assert.True(result.CoverFound);
    }

    [Fact]
    public void ParsesCoverUrl_OgImage_Fallback()
    {
        var html = WrapInDlsiteShell(@"
            <meta property=""og:image"" content=""//img.dlsite.jp/cover.jpg"">
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("https://img.dlsite.jp/cover.jpg", result.CoverUrl);
        Assert.True(result.CoverFound);
    }

    [Fact]
    public void CoverUrl_ItempropTakesPriorityOverOgImage()
    {
        var html = WrapInDlsiteShell(@"
            <meta itemprop=""image"" content=""//img.dlsite.jp/primary.jpg"">
            <meta property=""og:image"" content=""//img.dlsite.jp/fallback.jpg"">
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("https://img.dlsite.jp/primary.jpg", result.CoverUrl);
    }

    [Fact]
    public void CoverUrl_AbsoluteHttps_Untouched()
    {
        var html = WrapInDlsiteShell(@"
            <meta itemprop=""image"" content=""https://img.dlsite.jp/cover.jpg"">
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("https://img.dlsite.jp/cover.jpg", result.CoverUrl);
    }

    [Fact]
    public void CoverMissing_ReturnsEmpty()
    {
        var html = WrapInDlsiteShell(@"
            <p>no image meta</p>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("", result.CoverUrl);
        Assert.False(result.CoverFound);
    }

    [Fact]
    public void ParsesEnglishVoiceActor()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>Voice Actor</th>
                <td><a href=""/va/1"">Alice</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("Alice", result.VaRaw);
    }

    [Fact]
    public void ParsesJapaneseVoiceActor_Seiyuu()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>声優</th>
                <td><a href=""/va/2"">花澤香菜</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ999999.html");

        Assert.Equal("花澤香菜", result.VaRaw);
    }

    [Fact]
    public void ParsesCastLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>Cast</th>
                <td><a href=""/va/3"">Bob</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123457.html");

        Assert.Equal("Bob", result.VaRaw);
    }

    [Fact]
    public void ParsesMultipleVoiceActors_CommaSeparated()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>Voice Actor</th>
                <td>
                    <a href=""/va/1"">Alice</a>,
                    <a href=""/va/2"">Bob</a>,
                    <a href=""/va/3"">Charlie</a>
                </td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("Alice, Bob, Charlie", result.VaRaw);
    }

    [Fact]
    public void ParsesKyarakutaaLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>キャラクター</th>
                <td><a href=""/va/4"">キャラA</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/home/work/=/product_id/RJ888888.html");

        Assert.Equal("キャラA", result.VaRaw);
    }

    [Fact]
    public void VaMissing_ReturnsEmptyString()
    {
        var html = WrapInDlsiteShell(@"
            <p>no VA info</p>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("", result.VaRaw);
    }

    [Fact]
    public void ParsesShutsuenLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>出演</th>
                <td><a href=""/va/5"">声優X</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/home/work/=/product_id/RJ000002.html");

        Assert.Equal("声優X", result.VaRaw);
    }

    [Fact]
    public void ParsesEnglishGenres()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>Genre(s)</th>
                <td>
                    <a href=""/genre/1"">ASMR</a>
                    <a href=""/genre/2"">Healing</a>
                    <a href=""/genre/3"">Voice</a>
                </td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("ASMR, Healing, Voice", result.Genre);
    }

    [Fact]
    public void ParsesJapaneseGenres()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>ジャンル</th>
                <td>
                    <a href=""/genre/1"">癒し</a>
                    <a href=""/genre/2"">ASMR</a>
                </td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ999999.html");

        Assert.Contains("癒し", result.Genre);
        Assert.Contains("ASMR", result.Genre);
    }

    [Fact]
    public void ParsesKategoriLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>カテゴリ</th>
                <td><a href=""/cat/1"">癒し</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000005.html");

        Assert.Equal("癒し", result.Genre);
    }

    [Fact]
    public void ParsesKategoriLongLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>カテゴリー</th>
                <td><a href=""/cat/1"">ASMR</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000006.html");

        Assert.Equal("ASMR", result.Genre);
    }

    [Fact]
    public void ParsesGenreWithColonAfterLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>ジャンル：</th>
                <td><a href=""/genre/1"">音声作品</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000007.html");

        Assert.Equal("音声作品", result.Genre);
    }

    [Fact]
    public void ParsesGenreWithFullwidthColon()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>ジャンル：</th>
                <td><a href=""/genre/1"">ボイス</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000008.html");

        Assert.Equal("ボイス", result.Genre);
    }

    [Fact]
    public void ParsesTagsLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>タグ</th>
                <td><a href=""/tag/1"">Tag1</a></td>
            </tr></table>
        ");
        
        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/home/work/=/product_id/RJ888888.html");

        Assert.Equal("Tag1", result.Genre);
    }

    [Fact]
    public void ParsesBunruiLabel()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>分類</th>
                <td><a href=""/cat/1"">音声作品</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/home/work/=/product_id/RJ000005.html");

        Assert.Equal("音声作品", result.Genre);
    }

    [Fact]
    public void GenreMissing_ReturnsEmpty()
    {
        var html = WrapInDlsiteShell(@"
            <p>no genre row</p>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("", result.Genre);
    }

    [Fact]
    public void GenreWithHtmlEntities()
    {
        var html = WrapInDlsiteShell(@"
            <table><tr>
                <th>Genre</th>
                <td><a href=""/genre/1"">Love &amp; Romance</a></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.Equal("Love & Romance", result.Genre);
    }

    [Fact]
    public void EmptyHtml_ParsesToDefaults()
    {
        var result = DLsiteParser.ParseWorkPageHtml("", "https://www.dlsite.com/eng/work/=/product_id/RJ000000.html");

        Assert.Equal("Unknown Title", result.ParsedTitle);
        Assert.Equal("", result.CoverUrl);
        Assert.Equal("Unknown Circle", result.Circle);
        Assert.Equal("", result.VaRaw);
        // An empty page carries no age row, so the rating is unknown. It used to default to SFW, which
        // put R-18 works in the All-Age list whenever DLsite was blocked (BUG-M-13).
        Assert.Equal(AgeRatings.Unknown, result.AgeRating);
        Assert.Equal("", result.Genre);
        Assert.False(result.TitleFound);
    }

    [Fact]
    public void ExtractDocumentTitle_ReadsTitleTag()
    {
        var html = WrapInDlsiteShell("", @"<title>DLsite</title>");

        Assert.Equal("DLsite", DLsiteParser.ExtractDocumentTitle(html));
    }

    [Fact]
    public void ExtractDocumentTitle_EmptyHtml_ReturnsEmpty()
    {
        Assert.Equal("", DLsiteParser.ExtractDocumentTitle(""));
    }

    [Fact]
    public void AgeGatePage_IsNotTreatedAsAWork()
    {
        // What DLsite actually served for every work: the age-consent page, whose only title is
        // the bare site name. It must never be stored as a work.
        var html = WrapInDlsiteShell(
            "<div class=\"age_check\">Are you over 18?</div>",
            @"<title>DLsite</title><meta property=""og:title"" content=""DLsite"" />");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ277878.html");

        Assert.False(result.TitleFound);
        Assert.Equal("Unknown Title", result.ParsedTitle);
        Assert.Equal("DLsite", DLsiteParser.ExtractDocumentTitle(html));
    }

    [Fact]
    public void CurrentTemplate_TitleComesFromDocumentTitle()
    {
        // The real page: og:title is the bare site name and there is no usable work_name heading,
        // so the only place the work title survives is the document title.
        var html = WrapInDlsiteShell(
            "",
            @"<title>【6時間ASMR】溺れるまで甘やかしたい [ななしぃんく] | DLsite 同人 - R18</title>" +
            @"<meta property=""og:title"" content=""DLsite"" />");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ01510352.html");

        Assert.True(result.TitleFound);
        Assert.Equal("【6時間ASMR】溺れるまで甘やかしたい [ななしぃんく]", result.ParsedTitle);
        Assert.Equal("document-title", result.TitleSource);
    }

    [Fact]
    public void WorkNameHeading_WithNestedTags_IsParsed()
    {
        var html = WrapInDlsiteShell(@"<h1 id=""work_name""><span>Nested</span> Title</h1>");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.True(result.TitleFound);
        Assert.Equal("Nested Title", result.ParsedTitle);
        Assert.Equal("h1", result.TitleSource);
    }

    [Fact]
    public void ErrorPage_TitleIsRejected()
    {
        var html = WrapInDlsiteShell("", @"<title>ページが見つかりません | DLsite</title>");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000000.html");

        Assert.False(result.TitleFound);
        Assert.Equal("Unknown Title", result.ParsedTitle);
    }

    [Fact]
    public void FieldFlags_ReflectWhatWasParsed()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">Flagged Work</h1>
            <span class=""maker_name""><a href=""/maker/1"">Some Circle</a></span>
            <table>
              <tr><th>Voice Actor</th><td><a href=""/va/1"">Someone</a></td></tr>
              <tr><th>Genre</th><td><a href=""/genre/1"">Tag One</a></td></tr>
            </table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ123456.html");

        Assert.True(result.CircleFound);
        Assert.True(result.VaFound);
        Assert.True(result.GenreFound);
        Assert.True(result.HasMetaTable);
        Assert.False(result.HasJsonLd);
    }

    [Fact]
    public void JsonLdRootSiteName_DoesNotBlockNestedProductNode()
    {
        var jsonLd = @"
<script type=""application/ld+json"">
{
  ""@context"": ""https://schema.org"",
  ""name"": ""DLsite"",
  ""@graph"": [
    { ""@type"": ""WebSite"", ""name"": ""DLsite"" },
    {
      ""@type"": ""Product"",
      ""name"": ""Real Work Name"",
      ""image"": ""//img.example/cover.jpg"",
      ""brand"": { ""@type"": ""Brand"", ""name"": ""Some Circle"" },
      ""keywords"": [""Tag One"", ""Tag Two""]
    }
  ]
}
</script>";

        var html = WrapInDlsiteShell("", jsonLd);

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html");

        Assert.True(result.TitleFound);
        Assert.Equal("Real Work Name", result.ParsedTitle);
        Assert.Equal("jsonld", result.TitleSource);
        Assert.Equal("Some Circle", result.Circle);
        Assert.True(result.GenreFound);
        Assert.Equal("Tag One, Tag Two", result.Genre);
        Assert.Equal("https://img.example/cover.jpg", result.CoverUrl);
    }

    [Fact]
    public void InvalidJsonLdTitle_DoesNotBlockDocumentTitle()
    {
        // The regression that shipped: a rejected JSON-LD name occupied the title field and every
        // later strategy was skipped because it only checked for emptiness.
        var head = @"<script type=""application/ld+json"">{""@type"":""WebSite"",""name"":""DLsite""}</script>" +
                   @"<title>Fallback Work Name | DLsite 同人 - R18</title>";

        var html = WrapInDlsiteShell("", head);

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html");

        Assert.True(result.TitleFound);
        Assert.Equal("Fallback Work Name", result.ParsedTitle);
        Assert.Equal("document-title", result.TitleSource);
    }

    [Fact]
    public void CastInTitle_IsUsedWhenNoCastRowExists()
    {
        var head = @"<title>【百合体験】はつこいリターンズ!～ミステリアス声優は上手く喋れない～【CV:土屋李央】 | DLsite 同人 - R18</title>";

        var html = WrapInDlsiteShell("", head);

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ01026607.html");

        Assert.True(result.TitleFound);
        Assert.True(result.VaFound);
        Assert.Equal("土屋李央", result.VaRaw);
    }

    [Fact]
    public void CastRowWins_OverCastInTitle()
    {
        var head = @"<title>Some Work【CV:タイトル内】 | DLsite 同人 - R18</title>";
        var body = @"<table><tr><th>声優</th><td><a href=""/va/9"">表の声優</a></td></tr></table>";

        var html = WrapInDlsiteShell(body, head);

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html");

        Assert.Equal("表の声優", result.VaRaw);
    }

    [Fact]
    public void CurrentTemplate_ReadsCastFromDefinitionList()
    {
        // Shape taken from a real 2026 DLsite product page.
        var body = @"
            <div class=""c-productInfo__item"">
              <dt class=""c-productInfo__key"">声優</dt>
              <dd class=""c-productInfo__value""><span><a href=""https://www.dlsite.com/maniax/fsr/=/keyword_creater/x"">土屋李央</a></span></dd>
            </div>";

        var html = WrapInDlsiteShell(body);

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ01026607.html");

        Assert.True(result.VaFound);
        Assert.Equal("土屋李央", result.VaRaw);
    }

    [Fact]
    public void CurrentTemplate_ReadsGenreFromGenreLinks()
    {
        var body = @"
            <a href=""https://www.dlsite.com/maniax/fsr/=/genre/305/from/work.genre/"">百合</a>
            <a href=""https://www.dlsite.com/maniax/fsr/=/genre/102/from/work.genre/"">ASMR</a>";

        var html = WrapInDlsiteShell(body);

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ01026607.html");

        Assert.True(result.GenreFound);
        Assert.Equal("百合, ASMR", result.Genre);
    }

    [Fact]
    public void BlockedPage_WithSiteNameAsOgTitle_IsNotTreatedAsAWork()
    {
        // Region-block and age-consent pages return HTTP 200 with the site's own name in
        // og:title, which used to be stored as the work title.
        var html = WrapInDlsiteShell(
            "",
            @"<meta property=""og:title"" content=""DLsite"" />");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html");

        Assert.False(result.TitleFound);
        Assert.NotEqual("DLsite", result.ParsedTitle);
        Assert.Equal("Unknown Title", result.ParsedTitle);
    }

    [Fact]
    public void NonEnglishPage_WithGenuineOgTitle_StillAcceptsTheTitle()
    {
        var html = WrapInDlsiteShell(
            "",
            @"<meta property=""og:title"" content=""Genuine Work Title"" />");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ123456.html");

        Assert.True(result.TitleFound);
        Assert.Equal("Genuine Work Title", result.ParsedTitle);
    }

    [Fact]
    public void MinimalHtml_OnlyTitle()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">Minimal Work</h1>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000003.html");

        Assert.Equal("Minimal Work", result.ParsedTitle);
        Assert.True(result.TitleFound);
        Assert.Equal("Unknown Circle", result.Circle);
        Assert.Equal("", result.CoverUrl);
    }

    [Fact]
    public void OriginalTitleWithSpanWrapper()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">English Title</h1>
            <table><tr>
                <th>Original title</th>
                <td><span>日本語タイトル</span></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000004.html");

        Assert.Equal("日本語タイトル", result.OriginalTitleRaw);
        Assert.Equal("English Title", result.ParsedTitle);
    }

    [Fact]
    public void OriginalTitleWithHtmlInside()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">English Work</h1>
            <table><tr>
                <th>Original title</th>
                <td><a href=""#"">日本語</a> with extra <span>text</span></td>
            </tr></table>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000007.html");

        Assert.Equal("日本語 with extra text", result.OriginalTitleRaw);
    }

    [Fact]
    public void TitleWithExtraAttributesInH1()
    {
        var html = WrapInDlsiteShell(@"
            <h1 class=""work-title"" id=""work_name"" data-work=""123"">Title With Attributes</h1>
        ");

        var result = DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000008.html");

        Assert.Equal("Title With Attributes", result.ParsedTitle);
    }

    [Fact]
    public void UrlContainsEng_DetectedAsEngPage()
    {
        var html = WrapInDlsiteShell(@"
            <h1 id=""work_name"">Test</h1>
        ");

        Assert.True(DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/eng/work/=/product_id/RJ000001.html").IsEngPage);
        Assert.False(DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/maniax/work/=/product_id/RJ000001.html").IsEngPage);
        Assert.False(DLsiteParser.ParseWorkPageHtml(html, "https://www.dlsite.com/home/work/=/product_id/RJ000001.html").IsEngPage);
    }
}

public class DLsiteParseTranslateJsonTests
{
    [Fact]
    public void ParsesSingleTranslation()
    {
        var json = @"[[[""Hello World"",""こんにちは世界"",null,null,1]],null,""ja""]";

        var result = DLsiteParser.ParseTranslateJson(json);

        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ParsesBatchTranslation_ConcatenatesAllSegments()
    {
        var json = @"[[[""Good morning"",""おはよう"",null,null,1],[""Good night"",""おやすみ"",null,null,1]],null,""ja""]";

        var result = DLsiteParser.ParseTranslateJson(json);

        Assert.Equal("Good morningGood night", result);
    }

    [Fact]
    public void ParsesRomajiTransliteration()
    {
        var json = @"[[[""ohayou"",""おはよう"",null,null,1]],null,""ja""]";

        var result = DLsiteParser.ParseTranslateJson(json);

        Assert.Equal("ohayou", result);
    }

    [Fact]
    public void EmptyJson_ReturnsEmpty()
    {
        Assert.Equal("", DLsiteParser.ParseTranslateJson(""));
        Assert.Equal("", DLsiteParser.ParseTranslateJson("   "));
    }

    [Fact]
    public void InvalidJson_ReturnsEmpty()
    {
        Assert.Equal("", DLsiteParser.ParseTranslateJson("not json at all"));
        Assert.Equal("", DLsiteParser.ParseTranslateJson("{ broken: true"));
    }

    [Fact]
    public void EmptyArray_ReturnsEmpty()
    {
        var json = @"[]";

        var result = DLsiteParser.ParseTranslateJson(json);

        Assert.Equal("", result);
    }

    [Fact]
    public void JsonWithEscapedCharacters()
    {
        var json = @"[[[""He said \""Hello\"""",""彼は「こんにちは」と言った"",null,null,1]],null,""ja""]";

        var result = DLsiteParser.ParseTranslateJson(json);

        Assert.Contains("Hello", result);
    }
}
