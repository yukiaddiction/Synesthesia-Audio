using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

/// <summary>
/// BUG-m-18: the Android folder picker returns a Storage Access Framework URI, not a filesystem path. Only
/// one of the two pickers converted it, so a folder chosen in Settings was stored as a raw tree URI and every
/// later scan reported it missing.
/// </summary>
public sealed class AndroidStoragePathTests
{
    [Fact]
    public void A_primary_tree_uri_becomes_a_storage_path()
    {
        var result = AndroidStoragePath.ToFileSystemPath("/tree/primary:Music/Synesthesia");

        Assert.Equal("/storage/emulated/0/Music/Synesthesia", result);
    }

    /// <summary>The document id repeats the tree id; only the relative folder should survive.</summary>
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
        // content://com.android.externalstorage.documents/tree/primary%3AMusic
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

    /// <summary>
    /// Desktop paths, and any tree URI for a volume this mapping does not know, must pass through untouched —
    /// the helper is called on every platform.
    /// </summary>
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
