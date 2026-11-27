using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>The whole-tree listing, the subtree reuse a rescan relies on, and the loop guard.</summary>
public class ScanListingTreeTests
{
    private static bool IsAudio(string path) => path.EndsWith(".mp3") || path.EndsWith(".mp4");
    private static bool IsSystem(string path) => path.Replace('\\', '/').TrimEnd('/').EndsWith("__MACOSX");

    private sealed class FakeSource : ITreeScanFileSource
    {
        private readonly Dictionary<string, List<ScanEntry>> _folders = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<ScanEntry>? Tree { get; set; }
        public List<string> Listed { get; } = new();

        public FakeSource With(string folder, params ScanEntry[] entries)
        {
            _folders[folder] = entries.ToList();
            return this;
        }

        public Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            lock (Listed) Listed.Add(path);
            return Task.FromResult<IReadOnlyList<ScanEntry>>(
                _folders.TryGetValue(path, out var entries) ? entries : new List<ScanEntry>());
        }

        public Task<IReadOnlyList<ScanEntry>?> TryListTreeAsync(string rootPath, CancellationToken cancellationToken = default) =>
            Task.FromResult(Tree);
    }

    [Fact]
    public async Task A_whole_tree_answer_is_used_without_walking()
    {
        var source = new FakeSource
        {
            Tree = new List<ScanEntry>
            {
                new("music/RJ1", true, "e1"),
                new("music/RJ1/mp3", true),
                new("music/RJ1/mp3/01.mp3", false),
                new("music/RJ1/MP4/本編/01.mp4", false),
                new("music/RJ1/__MACOSX/01.mp3", false)
            }
        };

        var listing = await ScanListing.CollectAudioFilesAsync(source, "music", IsAudio, IsSystem);

        Assert.True(listing.UsedTreeListing);
        Assert.Empty(source.Listed);
        Assert.Equal(1, listing.ListRequests);
        Assert.Equal(new[] { "music/RJ1/mp3/01.mp3", "music/RJ1/MP4/本編/01.mp4" }, listing.AudioFiles);
        Assert.Equal("e1", listing.DirectoryETags["music/RJ1"]);
    }

    [Fact]
    public async Task A_refused_tree_falls_back_to_the_walk()
    {
        var source = new FakeSource()
            .With("music", new ScanEntry("music/RJ1", true))
            .With("music/RJ1", new ScanEntry("music/RJ1/01.mp3", false));

        var listing = await ScanListing.CollectAudioFilesAsync(source, "music", IsAudio);

        Assert.False(listing.UsedTreeListing);
        Assert.Equal(new[] { "music/RJ1/01.mp3" }, listing.AudioFiles);
    }

    [Fact]
    public async Task An_unchanged_folder_is_reused_instead_of_listed()
    {
        var source = new FakeSource()
            .With("music", new ScanEntry("music/RJ1", true, "same"), new ScanEntry("music/RJ2", true, "new"))
            .With("music/RJ2", new ScanEntry("music/RJ2/01.mp3", false));

        IReadOnlyList<string>? Reuse(ScanEntry entry) =>
            entry.Path == "music/RJ1" && entry.ETag == "same" ? new[] { "music/RJ1/mp3/01.mp3" } : null;

        var listing = await ScanListing.CollectAudioFilesAsync(source, "music", IsAudio, reuseSubtree: Reuse);

        Assert.DoesNotContain("music/RJ1", source.Listed);
        Assert.Equal(1, listing.ReusedFolders);
        Assert.Equal(new[] { "music/RJ1/mp3/01.mp3", "music/RJ2/01.mp3" }, listing.AudioFiles.OrderBy(f => f));
        Assert.Equal("same", listing.DirectoryETags["music/RJ1"]);
    }

    [Fact]
    public async Task A_folder_that_lists_itself_does_not_loop_the_walk()
    {
        var source = new FakeSource()
            .With("music", new ScanEntry("music/RJ1", true))
            .With("music/RJ1", new ScanEntry("music/RJ1", true), new ScanEntry("music/RJ1/01.mp3", false));

        var listing = await ScanListing.CollectAudioFilesAsync(source, "music", IsAudio);

        Assert.Equal(new[] { "music/RJ1/01.mp3" }, listing.AudioFiles);
        Assert.Equal(1, source.Listed.Count(p => p == "music/RJ1"));
    }
}
