using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// DUP-08: the work-number comparison was inlined three times in the JSON parser, as
/// <c>Regex.Replace(value, "\D", "")</c>. A work number that matched in one path but not another made the same
/// request succeed or fail depending on which shape the response happened to have.
/// </summary>
public sealed class DlsiteWorkNumberTests
{
    [Theory]
    [InlineData("RJ123456", "123456")]
    [InlineData("rj123456", "123456")]
    [InlineData("123456", "RJ123456")]
    [InlineData("RJ0123456", "0123456")]
    public void The_rj_prefix_does_not_change_identity(string value, string expected)
    {
        Assert.True(DlsiteWorkNumber.RefersToSameWork(value, expected));
    }

    [Theory]
    [InlineData("RJ123456", "RJ654321")]
    [InlineData("RJ123456", "")]
    [InlineData("RJ123456", null)]
    [InlineData("", "123456")]
    [InlineData(null, "123456")]
    public void Different_or_missing_numbers_do_not_match(string? value, string? expected)
    {
        Assert.False(DlsiteWorkNumber.RefersToSameWork(value, expected));
    }

    /// <summary>Leading zeros are part of the number: 0123456 is not 123456.</summary>
    [Fact]
    public void Leading_zeros_are_significant()
    {
        Assert.False(DlsiteWorkNumber.RefersToSameWork("RJ0123456", "RJ123456"));
    }

    [Fact]
    public void An_empty_value_never_matches_an_empty_expectation()
    {
        // Otherwise a response with no work number at all would be accepted as a match.
        Assert.False(DlsiteWorkNumber.RefersToSameWork("", ""));
        Assert.False(DlsiteWorkNumber.RefersToSameWork("RJ", "RJ"));
    }

    [Theory]
    [InlineData("RJ123456", "123456")]
    [InlineData("  RJ123456  ", "123456")]
    [InlineData("RJ-123456", "123456")]
    [InlineData("no digits", "")]
    [InlineData(null, "")]
    public void Digits_are_extracted(string? value, string expected)
    {
        Assert.Equal(expected, DlsiteWorkNumber.DigitsOf(value));
    }

    [Fact]
    public void A_non_ascii_digit_is_not_treated_as_a_digit()
    {
        // char.IsDigit would accept these; a DLsite work number cannot contain them.
        Assert.Equal("123", DlsiteWorkNumber.DigitsOf("1٢2３3"));
    }
}
