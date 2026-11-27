using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Disc and format folders must resolve to the folder above them, or a two-disc album shows as two works with
/// half the tracks each.
/// </summary>
public sealed class WorkFolderResolverTests
{
    private const string Base = "/music";

    private static string Cloud(string file) => WorkFolderResolver.Resolve(file, Base, isCloud: true);

    [Theory]
    [InlineData("/music/WorkA/CD1/01.mp3")]
    [InlineData("/music/WorkA/CD2/01.mp3")]
    [InlineData("/music/WorkA/Disc1/01.mp3")]
    [InlineData("/music/WorkA/Disc 2/01.mp3")]
    [InlineData("/music/WorkA/Vol2/01.mp3")]
    [InlineData("/music/WorkA/Volume 3/01.mp3")]
    [InlineData("/music/WorkA/disk4/01.mp3")]
    [InlineData("/music/WorkA/mp3/01.mp3")]
    [InlineData("/music/WorkA/wav/01.mp3")]
    [InlineData("/music/WorkA/flac/01.flac")]
    [InlineData("/music/WorkA/ogg/01.ogg")]
    [InlineData("/music/WorkA/opus/01.opus")]
    [InlineData("/music/WorkA/m4a/01.m4a")]
    public void Parts_of_a_work_resolve_to_the_work_folder(string file)
    {
        Assert.Equal("/music/WorkA", Cloud(file));
    }

    [Fact]
    public void A_track_directly_in_the_work_folder_resolves_to_it()
    {
        Assert.Equal("/music/WorkA", Cloud("/music/WorkA/01.mp3"));
    }

    [Fact]
    public void A_track_at_the_library_root_resolves_to_the_base()
    {
        Assert.Equal(Base, Cloud("/music/01.mp3"));
    }

    [Fact]
    public void A_product_id_folder_is_preferred()
    {
        Assert.Equal("/music/Some Title RJ123456", Cloud("/music/Some Title RJ123456/disc1/01.mp3"));
    }

    /// <summary>DLsite sub-folders repeat the code; taking the deepest one split a work into one album per format folder.</summary>
    [Theory]
    [InlineData("/music/RJ01234567 Title/RJ01234567_mp3/01.mp3")]
    [InlineData("/music/RJ01234567 Title/RJ01234567_wav/01.wav")]
    [InlineData("/music/RJ01234567 Title/RJ01234567_mp4/本編/01.mp4")]
    [InlineData("/music/RJ01234567 Title/MP4/本編/おまけ/01.mp4")]
    public void The_outermost_product_id_folder_is_the_work(string file)
    {
        Assert.Equal("/music/RJ01234567 Title", Cloud(file));
    }

    [Fact]
    public void A_product_id_folder_inside_a_circle_folder_is_the_work()
    {
        Assert.Equal("/music/Circle/RJ01234567", Cloud("/music/Circle/RJ01234567/mp3/01.mp3"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    public void A_library_at_the_server_root_still_finds_the_work(string basePath)
    {
        var result = WorkFolderResolver.Resolve("RJ01234567/RJ01234567_mp3/sub/01.mp3", basePath, isCloud: true);

        Assert.Equal("RJ01234567", result);
    }

    [Theory]
    [InlineData("/music/")]
    [InlineData("/music//")]
    public void A_trailing_separator_on_the_base_is_tolerated(string basePath)
    {
        var result = WorkFolderResolver.Resolve("/music/WorkA/CD1/01.mp3", basePath, isCloud: true);

        Assert.Equal("/music/WorkA", result);
    }

    [Fact]
    public void A_track_at_the_root_resolves_to_the_base_even_with_a_trailing_separator()
    {
        var result = WorkFolderResolver.Resolve("/music/01.mp3", "/music/", isCloud: true);

        Assert.Equal("/music", result);
    }

    /// <summary>The answer is used as a cache key, so it must keep the native separator.</summary>
    [Fact]
    public void Local_paths_keep_the_native_separator()
    {
        var separator = Path.DirectorySeparatorChar;
        var expected = $"{separator}music{separator}WorkA";

        var result = WorkFolderResolver.Resolve("/music/WorkA/CD1/01.mp3", "/music", isCloud: false);

        Assert.Equal(expected, result);
        Assert.DoesNotContain('/', result);
    }

    [Theory]
    [InlineData("CD1", true)]
    [InlineData("cd2", true)]
    [InlineData("Disc 2", true)]
    [InlineData("Disk-4", true)]
    [InlineData("vol_3", true)]
    [InlineData("bonus", true)]
    [InlineData("part1", true)]
    [InlineData("side B", false)] // "side" alone matches; "side B" is a different folder name, not a disc marker
    [InlineData("WorkA", false)]
    [InlineData("mp3", false)] // a format folder, checked separately
    public void Disc_folder_detection(string name, bool expected)
    {
        Assert.Equal(expected, WorkFolderResolver.IsDiscFolder(name));
    }

    [Theory]
    [InlineData("mp3", true)]
    [InlineData("WAV", true)]
    [InlineData("flac", true)]
    [InlineData("ogg", true)]
    [InlineData("opus", true)]
    [InlineData("m4a", true)]
    [InlineData("mp4", true)]
    [InlineData("aac", true)]
    [InlineData("CD1", false)]
    [InlineData("music", false)]
    public void Format_folder_detection(string name, bool expected)
    {
        Assert.Equal(expected, WorkFolderResolver.IsFormatFolder(name));
    }

    [Theory]
    [InlineData("disc")]
    [InlineData("cd")]
    [InlineData("vol")]
    [InlineData("volume")]
    [InlineData("disk")]
    [InlineData("bonus")]
    [InlineData("track")]
    [InlineData("side")]
    [InlineData("part")]
    [InlineData("effect")]
    [InlineData("se")]
    public void All_legacy_alternatives_are_still_recognised(string name)
    {
        Assert.True(WorkFolderResolver.IsDiscFolder(name), $"'{name}' must still be treated as a part folder");
    }
    /// <summary>
    /// Real pCloud layout: per-track folders ("01_…", "04_…") match no disc or format pattern, so each used to
    /// become an album of its own.
    /// </summary>
    [Theory]
    [InlineData("/lib/Voice Work/SFW/Other/RJ01510352/RJ01510352/mp3/01_1週間で好感度MAX/x.mp3")]
    [InlineData("/lib/Voice Work/SFW/Other/RJ01510352/RJ01510352/mp3/04_お風呂屋さん/y.mp3")]
    [InlineData("/lib/Voice Work/SFW/Other/RJ01510352/RJ01510352/mp3/deeper/still/z.mp3")]
    public void Arbitrary_subfolders_of_a_work_belong_to_the_work(string file)
    {
        var basePath = "/lib/Voice Work/SFW/Other";
        var resolved = WorkFolderResolver.Resolve(file, basePath, isCloud: true);

        // The work is the first folder under the library root, whichever folder carries the product id.
        Assert.Contains("RJ01510352", resolved);
        Assert.DoesNotContain("01_1週間", resolved);
        Assert.DoesNotContain("04_お風呂屋さん", resolved);
        Assert.DoesNotContain("/deeper", resolved);
    }

    [Fact]
    public void A_track_directly_in_the_work_folder_still_resolves_to_the_work()
    {
        var resolved = WorkFolderResolver.Resolve(
            "/lib/Voice Work/RJ01510352/mp3/02_track.mp3",
            "/lib/Voice Work",
            isCloud: true);

        Assert.Equal("/lib/Voice Work/RJ01510352", resolved);
    }
}
