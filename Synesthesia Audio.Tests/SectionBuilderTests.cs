using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Reproduces the device's cloud layout without the device. Keying sections off one leaf name used to hide
/// sibling folders.
/// </summary>
public sealed class SectionBuilderTests
{
    private const string Base = "/lib/Voice Work/SFW/Other";
    private const string Work = Base + "/RJ01510352/RJ01510352/mp3";

    [Fact]
    public void Files_in_the_work_folder_and_in_subfolders_become_separate_sections()
    {
        var files = new[]
        {
            Work + "/02_両耳癒し猫の溺愛おねだり.mp3",
            Work + "/03_ママ系ナースの極甘両耳診察.mp3",
            Work + "/01_1週間で好感度MAX/track.mp3",
            Work + "/04_お風呂屋さんで聞きAS/track.mp3"
        };

        var sections = SectionBuilder.Build(files, Work);

        Assert.Equal(3, sections.Count);
        Assert.True(sections[0].IsMain);
        Assert.Equal("Main folder", sections[0].Title);
        Assert.Equal(2, sections[0].Files.Count);
        Assert.Equal("01_1週間で好感度MAX", sections[1].Title);
        Assert.Equal("04_お風呂屋さんで聞きAS", sections[2].Title);
    }

    [Fact]
    public void Sibling_folders_are_both_kept()
    {
        var files = new[]
        {
            Work + "/01_first/a.mp3",
            Work + "/02_second/b.mp3"
        };

        var sections = SectionBuilder.Build(files, Work);

        Assert.Equal(2, sections.Count);
        Assert.All(sections, s => Assert.False(s.IsMain));
        Assert.Equal(new[] { "01_first", "02_second" }, sections.Select(s => s.Title));
    }

    [Fact]
    public void Nested_folders_keep_their_relative_path()
    {
        var files = new[]
        {
            Work + "/disc1/inner/a.mp3",
            Work + "/disc2/b.mp3"
        };

        var sections = SectionBuilder.Build(files, Work);

        Assert.Equal(new[] { "disc1/inner", "disc2" }, sections.Select(s => s.Title));
    }

    [Fact]
    public void A_work_with_only_subfolders_has_no_main_section()
    {
        var files = new[] { Work + "/CD1/a.flac", Work + "/CD2/b.flac" };

        var sections = SectionBuilder.Build(files, Work);

        Assert.Equal(2, sections.Count);
        Assert.DoesNotContain(sections, s => s.IsMain);
    }

    [Theory]
    [InlineData("/lib/Work/a.mp3")]
    [InlineData(@"C:\lib\Work\a.mp3")]
    public void A_flat_work_has_only_the_main_section(string file)
    {
        var work = file.Replace('\\', '/');
        work = work[..work.LastIndexOf('/')];

        var sections = SectionBuilder.Build(new[] { file }, work);

        Assert.Single(sections);
        Assert.True(sections[0].IsMain);
        Assert.Equal("Main folder", sections[0].Title);
    }

    [Fact]
    public void Local_backslash_paths_group_the_same_way()
    {
        var files = new[]
        {
            @"C:\lib\Work\CD1\a.flac",
            @"C:\lib\Work\CD2\b.flac",
            @"C:\lib\Work\c.mp3"
        };

        var sections = SectionBuilder.Build(files, @"C:\lib\Work");

        Assert.Equal(3, sections.Count);
        Assert.True(sections[0].IsMain);
        Assert.Equal(new[] { "Main folder", "CD1", "CD2" }, sections.Select(s => s.Title));
    }
}
