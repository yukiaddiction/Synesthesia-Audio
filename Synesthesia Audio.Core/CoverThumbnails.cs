namespace Synesthesia_Audio.Core;

/// <summary>
/// Views decode downscaled copies instead of full-size scans. Thumbnails sit next to the original, named after it,
/// so they move together and the orphan sweep can recognise them.
/// </summary>
public static class CoverThumbnails
{
    /// <summary>Wide enough for the largest card or preview at any density.</summary>
    public const int CardEdge = 256;

    /// <summary>Small enough for the transport dock and list rows.</summary>
    public const int SmallEdge = 64;

    public static readonly IReadOnlyList<int> Sizes = [CardEdge, SmallEdge];

    /// <summary>E.g. <c>covers/abc.jpg</c> becomes <c>covers/abc.256.jpg</c>.</summary>
    public static string For(string? coverPath, int edge)
    {
        if (string.IsNullOrWhiteSpace(coverPath)) return string.Empty;

        var directory = Path.GetDirectoryName(coverPath);
        var stem = Path.GetFileNameWithoutExtension(coverPath);
        var extension = Path.GetExtension(coverPath);
        if (string.IsNullOrEmpty(extension)) extension = ".jpg";

        var name = stem + "." + edge + extension;
        return string.IsNullOrEmpty(directory) ? name : Path.Combine(directory, name);
    }

    public static bool IsThumbnail(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var dot = stem.LastIndexOf('.');
        if (dot < 0) return false;

        return int.TryParse(stem[(dot + 1)..], out var edge) && Sizes.Contains(edge);
    }
}
