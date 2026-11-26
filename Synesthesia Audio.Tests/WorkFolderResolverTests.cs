using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-03 / BUG-m-07: which folder counts as "the work".
///
/// A work split across disc folders (CD1, CD2, Disc 2, Vol3) or format folders (mp3, wav, flac) must
/// resolve to the one folder above them, otherwise the library shows a two-disc album as two works with
/// half its tracks each. A trailing separator in the configured base path must also be harmless.
/// </summary>
public sealed class WorkFolderResolverTests
{
    private const string Base = "/music";

    private static string Cloud(string file) => WorkFolderResolver.Resolve(file, Base, isCloud: true);

    /// <summary>BUG-m-03 done_when: each of these resolves to the work folder itself.</summary>
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

    /// <summary>The deepest product-id folder wins, so "RJ123456 disc1" style names land on the work.</summary>
    [Fact]
    public void A_product_id_folder_is_preferred()
    {
        Assert.Equal("/music/Some Title RJ123456", Cloud("/music/Some Title RJ123456/disc1/01.mp3"));
    }

    /// <summary>BUG-m-07: a trailing separator on the base path must not change the answer or throw.</summary>
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

    /// <summary>Windows paths keep backslashes, because the answer is used as a cache key.</summary>
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

    /// <summary>Every alternative the old per-branch regexes carried must still be recognised.</summary>
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
    /// Real library layout, seen on a pCloud library: the work lives under .../RJ01510352/RJ01510352/mp3,
    /// and inside it some tracks are loose files while others sit in per-track folders named "01_…", "04_…".
    /// Those folder names match no disc or format pattern, so they used to resolve to works of their own -
    /// which is why such a work appeared as several albums of one folder each.
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
