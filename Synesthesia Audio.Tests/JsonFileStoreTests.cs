using System.Text.Json;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// A write made during a load must survive it, and concurrent writers must never corrupt the file. A path-keyed
/// dictionary mirrors what the library cache actually persists.
/// </summary>
public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public JsonFileStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "syna-jsonstore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "store.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; the temp directory is disposable by design.
        }
    }

    private static Dictionary<string, string> Document(params (string Key, string Value)[] entries)
    {
        var document = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries) document[key] = value;
        return document;
    }

    private void WriteRawFile(string json) => File.WriteAllText(_path, json);

    [Fact]
    public async Task Write_before_load_completes_survives_the_load()
    {
        WriteRawFile(JsonSerializer.Serialize(Document(("persisted", "yes"))));

        var store = new JsonFileStore<Dictionary<string, string>>(_path);

        // Deliberately not awaiting Loaded: the startup race where the cache takes a write mid-read.
        store.Update(document =>
        {
            document["written-early"] = "kept";
            return document;
        });

        var loaded = await store.Loaded;

        Assert.Equal("yes", loaded["persisted"]);
        Assert.Equal("kept", loaded["written-early"]);
    }

    [Fact]
    public async Task Loaded_exposes_the_persisted_document()
    {
        WriteRawFile(JsonSerializer.Serialize(Document(("work-a", "one"), ("work-b", "two"))));

        var store = new JsonFileStore<Dictionary<string, string>>(_path);
        var loaded = await store.Loaded;

        Assert.Equal(2, loaded.Count);
        Assert.Equal("two", loaded["work-b"]);
    }

    [Fact]
    public async Task Missing_file_yields_an_empty_document()
    {
        var store = new JsonFileStore<Dictionary<string, string>>(_path);
        var loaded = await store.Loaded;

        Assert.Empty(loaded);
        Assert.False(File.Exists(_path));
    }

    [Fact]
    public async Task FlushAsync_persists_a_change_from_a_fresh_store()
    {
        WriteRawFile(JsonSerializer.Serialize(Document(("existing", "value"))));

        var store = new JsonFileStore<Dictionary<string, string>>(_path);
        await store.Loaded;
        store.Update(document =>
        {
            document["added"] = "here";
            return document;
        });

        await store.FlushAsync();

        using var reloaded = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal("value", reloaded.RootElement.GetProperty("existing").GetString());
        Assert.Equal("here", reloaded.RootElement.GetProperty("added").GetString());
    }

    [Fact]
    public async Task Concurrent_updates_produce_a_valid_document()
    {
        // Mirrors the up-to-four parallel DLsite tasks that write tag mappings at once.
        var store = new JsonFileStore<Dictionary<string, string>>(_path, saveDebounceMs: 0);
        await store.Loaded;

        var writers = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
        {
            for (var i = 0; i < 25; i++)
            {
                store.Update(document =>
                {
                    document[$"w{worker}-{i}"] = "x";
                    return document;
                });
            }
        }));

        await Task.WhenAll(writers);
        await store.FlushAsync();

        var json = File.ReadAllText(_path);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        Assert.NotNull(parsed);
        Assert.Equal(100, parsed!.Count);
    }

    /// <summary>The backup keeps learned mappings recoverable.</summary>
    [Fact]
    public async Task Corrupt_file_is_backed_up_not_silently_replaced()
    {
        const string corrupt = "{ this is not json";
        WriteRawFile(corrupt);

        Exception? reported = null;
        var store = new JsonFileStore<Dictionary<string, string>>(_path);
        store.LoadFailed += ex => reported = ex;

        var loaded = await store.Loaded;

        Assert.Empty(loaded);
        Assert.NotNull(reported);

        var backup = _path + ".bak";
        Assert.True(File.Exists(backup), "the unreadable document must be kept aside");
        Assert.Equal(corrupt, File.ReadAllText(backup));
    }

    [Fact]
    public async Task Replace_before_load_is_authoritative()
    {
        WriteRawFile(JsonSerializer.Serialize(Document(("stale", "value"))));

        var store = new JsonFileStore<Dictionary<string, string>>(_path);
        store.Replace(Document(("fresh", "value")));

        var loaded = await store.Loaded;

        Assert.False(loaded.ContainsKey("stale"));
        Assert.Equal("value", loaded["fresh"]);
    }

    [Fact]
    public async Task Save_leaves_no_temporary_file_behind()
    {
        var store = new JsonFileStore<Dictionary<string, string>>(_path);
        await store.Loaded;
        store.Update(document =>
        {
            document["k"] = "v";
            return document;
        });

        await store.FlushAsync();

        Assert.True(File.Exists(_path));
        Assert.False(File.Exists(_path + ".tmp"));
    }

    [Fact]
    public async Task OnBeforeSave_transform_is_persisted_but_not_applied_to_the_live_document()
    {
        // SettingsService relies on this to strip secrets before writing while keeping them in memory.
        var store = new JsonFileStore<Dictionary<string, string>>(
            _path,
            onBeforeSave: document => new Dictionary<string, string>(document) { ["secret"] = string.Empty });
        await store.Loaded;

        store.Update(document =>
        {
            document["secret"] = "value-in-memory";
            return document;
        });

        await store.FlushAsync();

        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));
        Assert.Equal(string.Empty, persisted!["secret"]);
        Assert.Equal("value-in-memory", store.Read(document => document["secret"]));
    }

    [Fact]
    public async Task DisposeAsync_flushes_a_pending_change()
    {
        var store = new JsonFileStore<Dictionary<string, string>>(_path, saveDebounceMs: 60_000);
        await store.Loaded;
        store.Update(document =>
        {
            document["pending"] = "value";
            return document;
        });

        await store.DisposeAsync();

        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));
        Assert.Equal("value", persisted!["pending"]);
    }
}
