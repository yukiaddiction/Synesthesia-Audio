namespace Synesthesia_Audio.Core;

/// <summary>
/// Validation for library-relative remote paths (WebDAV).
///
/// The dangerous input is a path that climbs out of the configured base folder. <c>..</c> survives
/// percent-escaping, so simply concatenating a caller's path onto the server URL let a caller read any
/// file the WebDAV account could see. This lives in Core so the rule is unit-testable.
/// </summary>
public static class RemotePath
{
    /// <summary>
    /// Returns the path with forward slashes and no leading slash, or throws when any segment is — or
    /// decodes to — <c>.</c> / <c>..</c>. Empty input yields an empty string.
    /// </summary>
    /// <exception cref="ArgumentException">A segment could escape the base folder.</exception>
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

    /// <summary>True when <paramref name="path"/> stays inside <paramref name="baseFolder"/>.</summary>
    public static bool IsInsideBase(string? path, string? baseFolder)
    {
        var candidate = ScanListing.NormalizePath(path);
        var parent = ScanListing.NormalizePath(baseFolder);
        return ScanListing.IsUnderOrEqual(candidate, parent);
    }

    private static bool IsDotSegment(string? segment) => segment is "." or "..";
}
