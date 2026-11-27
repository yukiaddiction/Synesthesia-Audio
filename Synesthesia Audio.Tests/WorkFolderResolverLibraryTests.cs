using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Mirrors a real pCloud library (Voice Work/{SFW,NSFW}/[Other/]RJ…/RJ…/…). The picker stores "/Voice Work" while the
/// server lists "Voice Work/…", and that mismatch made every leaf folder (本編, おまけ, 02_mp3版) a work of its own.
/// </summary>
public sealed class WorkFolderResolverLibraryTests
{
    private static readonly string[] Library =
    {
        "Voice Work/NSFW/RJ01095363/RJ01095363/本編/01.mp3",
        "Voice Work/NSFW/RJ01095363/RJ01095363/おまけ/01.mp3",
        "Voice Work/NSFW/RJ01095363/RJ01095363/02_mp3版/01.mp3",
        "Voice Work/SFW/Other/RJ01510352/RJ01510352/mp3/01_1週間/x.mp3",
        "Voice Work/SFW/Other/RJ01510352/RJ01510352/mp3/02_track.mp3",
        "Voice Work/SFW/Kitakoji_Hisui_June_Bride_2026_Voice/01.mp3",
        "Voice Work/SFW/Lain_Paterson_June_Bride_2026_Voice/01.mp3",
        "Voice Work/SFW/Other/Booth Work/本編/01_本編SEあり/01.mp3",
        "Voice Work/SFW/Other/Booth Work/おまけ/01.mp3",
        "Voice Work/NSFW/Fanbox Work/恋をしたあなたへ。/01.mp3",
        "Voice Work/NSFW/Fanbox Work/シチュエーションボイス/01.mp3"
    };

    [Theory]
    [InlineData("/Voice Work")]
    [InlineData("Voice Work")]
    [InlineData("/Voice Work/")]
    public void Each_work_is_one_album_whatever_the_base_spelling(string basePath)
    {
        var works = WorkFolderResolver.ResolveAll(Library, basePath, isCloud: true);

        Assert.Equal(new[]
        {
            "Voice Work/NSFW/Fanbox Work",
            "Voice Work/NSFW/RJ01095363",
            "Voice Work/SFW/Kitakoji_Hisui_June_Bride_2026_Voice",
            "Voice Work/SFW/Lain_Paterson_June_Bride_2026_Voice",
            "Voice Work/SFW/Other/Booth Work",
            "Voice Work/SFW/Other/RJ01510352"
        }, works.Values.Distinct().OrderBy(w => w, StringComparer.Ordinal));
    }

    [Fact]
    public void A_category_named_like_one_is_never_a_work_even_without_product_ids_in_it()
    {
        var files = new[] { "Voice Work/SFW/Some Work/本編/01.mp3", "Voice Work/SFW/Other Work/01.mp3" };

        var works = WorkFolderResolver.ResolveAll(files, "/Voice Work", isCloud: true);

        Assert.Equal("Voice Work/SFW/Some Work", works[files[0]]);
        Assert.Equal("Voice Work/SFW/Other Work", works[files[1]]);
    }

    [Fact]
    public void Format_folders_carrying_the_code_under_a_titled_folder_are_one_work()
    {
        var files = new[]
        {
            "Voice Work/NSFW/Some Title/RJ01234567_mp3/本編/01.mp3",
            "Voice Work/NSFW/Some Title/RJ01234567_wav/本編/01.wav",
            "Voice Work/NSFW/Some Title/RJ01234567_mp4/01.mp4",
            "Voice Work/NSFW/RJ07654321/01.mp3"
        };

        var works = WorkFolderResolver.ResolveAll(files, "/Voice Work", isCloud: true);

        Assert.Equal(new[] { "Voice Work/NSFW/RJ07654321", "Voice Work/NSFW/Some Title" },
            works.Values.Distinct().OrderBy(w => w, StringComparer.Ordinal));
    }

    [Fact]
    public void Two_different_works_in_one_circle_folder_stay_apart()
    {
        var files = new[] { "Voice Work/NSFW/Circle/RJ01111111/01.mp3", "Voice Work/NSFW/Circle/RJ02222222/01.mp3" };

        var works = WorkFolderResolver.ResolveAll(files, "/Voice Work", isCloud: true);

        Assert.Equal("Voice Work/NSFW/Circle/RJ01111111", works[files[0]]);
        Assert.Equal("Voice Work/NSFW/Circle/RJ02222222", works[files[1]]);
    }

    [Fact]
    public void A_single_file_resolves_like_before()
    {
        Assert.Equal("Voice Work/NSFW/RJ01095363",
            WorkFolderResolver.Resolve("Voice Work/NSFW/RJ01095363/RJ01095363/本編/01.mp3", "/Voice Work", isCloud: true));
    }
}

public sealed class CacheMergeCoverageTests
{
    [Fact]
    public void A_cached_sub_folder_of_a_scanned_work_is_not_kept_as_a_second_work()
    {
        var scanned = new[] { "Voice Work/NSFW/RJ01095363" };
        var cached = new[] { "Voice Work/NSFW/RJ01095363/RJ01095363/本編", "Voice Work/NSFW/RJ01058580" };
        var failed = new[] { "Voice Work/NSFW/RJ01095363/RJ01095363", "Voice Work/NSFW/RJ01058580/RJ01058580" };

        var merged = LibraryCacheMergePolicy.Merge(scanned, cached, failed, p => p);

        Assert.Equal(new[] { "Voice Work/NSFW/RJ01095363", "Voice Work/NSFW/RJ01058580" }, merged);
    }
}
