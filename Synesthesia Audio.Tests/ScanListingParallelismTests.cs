using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// PERF-02: the folder walk listed one folder at a time, so a WebDAV library spent most of a scan waiting on a
/// round trip per folder. It now lists a bounded batch at a time — bounded, so a cloud server is not flooded.
/// </summary>
public sealed class ScanListingParallelismTests
{
    /// <summary>Records how many listings are in flight at once, and can be made slow.</summary>
    private sealed class TrackingFileSource : IScanFileSource
    {
        private readonly Dictionary<string, List<ScanEntry>> _directories = new(StringComparer.OrdinalIgnoreCase);
        private int _inFlight;

        public int MaxObservedInFlight { get; private set; }
        public int ListCalls { get; private set; }
        public TimeSpan Delay { get; init; } = TimeSpan.Zero;

        public TrackingFileSource With(string path, params ScanEntry[] entries)
        {
            _directories[path] = entries.ToList();
            return this;
        }

        public async Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref _inFlight);
            lock (_directories)
            {
                ListCalls++;
                MaxObservedInFlight = Math.Max(MaxObservedInFlight, current);
            }

            try
            {
                if (Delay > TimeSpan.Zero) await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
                else await Task.Yield();

                return _directories.TryGetValue(path, out var entries)
                    ? entries
                    : Array.Empty<ScanEntry>();
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
    }

    private static bool IsAudio(string path) => path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task Folders_are_listed_in_parallel()
    {
        var source = new TrackingFileSource { Delay = TimeSpan.FromMilliseconds(30) }
            .With("/music",
                new ScanEntry("/music/a", true),
                new ScanEntry("/music/b", true),
                new ScanEntry("/music/c", true),
                new ScanEntry("/music/d", true))
            .With("/music/a", new ScanEntry("/music/a/01.mp3", false))
            .With("/music/b", new ScanEntry("/music/b/01.mp3", false))
            .With("/music/c", new ScanEntry("/music/c/01.mp3", false))
            .With("/music/d", new ScanEntry("/music/d/01.mp3", false));

        var listing = await ScanListing.CollectAudioFilesAsync(source, "/music", IsAudio);

        Assert.Equal(4, listing.AudioFiles.Count);
        Assert.True(source.MaxObservedInFlight > 1,
            $"expected concurrent listings, observed at most {source.MaxObservedInFlight}");
    }

    /// <summary>The cap matters: a cloud server should not receive an unbounded burst of PROPFINDs.</summary>
    [Fact]
    public async Task Parallelism_is_bounded_by_the_cap()
    {
        var source = new TrackingFileSource { Delay = TimeSpan.FromMilliseconds(30) };
        var rootEntries = Enumerable.Range(0, 12)
            .Select(i => new ScanEntry($"/music/d{i}", true))
            .ToArray();
        source.With("/music", rootEntries);

        await ScanListing.CollectAudioFilesAsync(source, "/music", IsAudio, maxParallelism: 3);

        Assert.True(source.MaxObservedInFlight <= 3,
            $"observed {source.MaxObservedInFlight} concurrent listings with a cap of 3");
        Assert.True(source.MaxObservedInFlight > 1, "the walk should still overlap work");
    }

    [Fact]
    public async Task A_cap_of_one_still_walks_everything()
    {
        var source = new TrackingFileSource()
            .With("/music", new ScanEntry("/music/a", true), new ScanEntry("/music/01.mp3", false))
            .With("/music/a", new ScanEntry("/music/a/02.mp3", false));

        var listing = await ScanListing.CollectAudioFilesAsync(source, "/music", IsAudio, maxParallelism: 1);

        Assert.Equal(2, listing.AudioFiles.Count);
        Assert.Equal(1, source.MaxObservedInFlight);
    }

    /// <summary>Batching must not lose the failure bookkeeping that BUG-M-01 depends on.</summary>
    [Fact]
    public async Task A_failed_folder_is_still_recorded_in_a_parallel_batch()
    {
        var source = new TrackingFileSource()
            .With("/music",
                new ScanEntry("/music/ok", true),
                new ScanEntry("/music/broken", true))
            .With("/music/ok", new ScanEntry("/music/ok/01.mp3", false));

        var failing = new ThrowingSource(source, "/music/broken");
        var listing = await ScanListing.CollectAudioFilesAsync(failing, "/music", IsAudio);

        Assert.Contains("/music/broken", listing.FailedPaths);
        Assert.False(listing.RootFailed);
        Assert.Single(listing.AudioFiles);
    }

    [Fact]
    public async Task A_failing_root_is_still_reported_as_a_root_failure()
    {
        var failing = new ThrowingSource(new TrackingFileSource(), "/music");

        var listing = await ScanListing.CollectAudioFilesAsync(failing, "/music", IsAudio);

        Assert.True(listing.RootFailed);
        Assert.False(LibraryCacheMergePolicy.CanReplaceWholeCache(listing));
    }

    private sealed class ThrowingSource : IScanFileSource
    {
        private readonly IScanFileSource _inner;
        private readonly string _failingPath;

        public ThrowingSource(IScanFileSource inner, string failingPath)
        {
            _inner = inner;
            _failingPath = failingPath;
        }

        public Task<IReadOnlyList<ScanEntry>> ListAsync(string path, CancellationToken cancellationToken = default) =>
            string.Equals(path, _failingPath, StringComparison.OrdinalIgnoreCase)
                ? throw new IOException($"cannot list {path}")
                : _inner.ListAsync(path, cancellationToken);
    }
}
