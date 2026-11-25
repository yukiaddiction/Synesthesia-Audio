using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// PAT-06: the storage-type values were bare string literals in a dozen places, including a <c>Contains</c>
/// test that matched anything containing the word. These are the comparisons the app now makes.
/// </summary>
public sealed class StorageTypesTests
{
    [Theory]
    [InlineData("WebDAV")]
    [InlineData("webdav")]
    [InlineData("  WebDAV  ")]
    public void Webdav_values_are_recognised(string value)
    {
        Assert.True(StorageTypes.IsWebDav(value));
        Assert.False(StorageTypes.IsLocal(value));
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("local")]
    public void Local_values_are_recognised(string value)
    {
        Assert.True(StorageTypes.IsLocal(value));
        Assert.False(StorageTypes.IsWebDav(value));
    }

    /// <summary>
    /// The regression the old <c>Contains("WebDAV")</c> allowed: a value that merely mentions WebDAV is not a
    /// WebDAV setting.
    /// </summary>
    [Theory]
    [InlineData("LocalWebDAV")]
    [InlineData("NotWebDAV")]
    [InlineData("WebDAV-typo")]
    public void A_value_that_merely_contains_the_word_is_not_webdav(string value)
    {
        Assert.False(StorageTypes.IsWebDav(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("something else")]
    public void Unknown_values_fall_back_to_local(string? value)
    {
        Assert.False(StorageTypes.IsWebDav(value));
        Assert.Equal(StorageTypes.Local, StorageTypes.Normalize(value));
    }

    [Fact]
    public void Webdav_normalises_to_its_canonical_spelling()
    {
        Assert.Equal(StorageTypes.WebDav, StorageTypes.Normalize("webdav"));
    }

    [Fact]
    public void The_persisted_values_are_unchanged_so_existing_settings_still_load()
    {
        // The settings file already contains these exact strings; changing them would silently reset every
        // user's storage choice.
        Assert.Equal("Local", StorageTypes.Local);
        Assert.Equal("WebDAV", StorageTypes.WebDav);
    }
}
