using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class WebDavMultistatusTests
{
    private const string BaseUrl = "https://cloud.example/remote.php/dav/files/me/";

    private static string Response(string href, bool collection, string? etag = null, string? fileId = null,
        bool splitNotFound = false)
    {
        var props =
            $"<d:resourcetype>{(collection ? "<d:collection/>" : "")}</d:resourcetype>" +
            (etag != null ? $"<d:getetag>{etag}</d:getetag>" : "") +
            (fileId != null ? $"<oc:fileid>{fileId}</oc:fileid>" : "");

        var notFound = splitNotFound
            ? "<d:propstat><d:prop><oc:fileid/></d:prop><d:status>HTTP/1.1 404 Not Found</d:status></d:propstat>"
            : "";

        // The 404 block comes first on purpose: the old parser read only the first propstat.
        return $"<d:response><d:href>{href}</d:href>{notFound}" +
               $"<d:propstat><d:prop>{props}</d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response>";
    }

    private static string Multistatus(params string[] responses) =>
        "<?xml version=\"1.0\"?><d:multistatus xmlns:d=\"DAV:\" xmlns:oc=\"http://owncloud.org/ns\">" +
        string.Concat(responses) + "</d:multistatus>";

    [Fact]
    public void A_hash_in_a_folder_name_is_kept_as_part_of_the_path()
    {
        var xml = Multistatus(
            Response("/remote.php/dav/files/me/RJ01234567/", true),
            Response("/remote.php/dav/files/me/RJ01234567/%231%20%E6%9C%AC%E7%B7%A8/", true));

        var listing = WebDavMultistatus.Parse(xml, BaseUrl, "RJ01234567");

        var entry = Assert.Single(listing.Entries);
        Assert.Equal("RJ01234567/#1 本編", entry.Path);
        Assert.True(entry.IsDirectory);
    }

    [Fact]
    public void The_requested_folder_is_not_listed_as_its_own_child()
    {
        var xml = Multistatus(
            Response("/remote.php/dav/files/me/Audio/", true),
            Response("/remote.php/dav/files/me/Audio/RJ01234567/", true));

        var listing = WebDavMultistatus.Parse(xml, BaseUrl, "Audio/");

        Assert.Equal(new[] { "Audio/RJ01234567" }, listing.Entries.Select(e => e.Path));
    }

    [Fact]
    public void A_decomposed_name_still_matches_the_requested_folder()
    {
        var decomposed = "ボイス".Normalize(System.Text.NormalizationForm.FormD);
        var xml = Multistatus(
            Response("/remote.php/dav/files/me/" + Uri.EscapeDataString(decomposed) + "/", true),
            Response("/remote.php/dav/files/me/" + Uri.EscapeDataString(decomposed) + "/01.mp3", false));

        var listing = WebDavMultistatus.Parse(xml, BaseUrl, "ボイス");

        Assert.Single(listing.Entries);
    }

    [Fact]
    public void A_not_found_propstat_does_not_hide_the_resource_type()
    {
        var xml = Multistatus(
            Response("/remote.php/dav/files/me/Audio/", true),
            Response("/remote.php/dav/files/me/Audio/RJ01234567/", true, etag: "\"abc\"", splitNotFound: true));

        var entry = Assert.Single(WebDavMultistatus.Parse(xml, BaseUrl, "Audio").Entries);

        Assert.True(entry.IsDirectory);
        Assert.Equal("\"abc\"", entry.ETag);
    }

    [Fact]
    public void An_owncloud_file_id_marks_the_server_as_propagating_etags()
    {
        var withId = Multistatus(Response("/remote.php/dav/files/me/Audio/RJ1/", true, fileId: "42"));
        var withoutId = Multistatus(Response("/remote.php/dav/files/me/Audio/RJ1/", true));

        Assert.True(WebDavMultistatus.Parse(withId, BaseUrl, "Audio").HasOwnCloudFileIds);
        Assert.False(WebDavMultistatus.Parse(withoutId, BaseUrl, "Audio").HasOwnCloudFileIds);
    }

    [Fact]
    public void An_href_outside_the_base_is_dropped()
    {
        var xml = Multistatus(Response("/somewhere/else/RJ1/", true));

        Assert.Empty(WebDavMultistatus.Parse(xml, BaseUrl, "").Entries);
    }

    [Fact]
    public void A_one_level_answer_to_an_infinite_request_looks_shallow()
    {
        var oneLevel = new List<WebDavEntry>
        {
            new("Audio/RJ1", "RJ1", true, 0, null),
            new("Audio/RJ2", "RJ2", true, 0, null)
        };
        var deep = new List<WebDavEntry>(oneLevel) { new("Audio/RJ1/01.mp3", "01.mp3", false, 1, null) };

        Assert.True(WebDavMultistatus.LooksShallow(oneLevel, "Audio"));
        Assert.False(WebDavMultistatus.LooksShallow(deep, "Audio"));
    }
}
