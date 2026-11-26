namespace Synesthesia_Audio.Core;

/// <summary>
/// Names for the downscaled copies of cover artwork.
///
/// Every card, the dock and the preview pane were all decoding the full-size cover, so a library of large scans
/// held far more pixels in memory than any of those surfaces could show (PERF-08). Thumbnails sit next to the
/// original, named after it, so a cover and its derivatives move together and an orphan sweep can recognise
/// them.
/// </summary>
public static class CoverThumbnails
{
    /// <summary>Wide enough for the largest card or preview at any density.</summary>
    public const int CardEdge = 256;

    /// <summary>Small enough for the transport dock and list rows.</summary>
    public const int SmallEdge = 64;

    /// <summary>The size the app actually uses in views.</summary>
    public static readonly IReadOnlyList<int> Sizes = [CardEdge, SmallEdge];

    /// <summary>
    /// The thumbnail path for <paramref name="coverPath"/> at <paramref name="edge"/>, e.g.
    /// <c>covers/abc.jpg</c> becomes <c>covers/abc.256.jpg</c>. An empty input has no thumbnail.
    /// </summary>
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

    /// <summary>True when the file name is a thumbnail this class would have produced.</summary>
    public static bool IsThumbnail(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var dot = stem.LastIndexOf('.');
        if (dot < 0) return false;

        return int.TryParse(stem[(dot + 1)..], out var edge) && Sizes.Contains(edge);
    }
}
