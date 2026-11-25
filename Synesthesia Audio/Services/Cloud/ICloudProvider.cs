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
}

public interface ICloudProvider
{
    bool IsConfigured { get; }

    // Each operation takes an optional explicit connection. Passing one means "use exactly these
    // values", which lets Browse and Test Connection work with unsaved form input without ever
    // mutating the shared saved settings. Omitting it falls back to the saved settings.
    Task<(bool Success, string ErrorMessage)> TestConnectionAsync(WebDavConnection? connection = null);
    Task<List<CloudFileInfo>> ListFilesAsync(string path, WebDavConnection? connection = null);
    Task<Stream> GetFileStreamAsync(string path, WebDavConnection? connection = null);
    Task<HttpResponseMessage> GetFileResponseAsync(string path, string? rangeHeader, WebDavConnection? connection = null);
    Task<bool> FileExistsAsync(string path, WebDavConnection? connection = null);
}
