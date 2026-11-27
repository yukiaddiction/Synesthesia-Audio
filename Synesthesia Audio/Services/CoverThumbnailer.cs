using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Writes the downscaled cover copies described by <see cref="CoverThumbnails"/>. Best effort: an undecodable
/// cover just gets no thumbnail and views fall back to the original, so it never disappears.
/// </summary>
public sealed class CoverThumbnailer
{
    private readonly ILogger<CoverThumbnailer> _logger;

    public CoverThumbnailer(ILogger<CoverThumbnailer> logger) => _logger = logger;

    public IReadOnlyList<string> EnsureThumbnails(string? coverPath)
    {
        if (string.IsNullOrWhiteSpace(coverPath) || !File.Exists(coverPath))
            return Array.Empty<string>();

        // A remote URL has nothing to resize yet; the caller comes back with the local path once downloaded.
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

            // Fit the longer side to `edge`, capped at 1 so a small cover is never upscaled.
            var scale = Math.Min(1f, edge / Math.Max(original.Width, original.Height));
            if (scale >= 1f) return false;

            using var resized = original.Downsize(original.Width * scale, original.Height * scale);

            // Temp file + move so a view never picks up a half-written thumbnail.
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
