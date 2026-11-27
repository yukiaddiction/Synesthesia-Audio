using Android.Support.V4.Media.Session;
using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Platforms.Android;

// Facade over AndroidMediaSession, kept so existing call sites did not all have to change at once.
internal static class MediaSessionHelper
{
    // Assigning null releases the session, which is what the foreground service does on detach.
    internal static MediaSessionCompat? CurrentSession
    {
        get => AndroidMediaSession.Current;
        set
        {
            if (value == null) AndroidMediaSession.Reset();
        }
    }

    internal static void UpdateSessionActivityIntent(string albumFolderPath) =>
        AndroidMediaSession.UpdateActivityIntent(albumFolderPath);

    // Name kept from the old reflection-based version; there is nothing to wrap any more.
    internal static void TryWrapSessionPlayer(MediaElement mediaElement)
    {
        if (mediaElement == null) return;

        AndroidMediaSession.EnsureSession();
    }

    internal static void UpdatePlaybackState(bool playing, long positionMs) =>
        AndroidMediaSession.UpdatePlaybackState(playing, positionMs);

    internal static void UpdateMetadata(string? title, string? artist, long durationMs, global::Android.Graphics.Bitmap? artwork) =>
        AndroidMediaSession.UpdateMetadata(title, artist, durationMs, artwork);

    internal static void ResetSession() => AndroidMediaSession.Reset();
}
