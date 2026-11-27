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

    // Falls back to last-modified when the server sends no etag; null when it sent neither.
    public string? ETag { get; set; }
}

public interface ICloudProvider
{
    bool IsConfigured { get; }

    // An explicit connection means "use exactly these values", so Browse and Test Connection work with unsaved
    // form input without touching the saved settings.
    Task<(bool Success, string ErrorMessage)> TestConnectionAsync(WebDavConnection? connection = null);
    Task<List<CloudFileInfo>> ListFilesAsync(string path, WebDavConnection? connection = null);

    /// <summary>
    /// Whole tree in one request, or null when the server refuses <c>Depth: infinity</c> (most do by default).
    /// A refusal is remembered so later scans do not ask again.
    /// </summary>
    Task<List<CloudFileInfo>?> TryListTreeAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// True once the server proved it updates a folder's etag when anything beneath it changes (Nextcloud, ownCloud,
    /// oCIS). Only then may a rescan skip an unchanged folder without listing it.
    /// </summary>
    bool PropagatesFolderETags { get; }
    Task<Stream> GetFileStreamAsync(string path, WebDavConnection? connection = null);
    Task<HttpResponseMessage> GetFileResponseAsync(string path, string? rangeHeader, WebDavConnection? connection = null);
    Task<bool> FileExistsAsync(string path, WebDavConnection? connection = null);
}
