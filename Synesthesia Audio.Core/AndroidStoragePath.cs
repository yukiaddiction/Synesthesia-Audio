namespace Synesthesia_Audio.Core;

/// <summary>
/// Turns what the Android folder picker returns into a real filesystem path.
///
/// The picker hands back a Storage Access Framework URI (<c>/tree/primary:Music/document/primary:Music</c>,
/// sometimes still percent-encoded inside a <c>content://</c> URL). Only the folder chosen in Settings went
/// through a conversion — the one chosen from the library screen did not — so a folder picked in Settings was
/// stored as a raw tree URI, and every later scan reported it missing.
/// </summary>
public static class AndroidStoragePath
{
    private const string PrimaryTreeMarker = "tree/primary:";
    private const string DocumentMarker = "/document/";
    private const string PrimaryRoot = "/storage/emulated/0";

    /// <summary>
    /// Returns a filesystem path for <paramref name="pickedPath"/>. Anything that is not the primary-volume
    /// tree URI is returned unchanged, so this is safe to call on every platform.
    /// </summary>
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
