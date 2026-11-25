using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Views;
using CommunityToolkit.Maui.Storage;

namespace Synesthesia_Audio.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly ScanFolderService _scanService;
    private readonly SettingsService _settingsService;
    private readonly ScanStateService _scanStateService;
    private List<WorkMetadata> _allWorks = new();

    private readonly LibraryCacheService _cacheService;
    private readonly ILogger<MainViewModel> _logger;

    public ObservableCollection<WorkMetadata> Albums { get; set; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "SFW";

    [ObservableProperty]
    private string _sortOption = "Title A-Z";

    private static readonly string[] SortOptions =
    {
        "Title A-Z", "Title Z-A", "RJ Code Newest", "RJ Code Oldest", "Circle A-Z", "VA A-Z"
    };

    [ObservableProperty]
    private double _scanProgress;

    [ObservableProperty]
    private string _scanProgressText = string.Empty;

    [ObservableProperty]
    private bool _isScanning;

    /// <summary>
    /// The work highlighted in the desktop preview pane. Null until something is picked; the
    /// compact layout never shows the pane, so this stays unused there.
    /// </summary>
    [ObservableProperty]
    private WorkMetadata? _selectedWork;

    public bool HasSelectedWork => SelectedWork != null;

    partial void OnSelectedWorkChanged(WorkMetadata? value) => OnPropertyChanged(nameof(HasSelectedWork));

    [RelayCommand]
    private void SelectWork(WorkMetadata? work) => SelectedWork = work;

    public bool IsScanStatusVisible => IsScanning;

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(IsScanStatusVisible));

    public bool HasFolderSelected => !string.IsNullOrEmpty(_settingsService.CurrentSettings.BaseAudioFolderPath);

    public MainViewModel(ScanFolderService scanService, SettingsService settingsService, LibraryCacheService cacheService, ScanStateService scanStateService, ILogger<MainViewModel> logger)
    {
        _logger = logger;
        var savedFilter = Preferences.Get("LastSelectedFilter", "SFW");
        SelectedFilter = savedFilter;
        Title = savedFilter == "NSFW" ? "Library (R-18)" : "Library (All Age)";
        SortOption = Preferences.Get("LastSortOption", "Title A-Z");

        _scanService = scanService;
        _settingsService = settingsService;
        _cacheService = cacheService;
        _scanStateService = scanStateService;

        _logger.LogInformation("MainViewModel initialized — filter: {Filter}, sort: {Sort}", savedFilter, SortOption);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSortOptionChanged(string value)
    {
        Preferences.Set("LastSortOption", value);
        ApplyFilters();
    }
    partial void OnSelectedFilterChanged(string value)
    {
        Title = value == "NSFW" ? "Library (R-18)" : "Library (All Age)";
        Preferences.Set("LastSelectedFilter", value);
        _logger.LogInformation("Library filter changed to {Filter}", value);
        ApplyFilters();
    }

    private bool _isInitializing;
    private Timer? _scanPollTimer;
    private readonly object _scanPollLock = new();
    private int _scanFinishedHandled;
    private const int PollIntervalMs = 500;

    public async Task InitializeAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try
        {
            // Every store reads its file lazily so nothing does I/O in a constructor. Wait for them here
            // before the first read: otherwise the UI races the load and shows an empty library with
            // default settings, and the "quick check for new works" believes every folder is new.
            await Task.WhenAll(_cacheService.Loaded, _settingsService.Loaded, _scanStateService.Loaded);

            _logger.LogInformation("Library initializing — works in cache: {Count}", _allWorks.Count);
#if ANDROID
            Platforms.Android.BatteryOptimizationHelper.RequestIgnoreBatteryOptimizations();
#endif

            if (_allWorks.Count == 0)
            {
                _allWorks = _cacheService.GetAllWorks();
                _logger.LogInformation("Loaded {Count} works from cache", _allWorks.Count);
            }
            else
            {
                for (int i = 0; i < _allWorks.Count; i++)
                {
                    var updated = _cacheService.GetWork(_allWorks[i].FolderPath);
                    if (updated != null)
                    {
                        _allWorks[i] = updated;
                    }
                }
                _logger.LogDebug("Refreshed {Count} works from cache", _allWorks.Count);
            }

            ApplyFilters();
            OnPropertyChanged(nameof(HasFolderSelected));

            var basePath = _settingsService.CurrentSettings.BaseAudioFolderPath;
            bool isCloud = _settingsService.CurrentSettings.StorageType == "WebDAV";

            if (!string.IsNullOrEmpty(basePath) && (isCloud || Directory.Exists(basePath)))
            {
                var cachedPaths = _allWorks.Select(w => w.FolderPath);
                var newWorks = await _scanService.CheckForNewWorksAsync(basePath, cachedPaths);
                if (newWorks.Count > 0)
                {
                    _logger.LogInformation("Found {Count} new works during quick background check", newWorks.Count);
                    _allWorks.AddRange(newWorks);
                    ApplyFilters();
                }
            }

            await HandleScanStateOnReturnAsync();
        }
        finally
        {
            _isInitializing = false;
        }
    }

    public async Task HandleResumeAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_settingsService.CurrentSettings.BaseAudioFolderPath)) return;
            if (_isInitializing) return;
            await HandleScanStateOnReturnAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resume scan-state handling failed");
        }
    }

    private async Task HandleScanStateOnReturnAsync()
    {
        var state = _scanStateService.Snapshot();

        if (state.Status == ScanStatus.Running)
        {
#if ANDROID
            if (Platforms.Android.ScanForegroundService.IsRunning)
            {
                IsScanning = true;
                ScanProgress = state.Progress;
                ScanProgressText = state.ProgressText;
                StartScanPolling();
                return;
            }
#endif
            _logger.LogWarning("Scan state is Running but no background service is alive — previous scan was interrupted");
            await PromptResumeScanAsync(state);
            return;
        }

        if (state.Status == ScanStatus.Failed)
        {
            if (!state.AlertShown)
            {
                _scanStateService.MarkAlertShown();
                await Shell.Current.DisplayAlert("Scan Failed",
                    state.ErrorMessage ?? "The last library scan failed for an unknown reason.", "OK");
            }
            return;
        }

        if (state.Status == ScanStatus.Completed)
        {
            if (!state.AlertShown)
            {
                _scanStateService.MarkAlertShown();
                await Shell.Current.DisplayAlert("Scan Complete",
                    $"Found {state.WorksFound} works ({state.TotalFiles} audio files).", "OK");
            }
        }
    }

    private async Task PromptResumeScanAsync(ScanState state)
    {
        _scanStateService.MarkInterrupted("The previous scan was interrupted while the app was closed.");

        var resume = await Shell.Current.DisplayAlert("Scan Interrupted",
            $"The previous library scan was interrupted while the app was closed.\n\nIt had reached {Math.Round(state.Progress * 100)}% before stopping.\n\nResume the scan now?",
            "Resume Scan", "Later");

        if (resume)
        {
            await StartBackgroundScanAsync();
        }
    }

    private async Task StartBackgroundScanAsync()
    {
        if (IsScanning) return;

        var basePath = _settingsService.CurrentSettings.BaseAudioFolderPath;
        if (string.IsNullOrEmpty(basePath))
        {
            await Shell.Current.DisplayAlert("Not Configured", "Please pick a library folder in Settings first.", "OK");
            return;
        }

        _scanStateService.MarkRunning("Scan Library Found 0 Work, 0 Audio Files");
        IsScanning = true;
        ScanProgress = 0;
        ScanProgressText = "Scan Library Found 0 Work, 0 Audio Files";
        StartScanPolling();

#if ANDROID
        Platforms.Android.ScanForegroundService.StartScanService();
#else
        RunInProcessScan(basePath);
#endif
    }

    private void RunInProcessScan(string pathToScan)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<(double, string)>(p =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ScanProgress = p.Item1;
                        ScanProgressText = p.Item2;
                    });
                });

                await _scanService.ScanLibraryAsync(pathToScan, progress);
                MainThread.BeginInvokeOnMainThread(OnScanFinishedInProcess);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Library scan failed — path: {Path}", pathToScan);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _scanStateService.MarkFailed($"Scan failed: {ex.Message}");
                    OnScanFinishedInProcess();
                });
            }
        });
    }

    private void OnScanFinishedInProcess()
    {
        StopScanPolling();
        IsScanning = false;
        ReloadFromCache();
        if (TryClaimScanFinished()) HandleScanFinished(_scanStateService.Snapshot());
    }

    private void HandleScanFinished(ScanState state)
    {
        SetStatusLine(state);
        if (state.AlertShown) return;

        _scanStateService.MarkAlertShown();

        if (state.Status == ScanStatus.Failed)
        {
            _ = Shell.Current.DisplayAlert("Scan Failed",
                state.ErrorMessage ?? "The library scan failed.", "OK");
        }
        else if (state.Status == ScanStatus.Completed)
        {
            _ = Shell.Current.DisplayAlert("Scan Complete",
                $"Found {state.WorksFound} works ({state.TotalFiles} audio files).", "OK");
        }
    }

    private void ReloadFromCache()
    {
        _allWorks = _cacheService.GetAllWorks();
        _logger.LogInformation("Library reloaded from cache — {Count} works", _allWorks.Count);
        ApplyFilters();
    }

    private void SetStatusLine(ScanState state)
    {
        if (state.Status == ScanStatus.Running)
        {
            ScanProgress = state.Progress;
            ScanProgressText = state.ProgressText;
        }
    }

    private void StartScanPolling()
    {
        lock (_scanPollLock)
        {
            // A new scan may report its own completion exactly once.
            _scanFinishedHandled = 0;

            if (_scanPollTimer == null)
            {
                _scanPollTimer = new Timer(_ => PollScanStatus(), null, PollIntervalMs, PollIntervalMs);
                return;
            }

            // StopScanPolling only pauses the existing timer. Leaving it paused made the second and every
            // later scan invisible to the poller, so IsScanning stayed true forever and the Rescan button
            // looked dead until the app was restarted.
            _scanPollTimer.Change(PollIntervalMs, PollIntervalMs);
        }
    }

    private void StopScanPolling()
    {
        lock (_scanPollLock)
        {
            _scanPollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    /// <summary>
    /// True for the first caller of the current scan only. The in-process task and the poller can both
    /// observe the same finished scan, which used to raise the completion alert twice.
    /// </summary>
    private bool TryClaimScanFinished() =>
        Interlocked.Exchange(ref _scanFinishedHandled, 1) == 0;

    private void PollScanStatus()
    {
        var state = _scanStateService.Snapshot();
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!state.IsRunning)
            {
                StopScanPolling();
                IsScanning = false;
                ReloadFromCache();
                if (TryClaimScanFinished()) HandleScanFinished(state);
                return;
            }

            ScanProgress = state.Progress;
            ScanProgressText = state.ProgressText;
        });
    }

    private void ApplyFilters()
    {
        var filtered = _allWorks.AsEnumerable();

        bool wantNsfw = SelectedFilter == "NSFW";
        filtered = filtered.Where(w => w.IsNsfw == wantNsfw);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var query = SearchText.Trim();
            filtered = filtered.Where(w => 
                w.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.Genre.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                w.VA.Contains(query, StringComparison.OrdinalIgnoreCase)
            );
        }

        filtered = SortOption switch
        {
            "Title Z-A" => filtered.OrderByDescending(w => w.Title, StringComparer.OrdinalIgnoreCase),
            "RJ Code Newest" => filtered.OrderByDescending(w => GetRjNumber(w.ProductId)),
            "RJ Code Oldest" => filtered.OrderBy(w => GetRjNumber(w.ProductId)),
            "Circle A-Z" => filtered.OrderBy(w => w.Circle, StringComparer.OrdinalIgnoreCase).ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase),
            "VA A-Z" => filtered.OrderBy(w => w.VA, StringComparer.OrdinalIgnoreCase).ThenBy(w => w.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase),
        };

        Albums = new ObservableCollection<WorkMetadata>(filtered);
        OnPropertyChanged(nameof(Albums));
    }

    private static long GetRjNumber(string productId)
    {
        var digits = new string(productId.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, out var n) ? n : 0;
    }

    [RelayCommand]
    private async Task ShowSortOptionsAsync()
    {
        var choice = await Shell.Current.DisplayActionSheet("Sort By", "Cancel", null, SortOptions);
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;
        SortOption = choice;
    }

    [RelayCommand]
    private async Task ScanLibraryAsync()
    {
        bool isCloud = _settingsService.CurrentSettings.StorageType == "WebDAV";
        string pathToScan = _settingsService.CurrentSettings.BaseAudioFolderPath;

        if (isCloud)
        {
            if (string.IsNullOrEmpty(pathToScan))
            {
                await Shell.Current.DisplayAlert("Not Configured", "Please configure your WebDAV cloud server URL and remote library folder in Settings first.", "OK");
                return;
            }
        }
        else
        {
            // If no path set yet, open folder picker (initial scan). Otherwise rescan existing path.
            if (string.IsNullOrEmpty(pathToScan))
            {
                try
                {
#if ANDROID
                    var status = await Permissions.CheckStatusAsync<AudioStoragePermission>();
                    if (status != PermissionStatus.Granted)
                    {
                        status = await Permissions.RequestAsync<AudioStoragePermission>();
                    }

                    if (status != PermissionStatus.Granted)
                    {
                        await Shell.Current.DisplayAlert("Permission Denied", "Permission to read audio files was denied. The app cannot scan the library.", "OK");
                        return;
                    }
#endif

                    var result = await FolderPicker.Default.PickAsync(default);
                    if (!result.IsSuccessful) return;

                    pathToScan = result.Folder.Path;

#if ANDROID
                    if (pathToScan.StartsWith("/tree/primary:"))
                        pathToScan = pathToScan.Replace("/tree/primary:", "/storage/emulated/0/");
#endif

                    if (!Directory.Exists(pathToScan)) return;

                    _settingsService.CurrentSettings.BaseAudioFolderPath = pathToScan;
                    _settingsService.SaveSettings(_settingsService.CurrentSettings);
                }
                catch (Exception ex)
                {
                    await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
                    return;
                }
            }
            else if (!Directory.Exists(pathToScan))
            {
                await Shell.Current.DisplayAlert("Folder Missing", $"The configured folder no longer exists:\n{pathToScan}\n\nPlease pick a new folder.", "OK");
                _settingsService.CurrentSettings.BaseAudioFolderPath = "";
                _settingsService.SaveSettings(_settingsService.CurrentSettings);
                return;
            }
        }

        if (IsScanning)
        {
            return;
        }

        try
        {
            _scanStateService.MarkRunning("Scan Library Found 0 Work, 0 Audio Files");

#if ANDROID
            IsScanning = true;
            ScanProgress = 0;
            ScanProgressText = "Scan Library Found 0 Work, 0 Audio Files";
            StartScanPolling();
            Platforms.Android.ScanForegroundService.StartScanService();
#else
            IsScanning = true;
            _logger.LogInformation("Library scan started — isCloud: {IsCloud}, path: {Path}", isCloud, pathToScan);
            ScanProgress = 0;
            ScanProgressText = "Scan Library Found 0 Work, 0 Audio Files";
            RunInProcessScan(pathToScan);
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start library scan — path: {Path}", pathToScan);
            IsScanning = false;
            _scanStateService.MarkFailed($"Scan could not be started: {ex.Message}");
            await Shell.Current.DisplayAlert("Scan Error",
                $"Scan failed to start. Check your connection and folder permissions.\n\nPath: {pathToScan}",
                "OK");
        }
    }

    [RelayCommand]
    private async Task OpenAlbumAsync(WorkMetadata album)
    {
        if (album == null) return;
        await Shell.Current.GoToAsync(nameof(AlbumPage), new Dictionary<string, object> { { "Album", album } });
    }

    [RelayCommand]
    private async Task GoToSettingsAsync()
    {
        await Shell.Current.GoToAsync("//Settings");
    }

    public void ClearAllWorks()
    {
        _allWorks.Clear();
        Albums.Clear();
    }
}

#if ANDROID
#pragma warning disable CA1416
public class AudioStoragePermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions
    {
        get
        {
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
            {
                return new[] { (Android.Manifest.Permission.ReadMediaAudio, true) };
            }
            else
            {
                return new[] { (Android.Manifest.Permission.ReadExternalStorage, true) };
            }
        }
    }
}
#pragma warning restore CA1416
#endif
