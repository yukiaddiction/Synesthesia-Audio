using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Writes the downscaled copies of cover artwork described by <see cref="CoverThumbnails"/>.
///
/// Generation is best effort: a cover that cannot be decoded (an unusual format, a truncated download) simply has
/// no thumbnail and the views fall back to the original, so a decode problem can never make a cover disappear
/// (PERF-08).
/// </summary>
public sealed class CoverThumbnailer
{
    private readonly ILogger<CoverThumbnailer> _logger;

    public CoverThumbnailer(ILogger<CoverThumbnailer> logger) => _logger = logger;

    /// <summary>
    /// Creates the thumbnails for <paramref name="coverPath"/> that do not exist yet. Returns the paths it wrote.
    /// </summary>
    public IReadOnlyList<string> EnsureThumbnails(string? coverPath)
    {
        if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath))
            return Array.Empty<string>();

        // Only local files: a remote URL has nothing to resize until it is downloaded, and the caller stores a
        // local path after that.
        if (coverPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return Array.Empty<string>();

        var written = new List<string>(CoverThumbnails.Sizes.Count);

        foreach (var edge in CoverThumbnails.Sizes)
        {
            var target = CoverThumbnails.For(coverPath, edge);
            if (File.Exists(target))
            {
                written.Add(target);
                continue;
            }

            if (TryWrite(coverPath, target, edge)) written.Add(target);
        }

        return written;
    }

    private bool TryWrite(string sourcePath, string targetPath, int edge)
    {
        string? tempPath = null;

        try
        {
            using var input = File.OpenRead(sourcePath);

            var original = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(input);
            if (original is null) return false;

            // Never upscale: a 100 px cover has no 256 px version.
            var scale = Math.Min(1f, edge / Math.Max(original.Width, original.Height));
            if (scale >= 1f) return false;

            using var resized = original.Downsize(original.Width * scale, original.Height * scale);

            // Written to a temporary file first so a half-written thumbnail is never picked up by a view.
            tempPath = targetPath + ".tmp";
            using (var output = File.Create(tempPath))
            {
                resized.Save(output);
            }

            File.Move(tempPath, targetPath, overwrite: true);
            tempPath = null;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not write a {Edge}px cover thumbnail", edge);
            return false;
        }
        finally
        {
            if (tempPath != null)
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
        }
    }
}
