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

    public AudioService(SettingsService settingsService, Cloud.ICloudProvider cloudProvider, Cloud.LocalHttpProxy localHttpProxy, PlaybackSessionService sessionService, IAudioPlatformService platformService, ILogger<AudioService> logger)
    {
        _settingsService = settingsService;
        _cloudProvider = cloudProvider;
        _localHttpProxy = localHttpProxy;
        _sessionService = sessionService;
        _platformService = platformService;
        _logger = logger;
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

    public void SetMediaElement(MediaElement mediaElement)
    {
        if (_mediaElement != null)
        {
            _mediaElement.PositionChanged -= OnPositionChanged;
            _mediaElement.StateChanged -= OnStateChanged;
            _mediaElement.MediaOpened -= OnMediaOpened;
            _mediaElement.MediaEnded -= OnMediaEnded;
        }

        _mediaElement = mediaElement;

        if (_mediaElement != null)
        {
            _mediaElement.PositionChanged += OnPositionChanged;
            _mediaElement.StateChanged += OnStateChanged;
            _mediaElement.MediaOpened += OnMediaOpened;
            _mediaElement.MediaEnded += OnMediaEnded;
        }
    }

    public async Task TryRestoreSessionAsync()
    {
        var me = _mediaElement;
        if (me == null) return;
        var session = _sessionService.Load();
        if (session == null || session.Playlist.Count == 0) return;
        if (session.CurrentTrackIndex < 0 || session.CurrentTrackIndex >= session.Playlist.Count) return;

        var sessionTrack = session.Playlist[session.CurrentTrackIndex];
        if (!File.Exists(sessionTrack.FilePath)) return;

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

        if (_settingsService.CurrentSettings.StorageType == "WebDAV")
        {
            if (!_localHttpProxy.EnsureRunning())
            {
                _logger.LogWarning("TryRestore: Cloud proxy not running, skipping session restore");
                _isRestoringSession = false;
                return;
            }
            var streamUrl = _localHttpProxy.GetProxyUrl(track.FilePath);
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
                if (!_localHttpProxy.EnsureRunning())
                {
                    _logger.LogError("WebDAV proxy failed to start");
                    await ShowPlaybackAlertAsync("Cloud streaming proxy could not start.");
                    return;
                }
                var streamUrl = _localHttpProxy.GetProxyUrl(track.FilePath);
                _logger.LogInformation("Proxy URL: {Url}", streamUrl);
                me.Source = MediaSource.FromUri(streamUrl);
            }
            else
            {
                _logger.LogInformation("Using local file: {Path}", track.FilePath);
                me.Source = MediaSource.FromFile(track.FilePath);
            }

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
        var me = _mediaElement;
        if (me == null) return;
        if (IsPlaying)
        {
            me.Pause();
            SaveSession();
        }
        else
        {
            me.Play();
        }
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
        _sessionService.Clear();
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
        try
        {
            _mediaElement?.Stop();
            SetMediaElement(null!);
            _playGate.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AudioService dispose error");
        }
    }
}
