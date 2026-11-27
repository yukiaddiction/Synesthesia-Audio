using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class NameRomanizerTests
{
    private static readonly NameReadingTable Table = NameReadingTable.FromLines(new[]
    {
        "水瀬\tMinase",
        "小鳥遊\tTakanashi",
        "田中\tTanaka",
        "一ノ瀬\tIchinose",
        "一\tIchi",
        "# a comment line",
        ""
    });

    // Stand-in for the app's kana table; enough to prove routing, not a kana implementation test.
    private static string Kana(string kana) => kana switch
    {
        "まこ" => "mako",
        "いのり" => "inori",
        "かすみ" => "kasumi",
        _ => kana
    };

    private static NameRomanizer Create() => new(Table, Kana);

    [Fact]
    public void KanaOnlyNameIsRomanized()
    {
        Assert.Equal("Kasumi", Create().RomanizeName("かすみ"));
    }

    [Fact]
    public void KnownKanjiUsesTheTable()
    {
        Assert.Equal("Tanaka", Create().RomanizeName("田中"));
    }

    [Fact]
    public void KanjiAndKanaCombine()
    {
        Assert.Equal("Takanashi Mako", Create().RomanizeName("小鳥遊 まこ"));
    }

    /// <summary>Japanese names often omit the space between surname and given name.</summary>
    [Fact]
    public void MissingSpaceBetweenSurnameAndGivenIsRecovered()
    {
        Assert.Equal("Takanashi Mako", Create().RomanizeName("小鳥遊まこ"));
    }

    [Fact]
    public void KanaInsideASurnameIsNotSplit()
    {
        Assert.Equal("Ichinose", Create().RomanizeName("一ノ瀬"));
    }

    [Fact]
    public void LongestMatchWins()
    {
        Assert.Equal("Ichinose", Create().RomanizeName("一ノ瀬"));
    }

    /// <summary>Regression: the old table turned 五十嵐 into an empty string.</summary>
    [Fact]
    public void UnknownKanjiIsPreservedNotDropped()
    {
        var result = Create().RomanizeName("五十嵐");

        Assert.Equal("五十嵐", result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void PartialMatchKeepsTheUnmatchedPart()
    {
        var result = Create().RomanizeName("田中筋");

        Assert.StartsWith("Tanaka", result, StringComparison.Ordinal);
        Assert.Contains("筋", result, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("CV: 田中", "Tanaka")]
    [InlineData("CV：田中", "Tanaka")]
    [InlineData("CV.田中", "Tanaka")]
    public void CreditPrefixIsStripped(string input, string expected)
    {
        Assert.Equal(expected, Create().RomanizeName(input));
    }

    [Fact]
    public void CastListIsSplitAndJoined()
    {
        var result = Create().RomanizeCastList("田中,かすみ/水瀬");

        Assert.Equal("Tanaka, Kasumi, Minase", result);
    }

    [Fact]
    public void EmptyInputReturnsEmpty()
    {
        Assert.Equal(string.Empty, Create().RomanizeName("   "));
        Assert.Equal(string.Empty, Create().RomanizeCastList(string.Empty));
    }

    [Fact]
    public void EmptyTableStillPreservesText()
    {
        var romanizer = new NameRomanizer(NameReadingTable.Empty, Kana);

        Assert.Equal("五十嵐", romanizer.RomanizeName("五十嵐"));
    }

    /// <summary>The Kawazu fallback needs the whole kanji run so its parser can read it in context.</summary>
    [Fact]
    public void UnknownKanjiFallsBackWithTheWholeRun()
    {
        var seen = new List<string>();
        var romanizer = new NameRomanizer(NameReadingTable.Empty, Kana, span =>
        {
            seen.Add(span);
            return span == "五十嵐" ? "Igarashi" : null;
        });

        Assert.Equal("Igarashi", romanizer.RomanizeName("五十嵐"));
        Assert.Equal(new[] { "五十嵐" }, seen);
    }

    [Fact]
    public void FallbackReturningNullKeepsTheKanji()
    {
        var romanizer = new NameRomanizer(NameReadingTable.Empty, Kana, _ => null);

        Assert.Equal("五十嵐", romanizer.RomanizeName("五十嵐"));
    }

    [Fact]
    public void FallbackIsNotUsedWhenTheTableMatches()
    {
        var calls = 0;
        var romanizer = new NameRomanizer(Table, Kana, _ => { calls++; return "WRONG"; });

        Assert.Equal("Tanaka", romanizer.RomanizeName("田中"));
        Assert.Equal(0, calls);
    }
}
