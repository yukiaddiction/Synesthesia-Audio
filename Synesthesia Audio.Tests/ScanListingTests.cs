using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// Tests for the library walk and the cache-merge decision. BUG-M-01 is the finding that a failed
/// listing could wipe the whole library cache, so these tests are written against a fake file source
/// that throws on demand — exactly the acceptance check the audit asks for.
/// </summary>
public sealed class ScanListingTests
{
    private sealed class FakeFileSource : IScanFileSource
    {
        public Dictionary<string, List<ScanEntry>> Directories { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ThrowFor { get; } = new(StringComparer.OrdinalIgnoreCase);

        public FakeFileSource WithDirectory(string path, params ScanEntry[] entries)
        {
            Directories[path] = entries.ToList();
            return this;
        }

        public Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            if (ThrowFor.Contains(path)) throw new IOException($"cannot list {path}");
            return Task.FromResult<IReadOnlyList<ScanEntry>>(
                Directories.TryGetValue(path, out var entries) ? entries : new List<ScanEntry>());
        }
    }

    private static bool IsAudio(string path) =>
        path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase);

    private static bool IsSystem(string path) =>
        path.Contains("__MACOSX", StringComparison.OrdinalIgnoreCase) ||
        System.IO.Path.GetFileName(path).StartsWith("._", StringComparison.Ordinal);

    /// <summary>BUG-M-01: a root listing failure must be reported as such, so the cache is left alone.</summary>
    [Fact]
    public async Task Root_failure_is_reported_and_forbids_replacing_the_cache()
    {
        var source = new FakeFileSource();
        source.ThrowFor.Add("/music");

        var listing = await ScanListing.CollectAudioFilesAsync(source, "/music", IsAudio, IsSystem);

        Assert.True(listing.RootFailed);
        Assert.Empty(listing.AudioFiles);
        Assert.False(LibraryCacheMergePolicy.CanReplaceWholeCache(listing));
    }

    [Fact]
    public async Task Complete_walk_allows_replacing_the_cache()
    {
        var source = new FakeFileSource()
            .WithDirectory("/music", new ScanEntry("/music/RJ000001", true))
            .WithDirectory("/music/RJ000001", new ScanEntry("/music/RJ000001/01.mp3", false));

        var listing = await ScanListing.CollectAudioFilesAsync(source, "/music", IsAudio, IsSystem);

        Assert.False(listing.RootFailed);
        Assert.True(listing.IsComplete);
        Assert.True(LibraryCacheMergePolicy.CanReplaceWholeCache(listing));
        Assert.Single(listing.AudioFiles);
    }

    /// <summary>
    /// BUG-M-01 done_when: when one subfolder cannot be listed, cached works under that folder must be
    /// retained instead of being dropped by the replacement list.
    /// </summary>
    [Fact]
    public async Task Works_under_a_failed_subfolder_are_retained()
    {
        var source = new FakeFileSource()
            .WithDirectory("/music",
                new ScanEntry("/music/RJ000001", true),
                new ScanEntry("/music/RJ000002", true),
                new ScanEntry("/music/RJ000003", true))
            .WithDirectory("/music/RJ000001", new ScanEntry("/music/RJ000001/01.mp3", false));
        source.ThrowFor.Add("/music/RJ000002");
        // RJ000003 lists fine but is empty: that work really is gone and should be dropped.

        var listing = await ScanListing.CollectAudioFilesAsync(source, "/music", IsAudio, IsSystem);

        Assert.False(listing.IsComplete);
        Assert.False(LibraryCacheMergePolicy.CanReplaceWholeCache(listing));

        var scanned = new[] { "/music/RJ000001" };
        var cached = new[] { "/music/RJ000001", "/music/RJ000002", "/music/RJ000003" };

        var merged = LibraryCacheMergePolicy.Merge(scanned, cached, listing.FailedPaths, path => path);

        Assert.Contains("/music/RJ000001", merged);
        Assert.Contains("/music/RJ000002", merged); // kept: its folder could not be read
        Assert.DoesNotContain("/music/RJ000003", merged); // dropped: listed successfully and is gone
    }

    [Fact]
    public void Merge_without_failures_is_just_the_scanned_list()
    {
        var merged = LibraryCacheMergePolicy.Merge(
            new[] { "/music/A" },
            new[] { "/music/old" },
            Array.Empty<string>(),
            path => path);

        Assert.Equal(new[] { "/music/A" }, merged);
    }

    [Fact]
    public async Task System_files_and_system_folders_are_skipped()
    {
        var source = new FakeFileSource()
            .WithDirectory("/music",
                new ScanEntry("/music/__MACOSX", true),
                new ScanEntry("/music/RJ000001", true),
                new ScanEntry("/music/._01.mp3", false))
            .WithDirectory("/music/RJ000001", new ScanEntry("/music/RJ000001/01.mp3", false));

        var listing = await ScanListing.CollectAudioFilesAsync(source, "/music", IsAudio, IsSystem);

        Assert.Single(listing.AudioFiles);
        Assert.Equal("/music/RJ000001/01.mp3", listing.AudioFiles[0]);
    }

    /// <summary>BUG-m-07: a trailing slash on the base path must not change or break the walk.</summary>
    [Fact]
    public async Task Trailing_slash_on_the_root_is_tolerated()
    {
        var source = new FakeFileSource()
            .WithDirectory("/music/", new ScanEntry("/music/RJ000001/01.mp3", false));

        var listing = await ScanListing.CollectAudioFilesAsync(source, "/music/", IsAudio, IsSystem);

        Assert.Single(listing.AudioFiles);
        Assert.Empty(listing.FailedPaths);
    }

    [Fact]
    public void Path_containment_handles_both_separators()
    {
        Assert.True(ScanListing.IsUnderOrEqual(@"C:\Music\A\01.mp3", @"C:\Music\A"));
        Assert.True(ScanListing.IsUnderOrEqual("C:/Music/A/01.mp3", @"C:\Music\A"));
        Assert.True(ScanListing.IsUnderOrEqual(@"C:\Music\A", @"C:\Music\A"));
        Assert.False(ScanListing.IsUnderOrEqual(@"C:\Music\AB\01.mp3", @"C:\Music\A"));
    }
}
