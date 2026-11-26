using System.Reflection;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace Synesthesia_Audio.Platforms.Android;

internal static class MediaSessionHelper
{
    private sealed class Log { }

    // These are static on purpose, and are the one exception left to PAT-07: an Android media session belongs to
    // the process, not to a window, and the system holds a reference to it. The rule that keeps them safe is that
    // they are cleared through ResetSession() whenever the media element is detached - the process outlives the
    // activity, so a session left behind is never re-wrapped and its transport buttons are dead.
    internal static global::Android.Support.V4.Media.Session.MediaSessionCompat? CurrentSession { get; set; }
    private static volatile bool _wrapped = false;
    private static string _lastAlbumFolderPath = string.Empty;
    private static ArtworkCallback? _artworkCallback;

    internal static void UpdateSessionActivityIntent(string albumFolderPath)
    {
        _lastAlbumFolderPath = albumFolderPath;

        var session = CurrentSession;
        if (session == null) return;

        var context = global::Android.App.Application.Context;
        var pendingIntent = AndroidNotificationHelper.BuildContentIntent(context, albumFolderPath);

        session.SetSessionActivity(pendingIntent);
    }

    // community toolkit media element wraps exoplayer internally and doesn't expose the
    // mediasessionconnector, so we reflect into it to swap in a custom forwarding player
    // that forces the android notification to show next/previous buttons.
    internal static void TryWrapSessionPlayer(CommunityToolkit.Maui.Views.MediaElement mediaElement)
    {
        if (mediaElement == null) return;

        if (_wrapped) return;

        var handler = mediaElement.Handler;
        if (handler == null) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(600);
                var liveHandler = mediaElement.Handler;
                if (liveHandler == null || !ReferenceEquals(liveHandler, handler)) return;
                TryWrap(handler);
            }
            catch (Exception ex)
            {
                Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogWarning(ex, "MediaSession: delayed wrap failed");
            }
        });
    }

    private static void TryWrap(Microsoft.Maui.IElementHandler handler)
    {
        try
        {
            var mediaManagerField = handler.GetType().GetField("mediaManager", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var mediaManager = mediaManagerField?.GetValue(handler);
            if (mediaManager == null) return;

            var connectorField = mediaManager.GetType().GetField("mediaSessionConnector", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var connectorObj = connectorField?.GetValue(mediaManager);
            if (connectorObj == null) return;

            var connector = connectorObj as MediaSessionConnector;
            if (connector == null) return;

            var sessionField = mediaManager.GetType().GetField("mediaSession", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var sessionObj = sessionField?.GetValue(mediaManager);
            if (sessionObj == null) return;

            var mediaSession = sessionObj as global::Android.Support.V4.Media.Session.MediaSessionCompat;
            if (mediaSession == null) return;

            CurrentSession = mediaSession;
            MediaPlaybackForegroundService.UpdateNotification();

            var context = global::Android.App.Application.Context;
            var pendingIntent = AndroidNotificationHelper.BuildContentIntent(context, _lastAlbumFolderPath);

            mediaSession.SetSessionActivity(pendingIntent);

            var playerProp = mediaManager.GetType().GetProperty("Player", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var playerField = mediaManager.GetType().GetField("player", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var currentPlayer = (playerProp?.GetValue(mediaManager) ?? playerField?.GetValue(mediaManager)) as IPlayer;

            if (currentPlayer == null) return;

            if (currentPlayer is CustomForwardingPlayer)
            {
                Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogInformation("MediaSession: Player already wrapped, skipping");
                return;
            }

            var wrapped = new CustomForwardingPlayer(currentPlayer);
            
            var wrappedType = wrapped.GetType();
            var fields = mediaManager.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                if (f.Name.Contains("player", System.StringComparison.OrdinalIgnoreCase) || f.Name.Contains("Player"))
                {
                    if (f.FieldType.IsAssignableFrom(wrappedType))
                    {
                        try
                        {
                            f.SetValue(mediaManager, wrapped);
                        }
                        catch (Exception ex) { Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogWarning(ex, "MediaSession: Field set failed"); }
                    }
                }
            }

            var props = mediaManager.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (p.Name.Equals("Player", System.StringComparison.OrdinalIgnoreCase) && p.CanWrite)
                {
                    if (p.PropertyType.IsAssignableFrom(wrappedType))
                    {
                        try
                        {
                            p.SetValue(mediaManager, wrapped);
                        }
                        catch (Exception ex) { Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogWarning(ex, "MediaSession: Property set failed"); }
                    }
                }
            }

            var playerViewProp = mediaManager.GetType().GetProperty("PlayerView", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var playerView = playerViewProp?.GetValue(mediaManager) as Com.Google.Android.Exoplayer2.UI.PlayerView;
            if (playerView != null)
            {
                try { playerView.Player = wrapped; }
                catch (Exception ex) { Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogWarning(ex, "MediaSession: PlayerView set failed"); }
            }

            const long actionPlay = 1 << 2;
            const long actionPause = 1 << 9;
            const long actionStop = 1 << 0;
            const long actionSeekTo = 1 << 8;
            const long actionFastForward = 1 << 6;
            const long actionRewind = 1 << 3;
            const long actionSkipToNext = 1 << 5;
            const long actionSkipToPrevious = 1 << 4;
            const long actionSetPlaybackSpeed = 1 << 22;

            long enabledActions = actionPlay | actionPause | actionStop | actionSeekTo | 
                                  actionFastForward | actionRewind | actionSkipToNext | 
                                  actionSkipToPrevious | actionSetPlaybackSpeed;
            connector.SetEnabledPlaybackActions(enabledActions);

            var customNavigator = new CustomQueueNavigator(mediaSession);
            connector.SetQueueNavigator(customNavigator);

            if (!_wrapped)
            {
                connector.SetPlayer(wrapped);
            }

            _wrapped = true;
            RegisterArtworkCallback(mediaSession);
            Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogInformation("MediaSession: Successfully wrapped player and attached QueueNavigator on MediaSessionConnector");
        }
        catch (System.Exception ex)
        {
            Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogError(ex, "MediaSession: Exception wrapping session player");
        }
    }

    private static void RegisterArtworkCallback(global::Android.Support.V4.Media.Session.MediaSessionCompat session)
    {
        try
        {
            _artworkCallback = new ArtworkCallback();
            session.Controller.RegisterCallback(_artworkCallback);
        }
        catch (System.Exception ex)
        {
            Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogWarning(ex, "MediaSession: artwork callback registration failed");
        }
    }

    /// <summary>
    /// Forgets the wrapped player, the session and the artwork callback when the media element goes away.
    ///
    /// The Android process outlives the Activity, so these statics survived a re-creation. The stale
    /// <see cref="_wrapped"/> flag then made <see cref="TryWrapSessionPlayer"/> return immediately, so the
    /// re-created session was never wrapped, its next/previous buttons went dead, and the old released
    /// session stayed referenced along with a callback still registered on it.
    /// </summary>
    internal static void ResetSession()
    {
        try
        {
            var session = CurrentSession;
            if (session != null && _artworkCallback != null)
            {
                try
                {
                    session.Controller.UnregisterCallback(_artworkCallback);
                }
                catch (System.Exception ex)
                {
                    Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogWarning(ex, "MediaSession: artwork callback unregistration failed");
                }
            }

            _artworkCallback = null;
            CurrentSession = null;
            _wrapped = false;
            _lastAlbumFolderPath = string.Empty;

            Synesthesia_Audio.Services.Logging.AppLog.Get<Log>()
                .LogInformation("MediaSession: session state reset for the next window");
        }
        catch (System.Exception ex)
        {
            Synesthesia_Audio.Services.Logging.AppLog.Get<Log>().LogWarning(ex, "MediaSession: reset failed");
        }
    }
}

internal class ArtworkCallback : global::Android.Support.V4.Media.Session.MediaControllerCompat.Callback
{
    private static string? _lastCoverPath;
    private static global::Android.Graphics.Bitmap? _cachedBitmap;
    private static long _lastApplyTicks;

    public override void OnMetadataChanged(global::Android.Support.V4.Media.MediaMetadataCompat? metadata)
    {
        base.OnMetadataChanged(metadata);
        ReapplyIfNeeded();
    }

    public override void OnPlaybackStateChanged(global::Android.Support.V4.Media.Session.PlaybackStateCompat? state)
    {
        base.OnPlaybackStateChanged(state);
        if (state?.State == global::Android.Support.V4.Media.Session.PlaybackStateCompat.StatePlaying)
            ReapplyIfNeeded();
    }

    private static void ReapplyIfNeeded()
    {
        var now = DateTime.UtcNow.Ticks;
        if (now - Interlocked.Read(ref _lastApplyTicks) < TimeSpan.TicksPerSecond)
            return;
        Interlocked.Exchange(ref _lastApplyTicks, now);
        ReapplyArtwork();
    }

    private static void ReapplyArtwork()
    {
        try
        {
            var audioService = IPlatformApplication.Current?.Services?.GetService<Synesthesia_Audio.Services.AudioService>();
            var coverPath = audioService?.CurrentTrack?.CoverImagePath;
            if (string.IsNullOrEmpty(coverPath) || !System.IO.File.Exists(coverPath)) return;

            if (coverPath != _lastCoverPath)
            {
                // Bounded, unlike the full-size decode this used to do: an oversized bitmap in the session
                // metadata throws TransactionTooLargeException and the artwork silently disappears (DUP-05).
                _cachedBitmap = AndroidNotificationHelper.DecodeArtwork(coverPath);
                _lastCoverPath = coverPath;
            }

            if (_cachedBitmap == null) return;
            var session = MediaSessionHelper.CurrentSession;
            if (session?.Controller?.Metadata == null) return;
            var metadata = new global::Android.Support.V4.Media.MediaMetadataCompat.Builder(session.Controller.Metadata)
                .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyArt, _cachedBitmap)
                .PutBitmap(global::Android.Support.V4.Media.MediaMetadataCompat.MetadataKeyAlbumArt, _cachedBitmap)
                .Build();
            session.SetMetadata(metadata);
        }
        catch (System.Exception ex)
        {
            // Was an empty catch: a failure here silently left the lock screen without artwork (PAT-05).
            Synesthesia_Audio.Services.Logging.AppLog.Get<ArtworkCallback>()
                .LogWarning(ex, "MediaSession: re-applying artwork failed");
        }
    }
}

public class CustomQueueNavigator : TimelineQueueNavigator
{
    public CustomQueueNavigator(global::Android.Support.V4.Media.Session.MediaSessionCompat window) : base(window)
    {
    }

    public override global::Android.Support.V4.Media.MediaDescriptionCompat GetMediaDescription(IPlayer player, int windowIndex)
    {
        var title = player.CurrentMediaItem?.MediaMetadata?.Title?.ToString() ?? "Synesthesia Audio";
        var artist = player.CurrentMediaItem?.MediaMetadata?.Artist?.ToString() ?? "";
        
        return new global::Android.Support.V4.Media.MediaDescriptionCompat.Builder()
            .SetTitle(title)
            .SetSubtitle(artist)
            .Build();
    }

    public override long GetSupportedQueueNavigatorActions(IPlayer player)
    {
        const long actionSkipToNext = 1 << 5;
        const long actionSkipToPrevious = 1 << 4;
        return actionSkipToNext | actionSkipToPrevious;
    }
}
