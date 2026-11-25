using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Models;
using System.Linq;

namespace Synesthesia_Audio.Services;

public partial class AudioService : ObservableObject, IDisposable
{
    private MediaElement? _mediaElement;
    private List<Track> _playlist = new();
    private int _currentIndex = -1;
    private readonly SemaphoreSlim _playGate = new(1, 1);
    private readonly object _playlistLock = new();

    private readonly SettingsService _settingsService;
    private readonly Cloud.ICloudProvider _cloudProvider;
    private readonly Cloud.LocalHttpProxy _localHttpProxy;
    private readonly PlaybackSessionService _sessionService;
    private readonly IAudioPlatformService _platformService;
    private readonly ILogger<AudioService> _logger;
    private DateTime _lastSessionSave = DateTime.MinValue;
    private bool _isRestoringSession;
    private double _restorePositionSeconds;
    private int _sourceGeneration;

    /// <summary>Consecutive MediaFailed events. Reset whenever a track opens successfully.</summary>
    private int _consecutiveFailures;
    private const int MaxConsecutivePlaybackFailures = 3;

    /// <summary>
    /// Completed when the element first reports a non-zero duration for the current source. Used instead
    /// of polling, which could wait up to 9.5 s and therefore pause a track the user had since started.
    /// </summary>
    private TaskCompletionSource<bool> _durationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void ResetDurationObservation() =>
        _durationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

#if ANDROID
    /// <summary>
    /// The cover URI currently shared with the system UI, so the grant can be revoked when the track changes or
    /// the window goes away instead of accumulating for the life of the process (SEC-m-06).
    /// </summary>
    private string? _grantedArtworkUri;

    private void RevokeGrantedArtwork(global::Android.Content.Context context)
    {
        var previous = _grantedArtworkUri;
        _grantedArtworkUri = null;
        if (previous == null) return;

        try
        {
            context.RevokeUriPermission(
                global::Android.Net.Uri.Parse(previous),
                global::Android.Content.ActivityFlags.GrantReadUriPermission);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Revoking a previous artwork grant failed");
        }
    }
#endif

    public AudioService(SettingsService settingsService, Cloud.ICloudProvider cloudProvider, Cloud.LocalHttpProxy localHttpProxy, PlaybackSessionService sessionService, IAudioPlatformService platformService, ILogger<AudioService> logger)
    {
        _settingsService = settingsService;
        _cloudProvider = cloudProvider;
        _localHttpProxy = localHttpProxy;
        _sessionService = sessionService;
        _platformService = platformService;
        _logger = logger;

        // Set the backing field rather than the property so startup does not re-persist it.
        _volume = Preferences.Get("PlayerVolume", 0.8d);
    }

    [ObservableProperty]
    private Track? _currentTrack;

    private string _currentAlbumFolderPath = string.Empty;

    public string CurrentAlbumFolderPath => _currentAlbumFolderPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoOverlayVisible))]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isBuffering;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackProgressText))]
    [NotifyPropertyChangedFor(nameof(PlaybackProgress))]
    private TimeSpan _position;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackProgressText))]
    [NotifyPropertyChangedFor(nameof(PlaybackProgress))]
    private TimeSpan _duration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoopModeText))]
    private LoopMode _currentLoopMode = LoopMode.None;

    [ObservableProperty]
    private bool _isSeeking;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlaybackProgressText))]
    [NotifyPropertyChangedFor(nameof(PlaybackProgress))]
    [NotifyPropertyChangedFor(nameof(SeekProgress))]
    private TimeSpan _seekPosition;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoOverlayVisible))]
    private bool _isVideoTrack;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoOverlayVisible))]
    private bool _isVideoVisible = true;

    [ObservableProperty]
    private bool _isFullScreen;

    /// <summary>Playback volume, 0..1. Persisted so the transport dock keeps its level.</summary>
    [ObservableProperty]
    private double _volume = 0.8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeIcon))]
    private bool _isMuted;

    /// <summary>Glyph for the desktop dock's mute button.</summary>
    public string VolumeIcon => IsMuted || Volume <= 0.001 ? "\uD83D\uDD07" : "\uD83D\uDD0A";

    partial void OnVolumeChanged(double value)
    {
        ApplyVolume();
        Preferences.Set("PlayerVolume", value);
        OnPropertyChanged(nameof(VolumeIcon));
    }

    partial void OnIsMutedChanged(bool value) => ApplyVolume();

    /// <summary>Volume factor applied while the OS asked us to duck under another app.</summary>
    private const double DuckingVolumeFactor = 0.2;
    private bool _isDucked;

    /// <summary>
    /// True when the current pause was caused by losing audio focus, so a later focus gain may resume it.
    /// Cleared on every manual play/pause — otherwise a call ending would restart playback the user had
    /// deliberately stopped.
    /// </summary>
    private bool _pausedByFocusLoss;

    private void ApplyVolume()
    {
        if (_mediaElement is null)
            return;

        _mediaElement.Volume = IsMuted ? 0 : (_isDucked ? Volume * DuckingVolumeFactor : Volume);
        _mediaElement.ShouldMute = IsMuted;
    }

    /// <summary>Lowers playback volume while another app holds transient focus.</summary>
    public void SetDucked(bool ducked)
    {
        if (_isDucked == ducked) return;
        _isDucked = ducked;
        ApplyVolume();
    }

    /// <summary>
    /// Pauses because the OS took audio focus away, and remembers why. Distinct from <see cref="Pause"/>
    /// so that only a focus-caused pause can be resumed by a focus gain.
    /// </summary>
    public void PauseForFocusLoss()
    {
        _pausedByFocusLoss = true;

        var me = _mediaElement;
        if (me == null || !IsPlaying) return;
        me.Pause();
        SaveSession();
    }

    /// <summary>Restores the volume, and resumes playback only if losing focus is what paused it.</summary>
    public void ResumeAfterFocusGain()
    {
        SetDucked(false);
        if (!_pausedByFocusLoss) return;

        _pausedByFocusLoss = false;
        if (!_platformService.RequestAudioFocus())
        {
            _logger.LogWarning("Not resuming — the OS refused audio focus");
            return;
        }

        _mediaElement?.Play();
    }

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;

    /// <summary>
    /// Seeks by a relative offset, clamped to the track bounds. Used by the desktop keyboard
    /// shortcuts so they share the same path as the seek slider.
    /// </summary>
    public void SeekBy(TimeSpan delta)
    {
        var me = _mediaElement;
        if (me is null || Duration.TotalSeconds <= 0)
            return;

        var target = Position + delta;
        if (target < TimeSpan.Zero) target = TimeSpan.Zero;
        if (target > Duration) target = Duration;

        var wasPlaying = IsPlaying;
        Position = target;
        me.SeekTo(target);
        if (wasPlaying) me.Play();
    }

    public bool IsVideoOverlayVisible => IsVideoTrack && IsVideoVisible && IsPlaying;

    public CommunityToolkit.Maui.Views.MediaElement? MediaElement => _mediaElement;

    public string PlaybackProgressText => IsSeeking
        ? $"{SeekPosition:mm\\:ss} / {Duration:mm\\:ss}"
        : $"{Position:mm\\:ss} / {Duration:mm\\:ss}";

    public string LoopModeText => CurrentLoopMode switch
    {
        LoopMode.None => "Repeat Off",
        LoopMode.Track => "Repeat One",
        LoopMode.Album => "Repeat All",
        _ => "Repeat Off"
    };

    public double PlaybackProgress
    {
        get
        {
            var source = IsSeeking ? SeekPosition : Position;
            if (Duration.TotalSeconds == 0) return 0;
            return Math.Clamp(source.TotalSeconds / Duration.TotalSeconds, 0, 1);
        }
    }

    public double SeekProgress
    {
        get => PlaybackProgress;
        set
        {
            if (!IsSeeking || Duration.TotalSeconds == 0) return;
            SeekPosition = TimeSpan.FromSeconds(value * Duration.TotalSeconds);
            OnPropertyChanged(nameof(PlaybackProgress));
            OnPropertyChanged(nameof(PlaybackProgressText));
        }
    }

    [RelayCommand]
    public void BeginSeek()
    {
        SeekPosition = Position;
        IsSeeking = true;
    }

    [RelayCommand]
    public async Task EndSeekAsync()
    {
        var me = _mediaElement;
        try
        {
            // No duration yet means nothing to seek to, but IsSeeking must still be cleared — leaving it
            // set made every later seek request a no-op.
            if (me == null || Duration.TotalSeconds <= 0) return;

            var wasPlaying = IsPlaying;
            await me.SeekTo(SeekPosition);

            // Play only after the seek has completed, otherwise playback resumes at the old position.
            if (wasPlaying) me.Play();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Seeking failed");
        }
        finally
        {
            IsSeeking = false;
        }
    }

    public void SetMediaElement(MediaElement? mediaElement)
    {
        if (_mediaElement != null)
        {
            _mediaElement.PositionChanged -= OnPositionChanged;
            _mediaElement.StateChanged -= OnStateChanged;
            _mediaElement.MediaOpened -= OnMediaOpened;
            _mediaElement.MediaEnded -= OnMediaEnded;
            _mediaElement.MediaFailed -= OnMediaFailed;
            _mediaElement.PropertyChanged -= OnMediaElementPropertyChanged;
        }

        _mediaElement = mediaElement;

        if (_mediaElement != null)
        {
            _mediaElement.PositionChanged += OnPositionChanged;
            _mediaElement.StateChanged += OnStateChanged;
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
            _mediaElement.MediaFailed += OnMediaFailed;
            _mediaElement.PropertyChanged += OnMediaElementPropertyChanged;
        }

        ApplyVolume();
    }

    /// <summary>
    /// Detaches the UI element when its window goes away and resets playback state, without disposing
    /// this singleton. The process outlives the window on Android, so a re-created window gets the same
    /// instance back; disposing it here used to leave the play gate dead and every later play silently
    /// failed with <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void DetachMediaElement()
    {
        var me = _mediaElement;

        try
        {
            me?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stopping the media element during detach failed");
        }

        SetMediaElement(null);

        IsPlaying = false;
        IsBuffering = false;
        IsSeeking = false;
        Position = TimeSpan.Zero;
        Duration = TimeSpan.Zero;
        SeekPosition = TimeSpan.Zero;

        try
        {
            _platformService.OnPlaybackStopped();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resetting platform playback state during detach failed");
        }

#if ANDROID
        // The Android process outlives the Activity, so the media-session statics have to be cleared here;
        // otherwise the next window's session is never wrapped and its next/previous buttons are dead.
        try
        {
            Platforms.Android.MediaSessionHelper.ResetSession();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resetting the Android media session during detach failed");
        }

        // Nothing should keep read access to a cover once this window is gone (SEC-m-06).
        try
        {
            RevokeGrantedArtwork(global::Android.App.Application.Context);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Revoking the artwork grant during detach failed");
        }
#endif

        // Remember where we were so reopening the window can offer to resume.
        SaveSession();
    }

    public async Task TryRestoreSessionAsync()
    {
        var me = _mediaElement;
        if (me == null) return;
        var session = await _sessionService.LoadAsync();
        if (session == null || session.Playlist.Count == 0) return;
        if (session.CurrentTrackIndex < 0 || session.CurrentTrackIndex >= session.Playlist.Count) return;

        var sessionTrack = session.Playlist[session.CurrentTrackIndex];

        // A remote path never exists on the local disk, so this check must only apply to local storage.
        // Running it first made WebDAV session restore dead code.
        var isCloudSession = _settingsService.CurrentSettings.StorageType == "WebDAV";
        if (!isCloudSession && !File.Exists(sessionTrack.FilePath)) return;

        Track track;
        // rebuild playlist from session DTOs
        lock (_playlistLock)
        {
            _playlist = session.Playlist.Select(s => new Track
            {
                Title = s.Title,
                TranslatedTitle = s.TranslatedTitle,
                FilePath = s.FilePath,
                CoverImagePath = s.CoverImagePath,
                VoiceActors = s.VoiceActors
            }).ToList();
            _currentIndex = session.CurrentTrackIndex;
            track = _playlist[_currentIndex];
        }
        CurrentLoopMode = session.LoopMode;
        _currentAlbumFolderPath = session.AlbumFolderPath;

        CurrentTrack = track;
        IsVideoTrack = track.FilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
        Interlocked.Increment(ref _sourceGeneration);
        ResetDurationObservation();

        me.MetadataTitle = !string.IsNullOrWhiteSpace(track.TranslatedTitle)
            ? track.TranslatedTitle
            : track.Title;

        me.MetadataArtist = !string.IsNullOrWhiteSpace(track.VoiceActors)
            ? track.VoiceActors
            : AppConstants.DefaultArtist;

        me.MetadataArtworkUrl = string.Empty;

        _isRestoringSession = true;
        _restorePositionSeconds = session.PositionSeconds;

        if (isCloudSession)
        {
            string streamUrl;
            try
            {
                streamUrl = _localHttpProxy.CreateStreamUrl(track.FilePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TryRestore: cloud streaming proxy could not start, skipping session restore");
                _isRestoringSession = false;
                return;
            }
            me.Source = MediaSource.FromUri(streamUrl);
        }
        else
        {
            me.Source = MediaSource.FromFile(track.FilePath);
        }

        _logger.LogInformation("Session restored: {Title} at {Position:F0}s", track.Title, session.PositionSeconds);
    }

    public void PlayTrack(Track track)
    {
        PlayTrack(track, new List<Track> { track });
    }

    public void PlayTrack(Track track, List<Track> playlist)
    {
        lock (_playlistLock)
        {
            _playlist = playlist ?? new List<Track>();
            _currentIndex = _playlist.IndexOf(track);
        }
        PlayTrackInternal(track);
    }

    private void PlayTrackInternal(Track? track)
    {
PlayTrackInternalAsync(track).FireAndForget(ex => _logger.LogWarning(ex, "Background task failed"));
    }

    private async Task PlayTrackInternalAsync(Track? track)
    {
        if (track == null || _mediaElement == null) return;

        var isCloud = _settingsService.CurrentSettings.StorageType == "WebDAV";

        // Validate before touching any state. A missing file used to become CurrentTrack, get written to
        // the saved session, and leave the buffering spinner turning forever.
        if (!isCloud && !File.Exists(track.FilePath))
        {
            _logger.LogError("Track file not found: {Path}", track.FilePath);
            ReportPlaybackFailure($"File not found:\n{track.FilePath}", advance: true);
            return;
        }

        string? failureMessage = null;
        await _playGate.WaitAsync();
        try
        {
            var me = _mediaElement;
            if (me == null) return;

            _isRestoringSession = false;
            CurrentTrack = track;
            IsVideoTrack = track.FilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
            var thisGen = Interlocked.Increment(ref _sourceGeneration);
            ResetDurationObservation();
            Position = TimeSpan.Zero;
            Duration = TimeSpan.Zero;
            SeekPosition = TimeSpan.Zero;
            IsSeeking = false;
            IsBuffering = true;
            OnPropertyChanged(nameof(PlaybackProgressText));
            OnPropertyChanged(nameof(PlaybackProgress));
            SaveSession();

            me.MetadataTitle = !string.IsNullOrWhiteSpace(track.TranslatedTitle)
                ? track.TranslatedTitle
                : track.Title;

            me.MetadataArtist = !string.IsNullOrWhiteSpace(track.VoiceActors)
                ? track.VoiceActors
                : AppConstants.DefaultArtist;

            _logger.LogInformation("Setting artwork from {File}", Synesthesia_Audio.Core.LogRedactor.FileName(track.CoverImagePath));

            if (!string.IsNullOrEmpty(track.CoverImagePath) && System.IO.File.Exists(track.CoverImagePath))
            {
                try
                {
#if ANDROID
                    var context = global::Android.App.Application.Context;
                    var file = new Java.IO.File(track.CoverImagePath);
                    var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                        context,
                        context.PackageName + ".fileprovider",
                        file);

                    // The previous track's grant is no longer needed. Without this the process accumulated a
                    // read grant per cover it ever displayed (SEC-m-06).
                    RevokeGrantedArtwork(context);

                    context.GrantUriPermission(
                        "com.android.systemui", uri, global::Android.Content.ActivityFlags.GrantReadUriPermission);
                    _grantedArtworkUri = uri.ToString();
                    me.MetadataArtworkUrl = _grantedArtworkUri;
#else
                    me.MetadataArtworkUrl = new Uri(System.IO.Path.GetFullPath(track.CoverImagePath)).AbsoluteUri;
#endif
                }
                catch (Exception ex)
                {
                    // Previously an empty catch: a FileProvider path misconfiguration silently produced no artwork.
                    _logger.LogWarning(ex, "Artwork could not be published to the lock screen");
                    me.MetadataArtworkUrl = string.Empty;
                }
            }
            else
            {
                me.MetadataArtworkUrl = string.Empty;
            }

            if (isCloud)
            {
                _logger.LogInformation("Using WebDAV proxy. Proxy running: {Running}", _localHttpProxy.IsRunning);

                string streamUrl;
                try
                {
                    streamUrl = _localHttpProxy.CreateStreamUrl(track.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WebDAV proxy failed to start");
                    IsBuffering = false;
                    failureMessage = "Cloud streaming proxy could not start.";
                    return;
                }

                // The URL carries the per-run proxy token, so it is never written to the log.
                _logger.LogInformation("Cloud stream prepared through the loopback proxy");
                me.Source = MediaSource.FromUri(streamUrl);
            }
            else
            {
                // File name only at Information: the full path discloses the library layout, and the log is a file
                // users attach to bug reports (SEC-m-02).
                _logger.LogInformation("Using local file: {File}", Synesthesia_Audio.Core.LogRedactor.FileName(track.FilePath));
                _logger.LogDebug("Local file path: {Path}", track.FilePath);
                me.Source = MediaSource.FromFile(track.FilePath);
            }

            // Ask for focus before the first sample plays. A refusal means another app owns the audio
            // output (a call, another player), so we must not start.
            if (!_platformService.RequestAudioFocus())
            {
                _logger.LogWarning("Playback not started — the OS refused audio focus");
                IsBuffering = false;
                return;
            }

            _pausedByFocusLoss = false;
            _logger.LogInformation("Calling MediaElement.Play() — handler: {HasHandler}", me.Handler != null);
            me.Play();
            _logger.LogInformation("MediaElement.Play() returned");
            _platformService.OnPlayStarted(me);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PlayTrackInternal failed for track: {Path}", track.FilePath);
            IsBuffering = false;
            failureMessage = $"Could not play track: {ex.Message}\nPath: {track.FilePath}";
        }
        finally
        {
            _playGate.Release();
        }

        // Shown only after the gate has been released. Awaiting a modal dialog while holding it blocked the
        // next track and the auto-advance path until the user dismissed the message.
        if (failureMessage != null)
        {
            await ShowPlaybackAlertAsync(failureMessage);
        }
    }

    /// <summary>
    /// One failure path for both an explicit play of an unusable file and a MediaFailed event: clear the
    /// spinner, tell the user without holding the play gate, and move on — bounded, so a library in which
    /// nothing is playable cannot be walked forever.
    /// </summary>
    private void ReportPlaybackFailure(string message, bool advance)
    {
        IsBuffering = false;
        IsPlaying = false;

ShowPlaybackAlertAsync(message).FireAndForget(ex => _logger.LogWarning(ex, "Background task failed"));

        if (!advance) return;

        if (Interlocked.Increment(ref _consecutiveFailures) >= MaxConsecutivePlaybackFailures)
        {
            Interlocked.Exchange(ref _consecutiveFailures, 0);
            _logger.LogError("Giving up after {Count} consecutive playback failures", MaxConsecutivePlaybackFailures);
            return;
        }

        MainThread.BeginInvokeOnMainThread(() => PlayNext(isAuto: true));
    }

    private async Task ShowPlaybackAlertAsync(string message)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var shell = Shell.Current;
                if (shell != null)
                    await shell.DisplayAlert("Playback Error", message, "OK");
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to display playback alert");
        }
    }

    public void PlayNext(bool isAuto = false)
    {
        _logger.LogInformation("PlayNext called — isAuto: {IsAuto}, IsPlaying: {IsPlaying}", isAuto, IsPlaying);
        if (_mediaElement == null) return;

        if (isAuto && CurrentLoopMode == LoopMode.Track)
        {
            PlayTrackInternal(CurrentTrack);
            return;
        }

        Track? nextTrack = null;
        bool stopPlayback = false;
        lock (_playlistLock)
        {
            if (_playlist.Count == 0) return;

            int nextIndex = _currentIndex + 1;
            if (nextIndex >= _playlist.Count)
            {
                if (CurrentLoopMode == LoopMode.Album)
                {
                    nextIndex = 0;
                }
                else
                {
                    stopPlayback = true;
                }
            }

            if (!stopPlayback)
            {
                _currentIndex = nextIndex;
                nextTrack = _playlist[nextIndex];
            }
        }

        if (stopPlayback)
        {
            Stop();
            return;
        }

        PlayTrackInternal(nextTrack);
    }

    public void PlayPrevious()
    {
        if (_mediaElement == null) return;

        // if you hit previous more than 3 seconds into a track, restart it. if you're under 3
        // seconds, go back to the actual previous track. standard music player behaviour.
        if (Position.TotalSeconds > AppConstants.PreviousTrackRestartThresholdSeconds)
        {
            PlayTrackInternal(CurrentTrack);
            return;
        }

        Track? prevTrack = null;
        lock (_playlistLock)
        {
            if (_playlist.Count == 0) return;

            int prevIndex = _currentIndex - 1;
            if (prevIndex < 0)
            {
                if (CurrentLoopMode == LoopMode.Album)
                {
                    prevIndex = _playlist.Count - 1;
                }
                else
                {
                    prevIndex = 0;
                }
            }

            _currentIndex = prevIndex;
            prevTrack = _playlist[prevIndex];
        }

        PlayTrackInternal(prevTrack);
    }

    public void ToggleLoopMode()
    {
        CurrentLoopMode = CurrentLoopMode switch
        {
            LoopMode.None => LoopMode.Track,
            LoopMode.Track => LoopMode.Album,
            LoopMode.Album => LoopMode.None,
            _ => LoopMode.None
        };
        SaveSession();
    }

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause();
        else Play();
    }

    /// <summary>
    /// Starts playback. Separate from <see cref="TogglePlayPause"/> so a remote "play" command is
    /// idempotent — pressing play while already playing must not pause.
    /// </summary>
    [RelayCommand]
    public void Play()
    {
        var me = _mediaElement;
        if (me == null || IsPlaying) return;

        // A deliberate play clears the focus-loss state, so regaining focus later cannot resume something
        // the user has already restarted themselves.
        _pausedByFocusLoss = false;

        // Any user action ends a pending session restore, so the restore can never pause a track the user
        // has just started.
        _isRestoringSession = false;

        if (!_platformService.RequestAudioFocus())
        {
            _logger.LogWarning("Playback not started — the OS refused audio focus");
            return;
        }

        me.Play();
    }

    /// <summary>
    /// Pauses playback. Idempotent, which is what a remote "pause" command means: pausing while already
    /// paused used to start playback again.
    /// </summary>
    [RelayCommand]
    public void Pause()
    {
        var me = _mediaElement;
        if (me == null || !IsPlaying) return;
        _pausedByFocusLoss = false;
        _isRestoringSession = false;
        me.Pause();
        SaveSession();
    }

    public void SetCurrentAlbumFolderPath(string folderPath)
    {
        _currentAlbumFolderPath = folderPath;
        _platformService.OnAlbumChanged(folderPath);
    }

    [RelayCommand]
    public void Stop()
    {
        var me = _mediaElement;
        if (me == null) return;
        _logger.LogInformation("Playback stopped");
_sessionService.ClearAsync().FireAndForget(ex => _logger.LogWarning(ex, "Background task failed"));
        _pausedByFocusLoss = false;
        _isRestoringSession = false;
        _platformService.AbandonAudioFocus();
        me.Stop();
        CurrentTrack = null;
        IsVideoTrack = false;
        IsFullScreen = false;
        lock (_playlistLock)
        {
            _playlist.Clear();
            _currentIndex = -1;
        }
    }

    private void SaveSession()
    {
        List<Track> snapshot;
        int index;
        lock (_playlistLock)
        {
            if (CurrentTrack == null || _playlist.Count == 0) return;
            snapshot = new List<Track>(_playlist);
            index = _currentIndex;
        }
        var dto = new PlaybackSessionState
        {
            AlbumFolderPath = _currentAlbumFolderPath,
            Playlist = snapshot.Select(t => new PlaybackSessionTrack
            {
                Title = t.Title,
                TranslatedTitle = t.TranslatedTitle,
                FilePath = t.FilePath,
                CoverImagePath = t.CoverImagePath,
                VoiceActors = t.VoiceActors
            }).ToList(),
            CurrentTrackIndex = index,
            PositionSeconds = Position.TotalSeconds,
            LoopMode = CurrentLoopMode
        };
_sessionService.SaveAsync(dto).FireAndForget(ex => _logger.LogWarning(ex, "Background task failed"));
    }

    private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        Position = e.Position;
        var now = DateTime.UtcNow;
        if ((now - _lastSessionSave).TotalSeconds >= AppConstants.SessionSaveIntervalSeconds)
        {
            _lastSessionSave = now;
            SaveSession();
        }
    }

    /// <summary>Signals that the element has reported a real duration, so a pending restore can seek.</summary>
    private void OnMediaElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MediaElement.Duration)) return;

        if ((_mediaElement?.Duration ?? TimeSpan.Zero) > TimeSpan.Zero)
        {
            _durationObserved.TrySetResult(true);
        }
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {        // A corrupt file, an unsupported codec or a proxy error used to leave the player stalled forever
        // with the buffering spinner still turning, because nothing subscribed to this event.
        var title = CurrentTrack?.Title ?? "This file";
        _logger.LogError("Playback failed — track: {Title}, error: {Error}", title, e.ErrorMessage);

        ReportPlaybackFailure($"{title} could not be played:\n{e.ErrorMessage}", advance: true);
    }

    private void OnStateChanged(object? sender, MediaStateChangedEventArgs e)
    {
        IsPlaying = e.NewState == MediaElementState.Playing;
        IsBuffering = e.NewState == MediaElementState.Buffering || e.NewState == MediaElementState.Opening;
        _platformService.OnPlaybackStateChanged(IsPlaying, CurrentTrack?.Title, _currentAlbumFolderPath);

        if (e.NewState == MediaElementState.Stopped)
        {
            _platformService.OnPlaybackStopped();
        }
    }

    private async void OnMediaOpened(object? sender, EventArgs e)
    {
        var gen = _sourceGeneration;
        try
        {
            var me = _mediaElement;
            if (me == null) return;

            // A track that opened proves the pipeline works, so the failure run is over.
            Interlocked.Exchange(ref _consecutiveFailures, 0);

            _logger.LogInformation("OnMediaOpened fired — gen: {Gen}, latest: {Latest}, isRestoring: {IsRestoring}",
                gen, _sourceGeneration, _isRestoringSession);

            if (gen != _sourceGeneration)
            {
                _logger.LogInformation("OnMediaOpened — stale callback, skipping");
                return;
            }

            // Wait for the element to report a duration instead of polling it for up to 9.5 s. The bound
            // keeps a stream that never reports one from leaving this handler pending forever.
            if (me.Duration == TimeSpan.Zero)
            {
                await Task.WhenAny(_durationObserved.Task, Task.Delay(3000));
            }

            if (gen != _sourceGeneration) return;
            me = _mediaElement;
            if (me == null) return;
            Duration = me.Duration;

            if (_isRestoringSession)
            {
                _isRestoringSession = false;
                _logger.LogInformation("OnMediaOpened — restoring session, seeking to {Pos}s", _restorePositionSeconds);
                if (_restorePositionSeconds > 0 && Duration.TotalSeconds > 0)
                {
                    var seekPos = TimeSpan.FromSeconds(Math.Min(_restorePositionSeconds, Duration.TotalSeconds - 0.5));
                    await me.SeekTo(seekPos);
                    Position = seekPos;
                }
                _logger.LogInformation("OnMediaOpened — pausing for session restore");
                me.Pause();
                _platformService.OnMediaOpened(CurrentTrack?.Title, _currentAlbumFolderPath);
            }
            else
            {
                _platformService.OnMediaOpened(CurrentTrack?.Title, _currentAlbumFolderPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnMediaOpened failed");
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        _logger.LogInformation("OnMediaEnded fired");
        PlayNext(isAuto: true);
    }

    [RelayCommand]
    public void TogglePlay()
    {
        TogglePlayPause();
    }

    [RelayCommand]
    public void NextTrack()
    {
        PlayNext();
    }

    [RelayCommand]
    public void PreviousTrack()
    {
        PlayPrevious();
    }

    [RelayCommand]
    public void ToggleLoop()
    {
        ToggleLoopMode();
    }

    [RelayCommand]
    public void ToggleVideoVisibility()
    {
        IsVideoVisible = !IsVideoVisible;
    }

    [RelayCommand]
    public void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
    }

    public void Dispose()
    {
        // Detach only. This service is a DI singleton that outlives the window: the process stays alive
        // across an Android activity re-creation, so disposing the semaphore here made every later play
        // throw ObjectDisposedException. There is no state that genuinely needs releasing at window close.
        DetachMediaElement();
    }
}
