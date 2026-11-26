using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// DUP-08: the base-URL rule was written twice — on the connection object and again where file URLs are built —
/// so the two could disagree about a trailing slash and produce different request paths.
/// </summary>
public sealed class WebDavUrlTests
{
    [Theory]
    [InlineData("https://host/dav", "https://host/dav/")]
    [InlineData("https://host/dav/", "https://host/dav/")]
    [InlineData("https://host/dav////", "https://host/dav/")]
    [InlineData("https://host", "https://host/")]
    [InlineData("  https://host/dav  ", "https://host/dav/")]
    public void Exactly_one_trailing_slash(string input, string expected)
    {
        Assert.Equal(expected, WebDavUrl.NormalizeBase(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_address_stays_empty(string? input)
    {
        // Normalising nothing must not produce "/", which would resolve as a relative root.
        Assert.Equal(string.Empty, WebDavUrl.NormalizeBase(input));
    }

    [Fact]
    public void A_typed_address_and_its_slashed_form_normalise_alike()
    {
        Assert.Equal(
            WebDavUrl.NormalizeBase("https://host/remote.php/dav/files/user"),
            WebDavUrl.NormalizeBase("https://host/remote.php/dav/files/user/"));
    }
}
