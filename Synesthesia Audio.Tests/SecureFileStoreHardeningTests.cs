using System.Security.Cryptography;
using System.Text.Json;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-21: a credential must never be silently dropped. The store used to delete an entry on *any*
/// decryption failure, and it started from an empty dictionary when the file could not be parsed — both
/// paths destroyed a stored password without telling anyone.
/// </summary>
public sealed class SecureFileStoreHardeningTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public SecureFileStoreHardeningTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "syna-securestore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "secure.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch
        {
            // Best-effort cleanup of the temp directory.
        }
    }

    /// <summary>Stands in for DPAPI: prefix on write, strip on read. Unreadable values simulate corruption.</summary>
    private static Task<string> Protect(string value) => Task.FromResult("enc:" + value);

    private static Task<string> Unprotect(string value)
    {
        if (!value.StartsWith("enc:", StringComparison.Ordinal))
            throw new CryptographicException("blob cannot be decrypted under this user");
        return Task.FromResult(value[4..]);
    }

    private SecureFileStore CreateStore(Func<string, Task<string>>? unprotect = null) =>
        new(_path, Protect, unprotect ?? Unprotect);

    [Fact]
    public async Task A_stored_credential_round_trips()
    {
        var store = CreateStore();
        await store.SetAsync("CloudPassword", "hunter2");

        Assert.Equal("hunter2", await CreateStore().GetAsync("CloudPassword"));
    }

    /// <summary>An undecryptable blob is the one case where dropping the entry is correct.</summary>
    [Fact]
    public async Task A_cryptographically_unreadable_entry_is_dropped()
    {
        var store = CreateStore();
        await store.SetAsync("CloudPassword", "hunter2");

        var otherUser = CreateStore(unprotect: _ => throw new CryptographicException("wrong user"));
        Assert.Null(await otherUser.GetAsync("CloudPassword"));

        // The entry is gone from the file, not merely unreadable in memory.
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));
        Assert.False(entries!.ContainsKey("CloudPassword"));
    }

    /// <summary>
    /// A transient failure must not cost the user their password: the entry has to survive, and a later
    /// read has to succeed.
    /// </summary>
    [Fact]
    public async Task A_transient_failure_does_not_destroy_the_credential()
    {
        var store = CreateStore();
        await store.SetAsync("CloudPassword", "hunter2");

        var flaky = CreateStore(unprotect: _ => throw new IOException("keychain busy"));
        await Assert.ThrowsAsync<IOException>(() => flaky.GetAsync("CloudPassword"));

        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path));
        Assert.True(entries!.ContainsKey("CloudPassword"), "a transient read failure must not delete the password");

        Assert.Equal("hunter2", await CreateStore().GetAsync("CloudPassword"));
    }

    /// <summary>A corrupt container is kept aside so the encrypted blobs remain recoverable.</summary>
    [Fact]
    public async Task A_corrupt_container_is_backed_up_not_discarded()
    {
        const string corrupt = "{ not json";
        File.WriteAllText(_path, corrupt);

        var store = CreateStore();
        Assert.Null(await store.GetAsync("CloudPassword"));

        var backup = _path + ".bak";
        Assert.True(File.Exists(backup), "the corrupt container must be kept aside");
        Assert.Equal(corrupt, File.ReadAllText(backup));
    }

    [Fact]
    public async Task Removing_an_entry_deletes_only_that_entry()
    {
        var store = CreateStore();
        await store.SetAsync("A", "1");
        await store.SetAsync("B", "2");

        await store.RemoveAsync("A");

        var reloaded = CreateStore();
        Assert.Null(await reloaded.GetAsync("A"));
        Assert.Equal("2", await reloaded.GetAsync("B"));
    }
}
