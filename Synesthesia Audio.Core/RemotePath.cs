namespace Synesthesia_Audio.Core;

/// <summary>
/// Validates library-relative WebDAV paths: a <c>..</c> segment, even percent-escaped, would let a caller read any
/// file the account can see.
/// </summary>
public static class RemotePath
{
    /// <exception cref="ArgumentException">A segment is, or decodes to, <c>.</c> or <c>..</c>.</exception>
    public static string NormalizeRelative(string? path)
    {
        var cleanPath = (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        var segments = cleanPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return string.Empty;

        var safeSegments = new List<string>(segments.Length);

        foreach (var segment in segments)
        {
            if (IsDotSegment(segment))
                throw new ArgumentException($"Remote path may not contain '{segment}' segments: {path}", nameof(path));

            // A segment that only *decodes* to a dot segment would slip past the check above.
            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(segment);
            }
            catch (UriFormatException)
            {
                throw new ArgumentException($"Remote path contains an invalid escape sequence: {path}", nameof(path));
            }

            if (IsDotSegment(decoded))
                throw new ArgumentException($"Remote path may not contain '{decoded}' segments: {path}", nameof(path));

            safeSegments.Add(segment);
        }

        return string.Join("/", safeSegments);
    }

    public static bool IsInsideBase(string? path, string? baseFolder)
    {
        var candidate = ScanListing.NormalizePath(path);
        var parent = ScanListing.NormalizePath(baseFolder);
        return ScanListing.IsUnderOrEqual(candidate, parent);
    }

    private static bool IsDotSegment(string? segment) => segment is "." or "..";
}
