using System.Text;
using System.Xml.Linq;

namespace Synesthesia_Audio.Core;

/// <summary>One resource from a PROPFIND answer, with its path relative to the configured base URL.</summary>
public sealed record WebDavEntry(string Path, string Name, bool IsDirectory, long Size, string? ETag);

/// <summary>What a PROPFIND answer held.</summary>
public sealed class WebDavListing
{
    public List<WebDavEntry> Entries { get; } = new();

    /// <summary>
    /// True when the server answered the ownCloud <c>fileid</c> property. Nextcloud, ownCloud and oCIS do, and they
    /// are the servers that change a folder's etag whenever anything beneath it changes, which is what lets a rescan
    /// skip an unchanged work without listing it.
    /// </summary>
    public bool HasOwnCloudFileIds { get; set; }
}

/// <summary>
/// Builds and reads WebDAV PROPFIND messages. Lives in Core so the parsing - which decides what the scan sees - is
/// tested without a server.
/// </summary>
public static class WebDavMultistatus
{
    public const string OwnCloudNamespace = "http://owncloud.org/ns";

    /// <summary>
    /// The properties asked for. <c>oc:fileid</c> is harmless on other servers: an unknown property is reported as
    /// not found in its own propstat block.
    /// </summary>
    public static string RequestBody =>
        "<?xml version=\"1.0\" encoding=\"utf-8\" ?>" +
        "<d:propfind xmlns:d=\"DAV:\" xmlns:oc=\"" + OwnCloudNamespace + "\"><d:prop>" +
        "<d:displayname/><d:resourcetype/><d:getcontentlength/><d:getetag/><d:getlastmodified/><oc:fileid/>" +
        "</d:prop></d:propfind>";

    /// <summary>
    /// Parses a multistatus answer. <paramref name="requestedPath"/> (relative to the base) is left out of the result
    /// wherever it appears, so a folder never lists itself as its own child.
    /// </summary>
    public static WebDavListing Parse(string xml, string baseUrl, string? requestedPath)
    {
        var listing = new WebDavListing();
        var doc = XDocument.Parse(xml);

        var baseUri = new Uri(WebDavUrl.NormalizeBase(baseUrl));
        var baseDecoded = Uri.UnescapeDataString(baseUri.AbsolutePath).TrimEnd('/');
        var self = Comparable(requestedPath);

        foreach (var response in doc.Descendants().Where(e => e.Name.LocalName == "response"))
        {
            var href = response.Elements().FirstOrDefault(e => e.Name.LocalName == "href")?.Value;
            if (string.IsNullOrWhiteSpace(href)) continue;

            var relative = RelativePath(href.Trim(), baseUri, baseDecoded);
            if (relative == null) continue;
            if (string.Equals(Comparable(relative), self, StringComparison.OrdinalIgnoreCase)) continue;

            // A server may split the answer into one propstat per status; only the successful ones describe the item.
            var props = response.Elements()
                .Where(e => e.Name.LocalName == "propstat" && IsSuccess(e))
                .SelectMany(e => e.Elements().Where(p => p.Name.LocalName == "prop"))
                .SelectMany(p => p.Elements())
                .ToList();

            if (props.Count == 0) continue;

            XElement? Prop(string name) => props.FirstOrDefault(p => p.Name.LocalName == name);

            var isDirectory = Prop("resourcetype")?.Elements().Any(e => e.Name.LocalName == "collection") ?? false;
            long.TryParse(Prop("getcontentlength")?.Value, out var size);

            var eTag = Prop("getetag")?.Value?.Trim();
            if (string.IsNullOrEmpty(eTag)) eTag = Prop("getlastmodified")?.Value?.Trim();
            if (string.IsNullOrEmpty(eTag)) eTag = null;

            var fileId = props.FirstOrDefault(p => p.Name.LocalName == "fileid" && p.Name.NamespaceName == OwnCloudNamespace);
            if (!string.IsNullOrWhiteSpace(fileId?.Value)) listing.HasOwnCloudFileIds = true;

            var name = Prop("displayname")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                var slash = relative.LastIndexOf('/');
                name = slash >= 0 ? relative[(slash + 1)..] : relative;
            }

            listing.Entries.Add(new WebDavEntry(relative, name, isDirectory, size, eTag));
        }

        return listing;
    }

    /// <summary>
    /// True when a <c>Depth: infinity</c> answer is really a one-level answer: there are folders, yet nothing two
    /// levels down. Some servers downgrade the depth silently, and trusting that would make a library look empty.
    /// </summary>
    public static bool LooksShallow(IReadOnlyList<WebDavEntry> entries, string? rootPath)
    {
        var root = Comparable(rootPath);
        var rootDepth = root.Length == 0 ? 0 : root.Count(c => c == '/') + 1;

        var hasFolder = false;
        foreach (var entry in entries)
        {
            var path = Comparable(entry.Path);
            var depth = path.Length == 0 ? 0 : path.Count(c => c == '/') + 1;
            if (depth - rootDepth >= 2) return false;
            if (entry.IsDirectory) hasFolder = true;
        }

        return hasFolder;
    }

    /// <summary>
    /// The href's path below the base, decoded. The href is resolved while still escaped: decoding it first turned a
    /// "#" or "?" in a folder name into a fragment or query, so "RJ123/#1 本編" came back as "RJ123" and the walk
    /// listed the parent again instead of the sub-folder.
    /// </summary>
    private static string? RelativePath(string href, Uri baseUri, string baseDecoded)
    {
        Uri item;
        try
        {
            item = new Uri(baseUri, href);
        }
        catch (UriFormatException)
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(item.AbsolutePath).TrimEnd('/');
        }
        catch (UriFormatException)
        {
            return null;
        }

        if (string.Equals(decoded, baseDecoded, StringComparison.OrdinalIgnoreCase)) return string.Empty;

        var prefix = baseDecoded + "/";
        if (!decoded.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;

        return decoded[prefix.Length..];
    }

    private static bool IsSuccess(XElement propstat)
    {
        var status = propstat.Elements().FirstOrDefault(e => e.Name.LocalName == "status")?.Value;
        if (string.IsNullOrWhiteSpace(status)) return true;

        var parts = status.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && parts[1].StartsWith('2');
    }

    /// <summary>
    /// Slashes trimmed and Unicode composed, for comparing only. Servers that store decomposed names (NAS shares
    /// written from a Mac) answer "ボ" as two code points; the stored path is left exactly as the server spelt it.
    /// </summary>
    private static string Comparable(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim('/').Normalize(NormalizationForm.FormC);
}
