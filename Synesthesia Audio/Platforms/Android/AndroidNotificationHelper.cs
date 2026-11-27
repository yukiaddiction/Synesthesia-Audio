using Android.App;
using Microsoft.Extensions.Logging;
using Android.Content;
using Android.Graphics;

namespace Synesthesia_Audio.Platforms.Android;

/// <summary>
/// Shared notification plumbing for the Android services. Artwork is always subsampled: a full-size cover in a
/// PendingIntent payload throws TransactionTooLargeException, which showed up only as missing artwork.
/// </summary>
internal static class AndroidNotificationHelper
{
    public static void CreateChannel(Context context, string channelId, string name, string description)
    {
        if (global::Android.OS.Build.VERSION.SdkInt < global::Android.OS.BuildVersionCodes.O) return;

        var channel = new NotificationChannel(channelId, name, NotificationImportance.Low)
        {
            Description = description
        };

        var manager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
        manager?.CreateNotificationChannel(channel);
    }

    // Distinct request codes keep the two services from replacing each other's intents.
    public static PendingIntent BuildContentIntent(Context context, string? albumFolderPath, int requestCode = 0)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        if (!string.IsNullOrEmpty(albumFolderPath))
            intent.PutExtra("AlbumFolderPath", albumFolderPath);

        return PendingIntent.GetActivity(
            context, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    public static Bitmap? DecodeArtwork(string? path, int maxPixels = 512)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;

        try
        {
            // Read dimensions first, so the real decode can subsample instead of allocating the full image.
            var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeFile(path, bounds);

            var options = new BitmapFactory.Options
            {
                InSampleSize = CalculateSampleSize(bounds.OutWidth, bounds.OutHeight, maxPixels)
            };

            return BitmapFactory.DecodeFile(path, options);
        }
        catch (System.Exception ex)
        {
            Services.Logging.AppLog.Get<MediaPlaybackForegroundService>()
                .LogWarning(ex, "Artwork decode failed");
            return null;
        }
    }

    /// <summary>
    /// Largest power-of-two subsample that keeps the long edge at or above <paramref name="maxPixels"/>;
    /// never below 1, which InSampleSize rejects.
    /// </summary>
    public static int CalculateSampleSize(int width, int height, int maxPixels)
    {
        if (width <= 0 || height <= 0) return 1;
        if (maxPixels <= 0) return 1;

        var longEdge = Math.Max(width, height);
        var sample = 1;

        while (longEdge / (sample * 2) >= maxPixels) sample *= 2;

        return sample;
    }
}
