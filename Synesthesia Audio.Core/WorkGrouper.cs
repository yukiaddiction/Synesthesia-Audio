namespace Synesthesia_Audio.Core;

/// <summary>
/// Pure path and naming rules for grouping audio files into works, sub-albums and tracks, kept here so they are testable
/// without scanning a real library.
/// </summary>
public static class WorkGrouper
{
    /// <summary>Empty for a path with no directory, so callers can tell "the work folder itself" from "unparseable".</summary>
    public static string GetRemoteDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var cleanPath = path.Replace('\\', '/').TrimEnd('/');
        var lastSlash = cleanPath.LastIndexOf('/');
        return lastSlash >= 0 ? cleanPath[..lastSlash] : string.Empty;
    }

    public static string GetRemoteFileName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var cleanPath = path.Replace('\\', '/');
        var lastSlash = cleanPath.LastIndexOf('/');
        return lastSlash >= 0 ? cleanPath[(lastSlash + 1)..] : cleanPath;
    }

    /// <summary>
    /// Null for files directly in the work folder (the caller's "Main" group) and for directories outside the work,
    /// which must not invent a sub-album.
    /// </summary>
    public static string? GetSubAlbumName(string? fileDirectory, string workPath)
    {
        if (string.IsNullOrEmpty(fileDirectory)) return null;
        if (string.Equals(fileDirectory, workPath, StringComparison.OrdinalIgnoreCase)) return null;

        var normalizedFileDir = fileDirectory.Replace('\\', '/').TrimEnd('/');
        var normalizedWork = workPath.Replace('\\', '/').TrimEnd('/');

        if (!normalizedFileDir.StartsWith(normalizedWork, StringComparison.OrdinalIgnoreCase)) return null;

        var relative = normalizedFileDir.Length > normalizedWork.Length
            ? normalizedFileDir[(normalizedWork.Length + 1)..]
            : string.Empty;

        return string.IsNullOrEmpty(relative) ? null : relative;
    }

    /// <summary>Lossless before lossy, so a track present in several formats keeps its best copy.</summary>
    public static int GetExtensionPriority(string? extension) =>
        extension?.ToLowerInvariant() switch
        {
            ".flac" => 1,
            ".wav" => 2,
            ".mp3" => 3,
            ".m4a" => 4,
            ".mp4" => 5,
            _ => 99
        };

    public static string GetTrackTitle(string? pathOrFileName) =>
        Path.GetFileNameWithoutExtension(GetRemoteFileName(pathOrFileName));
}
