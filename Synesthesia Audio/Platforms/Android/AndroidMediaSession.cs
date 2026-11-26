using Android.Content;
using Android.Support.V4.Media;
using Android.Support.V4.Media.Session;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Services.Logging;

namespace Synesthesia_Audio.Platforms.Android;

/// <summary>
/// The app's own Android media session.
///
/// This replaces the previous approach, which reflected into CommunityToolkit's MediaElement handler to borrow its
/// session, wrap its player and steal its transport commands (OVR-03). That version was written against toolkit
/// 5.0.0 internals - a private media manager field - so it broke silently on any toolkit upgrade that moved them.
///
/// The session here is ours: the system binds to it for the lock screen, the notification and headset buttons, and
/// its callbacks drive <see cref="Services.AudioService"/> directly. Playback itself stays with MediaElement; what
/// changed is who owns the session.
/// </summary>
internal static class AndroidMediaSession
{
    private const string Tag = "synesthesia-audio";
    private const string LogCategory = "MediaSession";

    // Static classes cannot be generic arguments (CS0718), so the log category is a nested marker type.
    private sealed class LogMarker { }
    private static readonly ILogger<LogMarker> Log = AppLog.Get<LogMarker>();

    private static MediaSessionCompat? _session;
    private static Callback? _callback;
    private static string _lastAlbumFolderPath = string.Empty;

    /// <summary>
    /// The live session, or null before the first attach. Kept process-wide on purpose: the system holds a
    /// reference to a media session, so it belongs to the process rather than to a window, and it is released in
    /// <see cref="Reset"/> when the media element is detached.
    /// </summary>
    internal static MediaSessionCompat? Current => _session;

    /// <summary>
    /// Creates the session if needed and makes it the active one. Safe to call on every play start.
    /// </summary>
    internal static MediaSessionCompat EnsureSession()
    {
        if (_session != null) return _session;

        var context = global::Android.App.Application.Context;
        var session = new MediaSessionCompat(context, Tag);
        session.SetFlags(MediaSessionCompat.FlagHandlesMediaButtons | MediaSessionCompat.FlagHandlesTransportControls);

        _callback = new Callback();
        session.SetCallback(_callback);
        session.Active = true;

        _session = session;
        UpdatePlaybackState(playing: false, positionMs: 0);
        UpdateActivityIntent();

        Log.LogInformation("{Category}: own media session created", LogCategory);
        return session;
    }

    /// <summary>
    /// Publishes the transport actions and the playing/paused state, which is what makes the system show play,
    /// pause, next and previous rather than a bare notification.
    /// </summary>
    internal static void UpdatePlaybackState(bool playing, long positionMs)
    {
        var session = _session;
        if (session == null) return;

        const long actions =
            PlaybackStateCompat.ActionPlay |
            PlaybackStateCompat.ActionPause |
            PlaybackStateCompat.ActionPlayPause |
            PlaybackStateCompat.ActionSkipToNext |
            PlaybackStateCompat.ActionSkipToPrevious |
            PlaybackStateCompat.ActionSeekTo;

        var state = playing ? PlaybackStateCompat.StatePlaying : PlaybackStateCompat.StatePaused;

        var playbackState = new PlaybackStateCompat.Builder()
            .SetActions(actions)
            .SetState(state, Math.Max(0, positionMs), playing ? 1f : 0f)
            .Build();

        session.SetPlaybackState(playbackState);
    }

    /// <summary>Sets what the lock screen and notification show for the current track.</summary>
    internal static void UpdateMetadata(string? title, string? artist, long durationMs, global::Android.Graphics.Bitmap? artwork)
    {
        var session = _session;
        if (session == null) return;

        var builder = new MediaMetadataCompat.Builder()
            .PutString(MediaMetadataCompat.MetadataKeyTitle, title ?? string.Empty)
            .PutString(MediaMetadataCompat.MetadataKeyArtist, artist ?? string.Empty)
            .PutLong(MediaMetadataCompat.MetadataKeyDuration, Math.Max(0, durationMs));

        if (artwork != null)
        {
            builder.PutBitmap(MediaMetadataCompat.MetadataKeyArt, artwork);
            builder.PutBitmap(MediaMetadataCompat.MetadataKeyAlbumArt, artwork);
        }

        session.SetMetadata(builder.Build());
    }

    /// <summary>Points the session's "open the app" action at the playing album.</summary>
    internal static void UpdateActivityIntent() =>
        UpdateActivityIntent(_lastAlbumFolderPath);

    internal static void UpdateActivityIntent(string albumFolderPath)
    {
        _lastAlbumFolderPath = albumFolderPath ?? string.Empty;

        var session = _session;
        if (session == null) return;

        var context = global::Android.App.Application.Context;
        session.SetSessionActivity(AndroidNotificationHelper.BuildContentIntent(context, _lastAlbumFolderPath));
    }

    /// <summary>
    /// Releases the session. The process outlives the window, so a session left behind would be replaced on the
    /// next attach while the system still pointed at the dead one - which is why detach calls this.
    /// </summary>
    internal static void Reset()
    {
        var session = _session;
        _session = null;
        _callback = null;

        if (session == null) return;

        try
        {
            session.Active = false;
            session.Release();
        }
        catch (Exception ex)
        {
            Log.LogWarning(ex, "{Category}: releasing the session failed", LogCategory);
        }
    }

    /// <summary>
    /// Transport callbacks. These drive <see cref="Services.AudioService"/> rather than a player, because the player
    /// belongs to MediaElement - this session only needs to tell the app what the user pressed.
    /// </summary>
    private sealed class Callback : MediaSessionCompat.Callback
    {
        private static Services.AudioService? Audio =>
            IPlatformApplication.Current?.Services?.GetService<Services.AudioService>();

        public override void OnPlay()
        {
            base.OnPlay();
            Audio?.Play();
        }

        public override void OnPause()
        {
            base.OnPause();
            Audio?.Pause();
        }

        public override void OnStop()
        {
            base.OnStop();
            Audio?.Pause();
        }

        public override void OnSkipToNext()
        {
            base.OnSkipToNext();
            Audio?.PlayNext();
        }

        public override void OnSkipToPrevious()
        {
            base.OnSkipToPrevious();
            Audio?.PreviousTrack();
        }

        public override void OnSeekTo(long positionMs)
        {
            base.OnSeekTo(positionMs);

            var audio = Audio;
            if (audio == null) return;

            // The audio service seeks by value then committing; that is the same path the on-screen slider uses.
            audio.SeekProgress = Math.Clamp(positionMs, 0, long.MaxValue) / 1000.0;
            if (audio.EndSeekCommand.CanExecute(null))
                audio.EndSeekCommand.Execute(null);
        }
    }
}
