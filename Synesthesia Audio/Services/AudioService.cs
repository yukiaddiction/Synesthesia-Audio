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
    private readonly IDialogService _dialog;
    private readonly ILogger<AudioService> _logger;
    private DateTime _lastSessionSave = DateTime.MinValue;
    private bool _isRestoringSession;
    private double _restorePositionSeconds;
    private int _sourceGeneration;

    private int _consecutiveFailures;
    private const int MaxConsecutivePlaybackFailures = 3;

    // Signalled on the first non-zero duration; replaces polling, which could wait long enough to pause a
    // track the user had since started.
    private TaskCompletionSource<bool> _durationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private void ResetDurationObservation() =>
        _durationObserved = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);


    public AudioService(SettingsService settingsService, Cloud.ICloudProvider cloudProvider, Cloud.LocalHttpProxy localHttpProxy, PlaybackSessionService sessionService, IAudioPlatformService platformService, IDialogService dialogService, ILogger<AudioService> logger)
    {
        _settingsService = settingsService;
        _cloudProvider = cloudProvider;
        _localHttpProxy = localHttpProxy;
        _sessionService = sessionService;
        _platformService = platformService;
        _dialog = dialogService;
        _logger = logger;

        // The backing field, so startup doesn't re-persist the value it just read.
        _volume = Preferences.Get("PlayerVolume", 0.8d);
    }

    [ObservableProperty]
    private Track? _currentTrack;

    /// <summary>
    /// Folder of the work being listened to, or empty when nothing is loaded. Library cards bind to this so the
    /// jacket of the active work can say "Now playing" without each card reaching into the playlist.
    /// </summary>
    public string NowPlayingFolderPath => CurrentTrack != null ? _currentAlbumFolderPath : string.Empty;

    /// <summary>Read as chapters in a voice drama rather than anonymous tracks; empty for a single file.</summary>
    public string QueuePositionText
    {
        get
        {
            lock (_playlistLock)
            {
                return CurrentTrack != null && _currentIndex >= 0 && _playlist.Count > 1
                    ? $"Scene {_currentIndex + 1:00} / {_playlist.Count:00}"
                    : string.Empty;
            }
        }
    }

    partial void OnCurrentTrackChanged(Track? value)
    {
        OnPropertyChanged(nameof(NowPlayingFolderPath));
        OnPropertyChanged(nameof(QueuePositionText));
    }

    private string _currentAlbumFolderPath = string.Empty;

    public string CurrentAlbumFolderPath => _currentAlbumFolderPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVideoOverlayVisible))]
    private bool _isPlaying;

    /// <summary>Screen-reader label that follows the state rather than always announcing "Play Pause".</summary>
    public string PlayPauseAccessibleName => IsPlaying ? "Pause" : "Play";

    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayPauseAccessibleName));

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

    /// <summary>0..1.</summary>
    [ObservableProperty]
    private double _volume = 0.8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeIcon))]
    private bool _isMuted;

    // Muted speaker vs. speaker emoji; near-zero volume counts as muted.
    public string VolumeIcon => IsMuted || Volume <= 0.001 ? "\uD83D\uDD07" : "\uD83D\uDD0A";

    private Timer? _volumeSaveTimer;
    private readonly object _volumeSaveLock = new();

    partial void OnVolumeChanged(double value)
    {
        ApplyVolume();

        // A slider drag raises this per pixel, so the preference write is debounced.
        lock (_volumeSaveLock)
        {
            _volumeSaveTimer ??= new Timer(_ => PersistVolume(), null, Timeout.Infinite, Timeout.Infinite);
            _volumeSaveTimer.Change(500, Timeout.Infinite);
        }

        OnPropertyChanged(nameof(VolumeIcon));
    }

    private void PersistVolume()
    {
        try
        {
            Preferences.Set("PlayerVolume", Volume);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Saving the volume preference failed");
        }
    }

    partial void OnIsMutedChanged(bool value) => ApplyVolume();

    private const double DuckingVolumeFactor = 0.2;
    private bool _isDucked;

    // Only a focus-caused pause may be resumed by focus gain; cleared on manual play/pause so a call ending
    // doesn't restart playback the user stopped.
    private bool _pausedByFocusLoss;

    private void ApplyVolume()
    {
        if (_mediaElement is null)
            return;

        _mediaElement.Volume = IsMuted ? 0 : (_isDucked ? Volume * DuckingVolumeFactor : Volume);
        _mediaElement.ShouldMute = IsMuted;
    }

    public void SetDucked(bool ducked)
    {
        if (_isDucked == ducked) return;
        _isDucked = ducked;
        ApplyVolume();
    }

    /// <summary>Separate from <see cref="Pause"/> so only a focus-caused pause is resumed by focus gain.</summary>
    public void PauseForFocusLoss()
    {
        _pausedByFocusLoss = true;

        var me = _mediaElement;
        if (me == null || !IsPlaying) return;
        me.Pause();
        SaveSession();
    }

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
            // Returning still reaches the finally: a stuck IsSeeking would turn every later seek into a no-op.
            if (me == null || Duration.TotalSeconds <= 0) return;

            var wasPlaying = IsPlaying;
            await me.SeekTo(SeekPosition);

            // Only after the seek completes, or playback resumes at the old position.
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
    /// Resets state without disposing: on Android the process outlives the window and a re-created window reuses
    /// this singleton, so disposing would break every later play.
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

        try
        {
            _platformService.OnMediaDetached();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Releasing platform media state during detach failed");
        }

        // So reopening the window can resume where we were.
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

        // A remote path never exists locally, so the file check applies to local storage only.
        var isCloudSession = IsCloudLibrary();
        if (!isCloudSession && !File.Exists(sessionTrack.FilePath)) return;

        Track track;
        lock (_playlistLock)
        {
            _playlist = session.Playlist.Select(ToTrack).ToList();
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

        var isCloud = IsCloudLibrary();

        // Validate before touching state, or a missing file becomes the saved CurrentTrack with an endless spinner.
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
                    me.MetadataArtworkUrl = _platformService.PublishArtwork(track.CoverImagePath);
                }
                catch (Exception ex)
                {
                    // Logged because a FileProvider misconfiguration otherwise just yields no artwork.
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

                // The URL carries the per-run proxy token, so it is never logged.
                _logger.LogInformation("Cloud stream prepared through the loopback proxy");
                me.Source = MediaSource.FromUri(streamUrl);
            }
            else
            {
                // Full path only at Debug: users attach the log to bug reports and it reveals the library layout.
                _logger.LogInformation("Using local file: {File}", Synesthesia_Audio.Core.LogRedactor.FileName(track.FilePath));
                _logger.LogDebug("Local file path: {Path}", track.FilePath);
                me.Source = MediaSource.FromFile(track.FilePath);
            }

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

        // Shown outside the gate, or the modal would block the next track until dismissed.
        if (failureMessage != null)
        {
            await ShowPlaybackAlertAsync(failureMessage);
        }
    }

    /// <summary>
    /// Auto-advance is capped by consecutive failures, so a library where nothing plays isn't walked forever.
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
                await _dialog.AlertAsync("Playback Error", message);
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
            // Album finished with repeat off: pause and keep the queue. Stop() would clear the playlist and
            // delete the saved session.
            var me = _mediaElement;
            _pausedByFocusLoss = false;
            _isRestoringSession = false;
            me?.Pause();
            IsPlaying = false;
            _platformService.AbandonAudioFocus();
            SaveSession();
            return;
        }

        PlayTrackInternal(nextTrack);
    }

    public void PlayPrevious()
    {
        if (_mediaElement == null) return;

        // Standard player behaviour: past the threshold, "previous" restarts the current track.
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

    /// <summary>Idempotent, unlike <see cref="TogglePlayPause"/>, so a remote "play" never pauses.</summary>
    [RelayCommand]
    public void Play()
    {
        var me = _mediaElement;
        if (me == null || IsPlaying) return;

        _pausedByFocusLoss = false;

        // Ends any pending restore, so it can't pause a track the user just started.
        _isRestoringSession = false;

        if (!_platformService.RequestAudioFocus())
        {
            _logger.LogWarning("Playback not started — the OS refused audio focus");
            return;
        }

        me.Play();
    }

    /// <summary>Idempotent, so a remote "pause" never resumes.</summary>
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
        OnPropertyChanged(nameof(NowPlayingFolderPath));
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

    // Both mapping directions sit together: a field added to only one is silently lost from the saved session.
    private static Track ToTrack(PlaybackSessionTrack saved) => new()
    {
        Title = saved.Title,
        TranslatedTitle = saved.TranslatedTitle,
        FilePath = saved.FilePath,
        CoverImagePath = saved.CoverImagePath,
        VoiceActors = saved.VoiceActors
    };

    private static PlaybackSessionTrack ToSessionTrack(Track track) => new()
    {
        Title = track.Title,
        TranslatedTitle = track.TranslatedTitle,
        FilePath = track.FilePath,
        CoverImagePath = track.CoverImagePath,
        VoiceActors = track.VoiceActors
    };

    private bool IsCloudLibrary() =>
        Synesthesia_Audio.Core.StorageTypes.IsWebDav(_settingsService.CurrentSettings.StorageType);

    private PlaybackSessionState? _lastSavedSession;
    private readonly object _lastSessionLock = new();

    /// <summary>
    /// Reuses the playlist from the last full save so the periodic save doesn't rebuild it just to move the position.
    /// </summary>
    private void SaveSessionPosition()
    {
        PlaybackSessionState copy;
        lock (_lastSessionLock)
        {
            if (_lastSavedSession == null)
            {
                SaveSession();
                return;
            }

            copy = new PlaybackSessionState
            {
                AlbumFolderPath = _lastSavedSession.AlbumFolderPath,
                Playlist = _lastSavedSession.Playlist,
                CurrentTrackIndex = _lastSavedSession.CurrentTrackIndex,
                PositionSeconds = Position.TotalSeconds,
                LoopMode = _lastSavedSession.LoopMode
            };
        }

        _sessionService.SaveAsync(copy).FireAndForget(ex => _logger.LogWarning(ex, "Background task failed"));
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
            Playlist = snapshot.Select(ToSessionTrack).ToList(),
            CurrentTrackIndex = index,
            PositionSeconds = Position.TotalSeconds,
            LoopMode = CurrentLoopMode
        };
        lock (_lastSessionLock) _lastSavedSession = dto;
_sessionService.SaveAsync(dto).FireAndForget(ex => _logger.LogWarning(ex, "Background task failed"));
    }

    private void OnPositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        Position = e.Position;
        var now = DateTime.UtcNow;
        if ((now - _lastSessionSave).TotalSeconds >= AppConstants.SessionSaveIntervalSeconds)
        {
            _lastSessionSave = now;
            SaveSessionPosition();
        }
    }

    private void OnMediaElementPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MediaElement.Duration)) return;

        if ((_mediaElement?.Duration ?? TimeSpan.Zero) > TimeSpan.Zero)
        {
            _durationObserved.TrySetResult(true);
        }
    }

    private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
    {        // Unhandled, a corrupt file, bad codec or proxy error leaves the player stalled with the spinner turning.
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

            Interlocked.Exchange(ref _consecutiveFailures, 0);

            _logger.LogInformation("OnMediaOpened fired — gen: {Gen}, latest: {Latest}, isRestoring: {IsRestoring}",
                gen, _sourceGeneration, _isRestoringSession);

            if (gen != _sourceGeneration)
            {
                _logger.LogInformation("OnMediaOpened — stale callback, skipping");
                return;
            }

            // Bounded so a stream that never reports a duration can't leave this handler pending forever.
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
                    // Clamped half a second before the end so the seek always lands inside the media.
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
        // Detach only: this singleton outlives an Android activity re-creation, so disposing the play gate would
        // make every later play throw ObjectDisposedException.
        DetachMediaElement();
    }
}
