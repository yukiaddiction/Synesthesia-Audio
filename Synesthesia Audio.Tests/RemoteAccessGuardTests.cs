using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Tests for the two guards that keep the WebDAV integration contained: the loopback proxy token
/// (SEC-M-01) and the remote path rule (SEC-M-02).
/// </summary>
public sealed class RemoteAccessGuardTests
{
    // ---- SEC-M-01: the proxy refuses anything without the current run's token ----

    [Fact]
    public void Token_matches_its_own_value()
    {
        var token = ProxyAccessToken.Create();

        Assert.True(token.Matches(token.Value));
    }

    [Fact]
    public void Token_rejects_missing_empty_and_wrong_values()
    {
        var token = ProxyAccessToken.Create();

        Assert.False(token.Matches(null));
        Assert.False(token.Matches(string.Empty));
        Assert.False(token.Matches("not-the-token"));
        Assert.False(token.Matches(token.Value + "x"));
        Assert.False(token.Matches(token.Value[..^1]));
    }

    [Fact]
    public void Tokens_differ_between_instances()
    {
        Assert.NotEqual(ProxyAccessToken.Create().Value, ProxyAccessToken.Create().Value);
    }

    [Fact]
    public void Token_value_is_url_safe()
    {
        var value = ProxyAccessToken.Create().Value;

        Assert.DoesNotContain('+', value);
        Assert.DoesNotContain('/', value);
        Assert.DoesNotContain('=', value);
    }

    // ---- SEC-M-02: a remote path may never climb out of the base folder ----

    [Theory]
    [InlineData("../../x")]
    [InlineData("../x")]
    [InlineData("a/../../x")]
    [InlineData("a/../x")]
    [InlineData("./x")]
    [InlineData("a/./b")]
    [InlineData("..")]
    [InlineData(@"..\..\x")]
    [InlineData("%2e%2e/x")]
    [InlineData("a/%2E%2E/x")]
    public void Traversal_attempts_are_rejected(string path)
    {
        Assert.Throws<ArgumentException>(() => RemotePath.NormalizeRelative(path));
    }

    [Theory]
    [InlineData("RJ000001/01.mp3", "RJ000001/01.mp3")]
    [InlineData("/RJ000001/01.mp3", "RJ000001/01.mp3")]
    [InlineData(@"RJ000001\01.mp3", "RJ000001/01.mp3")]
    public void Ordinary_paths_are_normalised(string input, string expected)
    {
        Assert.Equal(expected, RemotePath.NormalizeRelative(input));
    }

    [Fact]
    public void Empty_path_is_allowed()
    {
        Assert.Equal(string.Empty, RemotePath.NormalizeRelative(null));
        Assert.Equal(string.Empty, RemotePath.NormalizeRelative(""));
        Assert.Equal(string.Empty, RemotePath.NormalizeRelative("/"));
    }

    [Fact]
    public void A_name_that_merely_contains_dots_is_allowed()
    {
        // Only whole dot segments are dangerous; "..."/"..mp3" are ordinary names.
        Assert.Equal("..mp3/a", RemotePath.NormalizeRelative("..mp3/a"));
        Assert.Equal("a/...", RemotePath.NormalizeRelative("a/..."));
    }

    [Fact]
    public void Base_folder_containment_is_enforced()
    {
        Assert.True(RemotePath.IsInsideBase("dav/RJ000001/01.mp3", "dav"));
        Assert.False(RemotePath.IsInsideBase("other/01.mp3", "dav"));
        Assert.False(RemotePath.IsInsideBase("davish/01.mp3", "dav"));
    }
}
