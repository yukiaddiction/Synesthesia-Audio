using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public sealed class DlsiteUrlTests
{
    [Fact]
    public void A_protocol_relative_url_becomes_https()
    {
        Assert.Equal("https://img.dlsite.jp/x.jpg", DlsiteUrl.EnsureAbsolute("//img.dlsite.jp/x.jpg"));
    }

    [Fact]
    public void An_already_absolute_url_is_untouched()
    {
        Assert.Equal("https://img.dlsite.jp/x.jpg", DlsiteUrl.EnsureAbsolute("https://img.dlsite.jp/x.jpg"));
        Assert.Equal("http://example.test/x.jpg", DlsiteUrl.EnsureAbsolute("http://example.test/x.jpg"));
    }

    [Fact]
    public void Surrounding_whitespace_is_removed()
    {
        Assert.Equal("https://img.dlsite.jp/x.jpg", DlsiteUrl.EnsureAbsolute("  https://img.dlsite.jp/x.jpg  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_value_does_not_become_a_bare_scheme(string? value)
    {
        Assert.Equal(string.Empty, DlsiteUrl.EnsureAbsolute(value));
    }

    [Fact]
    public void A_relative_path_is_left_alone_rather_than_guessed()
    {
        // No protocol and no leading slashes: inventing a host would be worse than returning it unchanged.
        Assert.Equal("img/x.jpg", DlsiteUrl.EnsureAbsolute("img/x.jpg"));
    }

    [Theory]
    [InlineData("https://img.dlsite.jp/x.jpg")]
    [InlineData("http://img.dlsite.jp/x.jpg")]
    public void An_absolute_http_url_is_usable(string url)
    {
        Assert.True(DlsiteUrl.IsUsableImageUrl(url));
    }

    [Theory]
    [InlineData("//img.dlsite.jp/x.jpg")]   // protocol-relative, not absolute
    [InlineData("img/x.jpg")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/secret.png")]
    public void Anything_else_is_not_a_usable_image_url(string? url)
    {
        Assert.False(DlsiteUrl.IsUsableImageUrl(url));
    }
}
