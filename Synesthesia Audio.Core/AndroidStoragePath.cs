namespace Synesthesia_Audio.Core;

/// <summary>
/// Converts the SAF tree URI the Android folder picker returns (<c>/tree/primary:Music/document/...</c>) into a
/// real path; a stored raw URI made every later scan report the folder missing.
/// </summary>
public static class AndroidStoragePath
{
    private const string PrimaryTreeMarker = "tree/primary:";
    private const string DocumentMarker = "/document/";
    private const string PrimaryRoot = "/storage/emulated/0";

    /// <summary>Anything other than a primary-volume tree URI passes through unchanged, so this is safe on every platform.</summary>
    public static string ToFileSystemPath(string? pickedPath)
    {
        if (string.IsNullOrWhiteSpace(pickedPath)) return string.Empty;

        var path = pickedPath.Trim();

        // SAF URIs can arrive percent-encoded (…/tree/primary%3AMusic), so decode before looking for the marker.
        string decoded;
        try
        {
            decoded = Uri.UnescapeDataString(path);
        }
        catch (UriFormatException)
        {
            decoded = path;
        }

        var markerIndex = decoded.IndexOf(PrimaryTreeMarker, StringComparison.Ordinal);
        if (markerIndex < 0) return path;

        var remainder = decoded[(markerIndex + PrimaryTreeMarker.Length)..];

        // The document id repeats the tree id after "/document/"; drop it and keep the relative folder.
        var documentIndex = remainder.IndexOf(DocumentMarker, StringComparison.Ordinal);
        if (documentIndex >= 0) remainder = remainder[..documentIndex];

        remainder = remainder.Trim('/');
        if (remainder.Length == 0) return PrimaryRoot;

        return PrimaryRoot + "/" + remainder;
    }
}
