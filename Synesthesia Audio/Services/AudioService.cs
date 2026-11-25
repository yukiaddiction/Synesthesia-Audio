using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;
using Microsoft.Extensions.Logging;
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
    public void EndSeek()
    {
        var me = _mediaElement;
        if (me == null || Duration.TotalSeconds == 0) return;
        var wasPlaying = IsPlaying;
        me.SeekTo(SeekPosition);
        IsSeeking = false;
        if (wasPlaying)
            me.Play();
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
        }

        _mediaElement = mediaElement;

        if (_mediaElement != null)
        {
            _mediaElement.PositionChanged += OnPositionChanged;
            _mediaElement.StateChanged += OnStateChanged;
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
            _mediaElement.MediaFailed += OnMediaFailed;
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
        _ = PlayTrackInternalAsync(track);
    }

    private async Task PlayTrackInternalAsync(Track? track)
    {
        if (track == null || _mediaElement == null) return;
        await _playGate.WaitAsync();
        try
        {
            var me = _mediaElement;
            if (me == null) return;

            _isRestoringSession = false;
            CurrentTrack = track;
            IsVideoTrack = track.FilePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase);
            var thisGen = Interlocked.Increment(ref _sourceGeneration);
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

            _logger.LogInformation("Setting artwork — coverPath: {Path}", track.CoverImagePath ?? "(null)");

            if (!string.IsNullOrEmpty(track.CoverImagePath) && System.IO.File.Exists(track.CoverImagePath))
            {
                try
                {
#if ANDROID
                    var file = new Java.IO.File(track.CoverImagePath);
                    var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(
                        global::Android.App.Application.Context,
                        global::Android.App.Application.Context.PackageName + ".fileprovider",
                        file);
                    global::Android.App.Application.Context.GrantUriPermission(
                        "com.android.systemui", uri, global::Android.Content.ActivityFlags.GrantReadUriPermission);
                    me.MetadataArtworkUrl = uri.ToString();
#else
                    me.MetadataArtworkUrl = new Uri(System.IO.Path.GetFullPath(track.CoverImagePath)).AbsoluteUri;
#endif
                }
                catch { me.MetadataArtworkUrl = string.Empty; }
            }
            else
            {
                me.MetadataArtworkUrl = string.Empty;
            }

            if (!File.Exists(track.FilePath) && _settingsService.CurrentSettings.StorageType != "WebDAV")
            {
                _logger.LogError("Track file not found: {Path}", track.FilePath);
                await ShowPlaybackAlertAsync($"File not found:\n{track.FilePath}");
                return;
            }

            if (_settingsService.CurrentSettings.StorageType == "WebDAV")
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
                    await ShowPlaybackAlertAsync("Cloud streaming proxy could not start.");
                    return;
                }

                // The URL carries the per-run proxy token, so it is never written to the log.
                _logger.LogInformation("Cloud stream prepared through the loopback proxy");
                me.Source = MediaSource.FromUri(streamUrl);
            }
            else
            {
                _logger.LogInformation("Using local file: {Path}", track.FilePath);
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
            await ShowPlaybackAlertAsync($"Could not play track: {ex.Message}\nPath: {track.FilePath}");
        }
        finally
        {
            _playGate.Release();
        }
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
        _ = _sessionService.ClearAsync();
        _pausedByFocusLoss = false;
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
        _ = _sessionService.SaveAsync(dto);
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

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {
        // A corrupt file, an unsupported codec or a proxy error used to leave the player stalled forever
        // with the buffering spinner still turning, because nothing subscribed to this event.
        IsBuffering = false;
        IsPlaying = false;

        var title = CurrentTrack?.Title ?? "This file";
        _logger.LogError("Playback failed — track: {Title}, error: {Error}", title, e.ErrorMessage);

        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures >= MaxConsecutivePlaybackFailures)
        {
            // Stop rather than loop through a playlist in which nothing is playable.
            Interlocked.Exchange(ref _consecutiveFailures, 0);
            _logger.LogError("Giving up after {Count} consecutive playback failures", failures);
            _ = ShowPlaybackAlertAsync($"These files could not be played:\n{e.ErrorMessage}");
            return;
        }

        // Deliberately not awaited: the play gate is not held here, so the message cannot block the
        // next track from starting.
        _ = ShowPlaybackAlertAsync($"{title} could not be played:\n{e.ErrorMessage}");
        MainThread.BeginInvokeOnMainThread(() => PlayNext(isAuto: true));
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

            await Task.Delay(500);
            if (gen != _sourceGeneration) return;

            for (int i = 0; i < 30; i++)
            {
                if (gen != _sourceGeneration) return;
                me = _mediaElement;
                if (me == null) return;
                if (gen != _sourceGeneration) return;
                Duration = me.Duration;
                if (Duration != TimeSpan.Zero) break;
                await Task.Delay(300);
            }

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
            else if (!_isRestoringSession)
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
