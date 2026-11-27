using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Users attach the log to bug reports, so it must not carry URL credentials, query-string tokens, or the shape
/// of someone's library.
/// </summary>
public sealed class LogRedactorTests
{
    [Fact]
    public void Userinfo_is_stripped_from_a_url()
    {
        var redacted = LogRedactor.Url("https://alice:s3cret@webdav.example.com/dav/music/");

        Assert.DoesNotContain("alice", redacted);
        Assert.DoesNotContain("s3cret", redacted);
        Assert.Contains("webdav.example.com", redacted);
    }

    [Fact]
    public void A_query_string_is_dropped()
    {
        var redacted = LogRedactor.Url("https://webdav.example.com/dav?token=abc123&path=/music");

        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("token", redacted);
        Assert.Equal("https://webdav.example.com/dav", redacted);
    }

    [Fact]
    public void A_plain_url_is_preserved()
    {
        Assert.Equal("https://webdav.example.com/dav/music",
            LogRedactor.Url("https://webdav.example.com/dav/music"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_values_are_marked(string? value)
    {
        Assert.Equal("(none)", LogRedactor.Url(value));
        Assert.Equal("(none)", LogRedactor.FileName(value));
    }

    [Fact]
    public void An_unparseable_url_is_not_echoed()
    {
        Assert.Equal("(unparseable url)", LogRedactor.Url("not a url with s3crets"));
    }

    [Theory]
    [InlineData("/storage/emulated/0/Music/RJ000001/01.mp3", "01.mp3")]
    [InlineData(@"C:\Music\RJ000001\01.mp3", "01.mp3")]
    [InlineData("01.mp3", "01.mp3")]
    [InlineData("https://webdav.example.com/dav/music/01.mp3", "01.mp3")]
    public void A_file_name_keeps_only_the_last_segment(string path, string expected)
    {
        Assert.Equal(expected, LogRedactor.FileName(path));
    }

    [Fact]
    public void A_full_cloud_track_path_discloses_neither_user_nor_layout()
    {
        var redacted = LogRedactor.FileName("https://alice:s3cret@webdav.example.com/dav/Private/RJ000001/01.mp3");

        Assert.Equal("01.mp3", redacted);
        Assert.DoesNotContain("alice", redacted);
        Assert.DoesNotContain("Private", redacted);
    }
}
