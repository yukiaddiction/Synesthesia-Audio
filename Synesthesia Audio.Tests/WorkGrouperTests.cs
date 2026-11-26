using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// The grouping rules behind the library scan. They are pure, which is the point of moving them into Core: a
/// wrong answer here shows up as tracks in the wrong sub-album or the wrong copy of a duplicate kept, and that is
/// otherwise only visible by scanning a real library.
/// </summary>
public sealed class WorkGrouperTests
{
    [Theory]
    [InlineData(@"C:\lib\RJ123456\CD1", @"C:\lib\RJ123456", "CD1")]
    [InlineData(@"C:\lib\RJ123456\Disc 2", @"C:\lib\RJ123456", "Disc 2")]
    [InlineData("C:/lib/RJ123456/CD1/", "C:/lib/RJ123456", "CD1")]
    [InlineData("C:/lib/RJ123456/CD1/flac", "C:/lib/RJ123456", "CD1/flac")]
    public void A_file_inside_a_subfolder_belongs_to_that_subalbum(string fileDirectory, string workPath, string expected)
    {
        Assert.Equal(expected, WorkGrouper.GetSubAlbumName(fileDirectory, workPath));
    }

    [Theory]
    [InlineData(@"C:\lib\RJ123456", @"C:\lib\RJ123456")]
    [InlineData(@"C:\lib\RJ123456\", @"C:\lib\RJ123456")]
    [InlineData("", @"C:\lib\RJ123456")]
    public void A_file_directly_in_the_work_folder_has_no_subalbum(string fileDirectory, string workPath)
    {
        Assert.Null(WorkGrouper.GetSubAlbumName(fileDirectory, workPath));
    }

    /// <summary>
    /// A file outside the work folder must not be given a sub-album - it would attach a stranger's directory to
    /// this work.
    /// </summary>
    [Fact]
    public void A_directory_outside_the_work_folder_has_no_subalbum()
    {
        Assert.Null(WorkGrouper.GetSubAlbumName(@"D:\elsewhere\RJ999999\CD1", @"C:\lib\RJ123456"));
    }

    [Fact]
    public void The_best_copy_of_a_duplicate_is_chosen_by_format()
    {
        var copies = new[] { "track.mp3", "track.flac", "track.wav" };

        var best = copies
            .OrderBy(c => WorkGrouper.GetExtensionPriority(Path.GetExtension(c)))
            .First();

        Assert.Equal("track.flac", best);
    }

    [Theory]
    [InlineData(".FLAC", 1)]
    [InlineData(".wav", 2)]
    [InlineData(".mp3", 3)]
    [InlineData(".ogg", 99)]
    [InlineData(null, 99)]
    public void Format_priority_is_ordered_and_tolerant(string? extension, int expected)
    {
        Assert.Equal(expected, WorkGrouper.GetExtensionPriority(extension));
    }

    /// <summary>Remote paths are normalised to forward slashes, because that is what cloud listings use and
    /// what the grouping comparisons expect back.</summary>
    [Theory]
    [InlineData("C:/lib/RJ123456/a/b", "C:/lib/RJ123456/a")]
    [InlineData(@"C:\lib\RJ123456\CD1", "C:/lib/RJ123456")]
    [InlineData("justname", "")]
    public void Remote_directory_splits_on_either_separator(string path, string expected)
    {
        Assert.Equal(expected, WorkGrouper.GetRemoteDirectory(path));
    }

    [Theory]
    [InlineData("C:/lib/RJ123456/track.flac", "track.flac")]
    [InlineData(@"C:\lib\RJ123456\track.flac", "track.flac")]
    [InlineData("track.flac", "track.flac")]
    public void Remote_file_name_splits_on_either_separator(string path, string expected)
    {
        Assert.Equal(expected, WorkGrouper.GetRemoteFileName(path));
    }

    [Fact]
    public void A_track_title_drops_the_folder_and_the_extension()
    {
        Assert.Equal("01 First Track", WorkGrouper.GetTrackTitle(@"C:\lib\RJ123456\01 First Track.mp3"));
    }
}
