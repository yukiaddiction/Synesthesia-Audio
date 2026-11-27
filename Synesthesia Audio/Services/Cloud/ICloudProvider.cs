using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Synesthesia_Audio.Services.Cloud;

public class CloudFileInfo
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public long Size { get; set; }

    /// <summary>The server's change tag (etag, or last-modified when there is none). Null when it sent neither.</summary>
    public string? ETag { get; set; }
}

public interface ICloudProvider
{
    bool IsConfigured { get; }

    // Each operation takes an optional explicit connection. Passing one means "use exactly these
    // values", which lets Browse and Test Connection work with unsaved form input without ever
    // mutating the shared saved settings. Omitting it falls back to the saved settings.
    Task<(bool Success, string ErrorMessage)> TestConnectionAsync(WebDavConnection? connection = null);
    Task<List<CloudFileInfo>> ListFilesAsync(string path, WebDavConnection? connection = null);

    /// <summary>
    /// Everything beneath <paramref name="path"/> in one request, or null when the server will not do that (most
    /// refuse <c>Depth: infinity</c> by default). A refusal is remembered so later scans do not ask again.
    /// </summary>
    Task<List<CloudFileInfo>?> TryListTreeAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// True once a listing showed the server changes a folder's etag whenever anything beneath it changes
    /// (Nextcloud, ownCloud, oCIS). Only then may a rescan skip an unchanged folder without listing it.
    /// </summary>
    bool PropagatesFolderETags { get; }
    Task<Stream> GetFileStreamAsync(string path, WebDavConnection? connection = null);
    Task<HttpResponseMessage> GetFileResponseAsync(string path, string? rangeHeader, WebDavConnection? connection = null);
    Task<bool> FileExistsAsync(string path, WebDavConnection? connection = null);
}
