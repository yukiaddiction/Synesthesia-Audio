using Android.Support.V4.Media.Session;
using CommunityToolkit.Maui.Views;

namespace Synesthesia_Audio.Platforms.Android;

/// <summary>
/// Thin facade over <see cref="AndroidMediaSession"/>, kept so the call sites that already existed do not have to
/// change at once.
///
/// Everything that used to live here - reflecting into CommunityToolkit's MediaElement handler to borrow its
/// private media manager, wrapping its player with a ForwardingPlayer and installing a custom queue navigator -
/// is gone (OVR-03). The session is the app's own now.
/// </summary>
internal static class MediaSessionHelper
{
    /// <summary>
    /// The app's media session, or null before the first attach. Assigning null releases it, which is what the
    /// foreground service does on detach.
    /// </summary>
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

    /// <summary>
    /// Starts or refreshes the session. The name is kept from the reflection version: callers attach it when a media
    /// element appears, and there is nothing to wrap any more.
    /// </summary>
    internal static void TryWrapSessionPlayer(MediaElement mediaElement)
    {
        if (mediaElement == null) return;

        AndroidMediaSession.EnsureSession();
    }

    /// <summary>Publishes the playing/paused state so the notification and headset buttons reflect it.</summary>
    internal static void UpdatePlaybackState(bool playing, long positionMs) =>
        AndroidMediaSession.UpdatePlaybackState(playing, positionMs);

    /// <summary>Sets the lock-screen text and artwork for the current track.</summary>
    internal static void UpdateMetadata(string? title, string? artist, long durationMs, global::Android.Graphics.Bitmap? artwork) =>
        AndroidMediaSession.UpdateMetadata(title, artist, durationMs, artwork);

    internal static void ResetSession() => AndroidMediaSession.Reset();
}
