namespace Synesthesia_Audio.Core;

/// <summary>
/// The rules that turn a flat list of audio files into works, sub-albums and tracks.
///
/// These are pure path and naming decisions, so they live in Core where they can be tested instead of only being
/// exercised by scanning a real library. They used to be private helpers inside the scan service, which is the
/// "WorkGrouper" part of the split the audit asks for (PAT-09).
/// </summary>
public static class WorkGrouper
{
    /// <summary>
    /// The directory part of a remote path, normalised to forward slashes. Empty string for a path with no
    /// directory, so callers can distinguish "the work folder itself" from "unparseable".
    /// </summary>
    public static string GetRemoteDirectory(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var cleanPath = path.Replace('\\', '/').TrimEnd('/');
        var lastSlash = cleanPath.LastIndexOf('/');
        return lastSlash >= 0 ? cleanPath[..lastSlash] : string.Empty;
    }

    /// <summary>The file name part of a remote path, with either separator.</summary>
    public static string GetRemoteFileName(string? path)
    {
        if (string.IsNullOrEmpty(path)) return string.Empty;

        var cleanPath = path.Replace('\\', '/');
        var lastSlash = cleanPath.LastIndexOf('/');
        return lastSlash >= 0 ? cleanPath[(lastSlash + 1)..] : cleanPath;
    }

    /// <summary>
    /// The sub-album a file belongs to: the directory between the work folder and the file, or null when the file
    /// sits directly in the work folder (the caller names that group "Main").
    ///
    /// A directory outside the work folder is also null - a file that is not actually under this work must not
    /// invent a sub-album for it.
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

    /// <summary>
    /// Which duplicate to keep when the same track exists in several formats: lossless first, then lossy, then
    /// anything unknown - keeping the best copy rather than whichever the file system happened to list first.
    /// </summary>
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

    /// <summary>The title shown for a file, without directory or extension.</summary>
    public static string GetTrackTitle(string? pathOrFileName) =>
        Path.GetFileNameWithoutExtension(GetRemoteFileName(pathOrFileName));
}
