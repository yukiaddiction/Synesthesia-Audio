using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// SEC-m-05: cover URLs come out of a scraped page, so they are attacker-influenced input. A page could point
/// the app at another host, a non-https scheme, or a body far larger than an image.
/// </summary>
public sealed class CoverUrlPolicyTests
{
    [Theory]
    [InlineData("https://img.dlsite.jp/modpub/images2/work/doujin/RJ000001/RJ000001_img_main.jpg")]
    [InlineData("https://www.dlsite.com/image.jpg")]
    [InlineData("https://dlsite.com/image.jpg")]
    [InlineData("https://static.dlsite.jp/image.jpg")]
    [InlineData("https://dl.format.co.jp/image.jpg")]
    public void Https_from_a_dlsite_host_is_allowed(string url)
    {
        Assert.True(CoverUrlPolicy.IsAllowed(url));
    }

    [Theory]
    [InlineData("http://img.dlsite.jp/image.jpg")]          // cleartext
    [InlineData("https://evil.example.com/image.jpg")]      // foreign host
    [InlineData("https://dlsite.jp.evil.com/image.jpg")]    // suffix spoof
    [InlineData("https://notdlsite.com/image.jpg")]         // prefix spoof
    [InlineData("file:///etc/passwd")]                      // local file
    [InlineData("ftp://img.dlsite.jp/image.jpg")]
    public void Anything_else_is_rejected(string url)
    {
        Assert.False(CoverUrlPolicy.IsAllowed(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("/relative/image.jpg")]
    public void Unusable_input_is_rejected(string? url)
    {
        Assert.False(CoverUrlPolicy.IsAllowed(url));
    }

    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("IMAGE/JPEG", true)]
    [InlineData("text/html", false)]   // a login or error page served with 200
    [InlineData("application/octet-stream", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void Content_type_must_be_an_image(string? mediaType, bool expected)
    {
        Assert.Equal(expected, CoverUrlPolicy.IsAcceptableContentType(mediaType));
    }

    [Fact]
    public void A_declared_length_over_the_cap_is_rejected()
    {
        Assert.True(CoverUrlPolicy.IsAcceptableLength(null));               // chunked responses declare nothing
        Assert.True(CoverUrlPolicy.IsAcceptableLength(1024));
        Assert.True(CoverUrlPolicy.IsAcceptableLength(CoverUrlPolicy.MaxCoverBytes));
        Assert.False(CoverUrlPolicy.IsAcceptableLength(CoverUrlPolicy.MaxCoverBytes + 1));
        Assert.False(CoverUrlPolicy.IsAcceptableLength(0));
    }
}
