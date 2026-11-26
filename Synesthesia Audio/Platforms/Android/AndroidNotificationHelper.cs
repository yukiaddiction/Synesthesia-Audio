using Android.App;
using Microsoft.Extensions.Logging;
using Android.Content;
using Android.Graphics;

namespace Synesthesia_Audio.Platforms.Android;

/// <summary>
/// The notification plumbing the Android services kept re-writing: channel creation, the content intent that
/// opens the app on a specific album, and a bitmap decode that stays under the binder limit (DUP-05).
///
/// The decode rule matters most: <c>BitmapFactory.DecodeFile</c> without <c>InSampleSize</c> allocates the full
/// image, and a large cover passed through a <c>PendingIntent</c> payload throws
/// <c>TransactionTooLargeException</c> — which surfaced as "no artwork at all", not as an error.
/// </summary>
internal static class AndroidNotificationHelper
{
    /// <summary>Creates (or updates) a low-importance notification channel. Safe to call repeatedly.</summary>
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

    /// <summary>
    /// The action <c>PendingIntent</c> that opens the app, optionally landing on an album. Distinct
    /// <paramref name="requestCode"/> values keep the two services from replacing each other's intents.
    /// </summary>
    public static PendingIntent BuildContentIntent(Context context, string? albumFolderPath, int requestCode = 0)
    {
        var intent = new Intent(context, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);

        if (!string.IsNullOrEmpty(albumFolderPath))
            intent.PutExtra("AlbumFolderPath", albumFolderPath);

        return PendingIntent.GetActivity(
            context, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
    }

    /// <summary>
    /// Decodes artwork subsampled to roughly <paramref name="maxPixels"/> on the long edge, so the bitmap fits
    /// in a notification. Returns null when the file is missing or unreadable.
    /// </summary>
    public static Bitmap? DecodeArtwork(string? path, int maxPixels = 512)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return null;

        try
        {
            // First pass reads dimensions only, so the second can subsample rather than allocating the full
            // image and throwing it away.
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
    /// The largest power-of-two subsample that still leaves the long edge at or above
    /// <paramref name="maxPixels"/>. Always at least 1, because <c>InSampleSize</c> below 1 is invalid.
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
