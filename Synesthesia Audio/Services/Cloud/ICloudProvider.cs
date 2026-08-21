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
    Task<(bool Success, string ErrorMessage)> TestConnectionAsync();
    Task<List<CloudFileInfo>> ListFilesAsync(string path);
    Task<Stream> GetFileStreamAsync(string path);
    Task<HttpResponseMessage> GetFileResponseAsync(string path, string? rangeHeader);
    Task<bool> FileExistsAsync(string path);
}
