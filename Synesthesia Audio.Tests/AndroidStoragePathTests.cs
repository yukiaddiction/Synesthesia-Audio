using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// The Android folder picker returns a Storage Access Framework URI; stored unconverted, every later scan
/// reported the folder missing.
/// </summary>
public sealed class AndroidStoragePathTests
{
    [Fact]
    public void A_primary_tree_uri_becomes_a_storage_path()
    {
        var result = AndroidStoragePath.ToFileSystemPath("/tree/primary:Music/Synesthesia");

        Assert.Equal("/storage/emulated/0/Music/Synesthesia", result);
    }

    [Fact]
    public void The_repeated_document_id_is_stripped()
    {
        var result = AndroidStoragePath.ToFileSystemPath(
            "/tree/primary:Music/document/primary:Music");

        Assert.Equal("/storage/emulated/0/Music", result);
    }

    [Fact]
    public void A_percent_encoded_content_uri_is_decoded_first()
    {
        var result = AndroidStoragePath.ToFileSystemPath(
            "content://com.android.externalstorage.documents/tree/primary%3AMusic");

        Assert.Equal("/storage/emulated/0/Music", result);
    }

    [Fact]
    public void The_volume_root_maps_to_the_storage_root()
    {
        Assert.Equal("/storage/emulated/0", AndroidStoragePath.ToFileSystemPath("/tree/primary:"));
        Assert.Equal("/storage/emulated/0", AndroidStoragePath.ToFileSystemPath("/tree/primary:/"));
    }

    [Fact]
    public void A_trailing_separator_is_removed()
    {
        Assert.Equal("/storage/emulated/0/Music", AndroidStoragePath.ToFileSystemPath("/tree/primary:Music/"));
    }

    /// <summary>The helper runs on every platform, so anything it does not recognise must pass through untouched.</summary>
    [Theory]
    [InlineData(@"C:\Music\Synesthesia")]
    [InlineData("/home/user/Music")]
    [InlineData("/storage/1A2B-3C4D/Music")]
    [InlineData("/tree/1A2B-3C4D:Music")]
    [InlineData("relative/folder")]
    public void Other_paths_are_returned_unchanged(string input)
    {
        Assert.Equal(input, AndroidStoragePath.ToFileSystemPath(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_input_yields_empty(string? input)
    {
        Assert.Equal(string.Empty, AndroidStoragePath.ToFileSystemPath(input));
    }
}
