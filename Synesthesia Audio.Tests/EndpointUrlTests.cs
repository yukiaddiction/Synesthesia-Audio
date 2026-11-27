using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>A custom endpoint receives the API key in a header, so plain http is allowed only to the local machine.</summary>
public sealed class EndpointUrlTests
{
    [Theory]
    [InlineData("https://api.deepl.com")]
    [InlineData("https://api-free.deepl.com")]
    [InlineData("https://translate.example.com/v2")]
    public void Https_is_always_acceptable(string url)
    {
        Assert.True(EndpointUrl.IsSecureOrLocal(url));
    }

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("http://127.0.0.1:5000")]
    [InlineData("http://[::1]:9000")]
    public void Http_to_the_local_machine_is_acceptable(string url)
    {
        Assert.True(EndpointUrl.IsSecureOrLocal(url));
    }

    [Theory]
    [InlineData("http://api.deepl.com")]
    [InlineData("http://translate.example.com")]
    [InlineData("http://192.168.1.10:8080")]
    public void Http_to_a_remote_host_is_rejected(string url)
    {
        Assert.False(EndpointUrl.IsSecureOrLocal(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com")]
    [InlineData("/relative/path")]
    public void Anything_unparseable_or_non_http_is_rejected(string? url)
    {
        Assert.False(EndpointUrl.IsSecureOrLocal(url));
    }

    [Fact]
    public void A_rejected_candidate_falls_back_to_the_default()
    {
        const string fallback = "https://api.deepl.com";

        Assert.Equal(fallback, EndpointUrl.UseOrFallback("http://evil.example.com", fallback));
        Assert.Equal(fallback, EndpointUrl.UseOrFallback(null, fallback));
    }

    [Fact]
    public void An_accepted_candidate_is_used_without_a_trailing_slash()
    {
        Assert.Equal("https://translate.example.com/v2",
            EndpointUrl.UseOrFallback("https://translate.example.com/v2/", "https://api.deepl.com"));
    }
}
