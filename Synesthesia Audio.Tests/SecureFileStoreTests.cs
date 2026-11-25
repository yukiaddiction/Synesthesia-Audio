using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class SecureFileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), "sa-secure-tests", Guid.NewGuid().ToString("N") + ".json");

    // Base64 + a tag: enough of a transform that a plaintext leak would be visible on disk.
    private static Task<string> Protect(string value) =>
        Task.FromResult("enc:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value)));

    private static Task<string> Unprotect(string value) =>
        Task.FromResult(value.StartsWith("enc:", StringComparison.Ordinal)
            ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value[4..]))
            : value);

    private SecureFileStore CreateStore(
        Func<string, Task<string>>? protect = null,
        Func<string, Task<string>>? unprotect = null) =>
        new(_path, protect ?? Protect, unprotect ?? Unprotect);

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
        var temporary = _path + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
    }

    [Fact]
    public async Task SetThenGet_RoundTrips()
    {
        var store = CreateStore();

        await store.SetAsync("DeepL", "secret-key:fx");

        Assert.Equal("secret-key:fx", await store.GetAsync("DeepL"));
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var store = CreateStore();

        Assert.Null(await store.GetAsync("absent"));
    }

    [Fact]
    public async Task Set_WritesProtectedValue_NotPlaintext()
    {
        var store = CreateStore();

        await store.SetAsync("DeepL", "secret-key:fx");

        var onDisk = await File.ReadAllTextAsync(_path);
        Assert.DoesNotContain("secret-key", onDisk);
        Assert.Contains("enc:", onDisk);
    }

    [Fact]
    public async Task Values_SurviveANewStoreInstance()
    {
        await CreateStore().SetAsync("DeepL", "persisted:fx");

        // A fresh instance must read what the previous one wrote, which is what happens across app launches.
        Assert.Equal("persisted:fx", await CreateStore().GetAsync("DeepL"));
    }

    [Fact]
    public async Task CorruptFile_RecoversToEmptyInsteadOfThrowing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, "{ this is not json");

        var store = CreateStore();

        Assert.Null(await store.GetAsync("anything"));
        await store.SetAsync("DeepL", "recovered");
        Assert.Equal("recovered", await store.GetAsync("DeepL"));
    }

    [Fact]
    public async Task Remove_DeletesOnlyThatKey()
    {
        var store = CreateStore();
        await store.SetAsync("keep", "a");
        await store.SetAsync("drop", "b");

        await store.RemoveAsync("drop");

        Assert.Equal("a", await store.GetAsync("keep"));
        Assert.Null(await store.GetAsync("drop"));
    }

    [Fact]
    public async Task UndecryptableBlob_DropsKeyInsteadOfFailingForever()
    {
        var store = CreateStore();
        await store.SetAsync("DeepL", "secret");

        // Simulate a blob protected under another Windows user: the unprotector now always fails.
        var hostile = CreateStore(unprotect: _ => throw new InvalidOperationException("cannot decrypt"));

        Assert.Null(await hostile.GetAsync("DeepL"));
    }
}
