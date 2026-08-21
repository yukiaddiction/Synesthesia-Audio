using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Views;
using CommunityToolkit.Maui.Storage;

namespace Synesthesia_Audio.ViewModels;

public partial class MainViewModel : BaseViewModel
{
    private readonly ScanFolderService _scanService;
    private readonly SettingsService _settingsService;
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

    public bool HasFolderSelected => !string.IsNullOrEmpty(_settingsService.CurrentSettings.BaseAudioFolderPath);

    public MainViewModel(ScanFolderService scanService, SettingsService settingsService, LibraryCacheService cacheService, ILogger<MainViewModel> logger)
    {
        _logger = logger;
        var savedFilter = Preferences.Get("LastSelectedFilter", "SFW");
        SelectedFilter = savedFilter;
        Title = savedFilter == "NSFW" ? "Library (R-18)" : "Library (All Age)";
        SortOption = Preferences.Get("LastSortOption", "Title A-Z");

        _scanService = scanService;
        _settingsService = settingsService;
        _cacheService = cacheService;

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

    public async Task InitializeAsync()
    {
        if (_isInitializing) return;
        _isInitializing = true;
        try
        {
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
        }
        finally
        {
            _isInitializing = false;
        }
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

        try
        {
            IsScanning = true;
            _logger.LogInformation("Library scan started — isCloud: {IsCloud}, path: {Path}", isCloud, pathToScan);
            ScanProgress = 0;
            ScanProgressText = "Scan Library Found 0 Work, 0 Audio Files";

            var startTime = DateTime.UtcNow;

            var progress = new Progress<(double, string)>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ScanProgress = p.Item1;
                    ScanProgressText = p.Item2;
                });
            });

            var result = await Task.Run(() => _scanService.ScanLibraryAsync(pathToScan, progress));
            var works = result.Works;
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Library scan completed — {Count} works, {Failures} failed folders, duration: {Duration:F1}s",
                works.Count, result.FailedFolders.Count, elapsed.TotalSeconds);
            _allWorks = works;
            ApplyFilters();

            if (result.FailedFolders.Count > 0)
            {
                var failedList = string.Join("\n", result.FailedFolders.Take(5));
                var suffix = result.FailedFolders.Count > 5 ? $"\n...and {result.FailedFolders.Count - 5} more" : "";
                await Shell.Current.DisplayAlert("Scan Complete",
                    $"Found {works.Count} works ({result.TotalFiles} files).\n\n{result.FailedFolders.Count} folder(s) could not be accessed:\n{failedList}{suffix}",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Library scan failed — path: {Path}", pathToScan);
            await Shell.Current.DisplayAlert("Scan Error",
                $"Scan failed. Check your connection and folder permissions.\n\nPath: {pathToScan}",
                "OK");
        }
        finally
        {
            IsScanning = false;
            ScanProgress = 0;
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
