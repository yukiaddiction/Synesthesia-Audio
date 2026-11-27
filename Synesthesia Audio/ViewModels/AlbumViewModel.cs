using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Views;

namespace Synesthesia_Audio.ViewModels;

[QueryProperty(nameof(Album), "Album")]
public partial class AlbumViewModel : BaseViewModel
{
    public AudioService AudioService { get; }
    private readonly LibraryCacheService _cacheService;
    private readonly DLsiteService _dlsiteService;
    private readonly ITranslationService _translationService;
    private readonly ILogger<AlbumViewModel> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IDialogService _dialog;
    private readonly INavigationService _navigation;

    private WorkMetadata? _album;
    public WorkMetadata? Album
    {
        get => _album;
        set
        {
            if (SetProperty(ref _album, value))
            {
                OnPropertyChanged(nameof(GenreTags));
            }
        }
    }

    public List<string> GenreTags => Album?.Genre?
        .Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(t => t.Trim())
        .Where(t => !string.IsNullOrEmpty(t))
        .ToList() ?? new();

    public AlbumViewModel(AudioService audioService, LibraryCacheService cacheService, DLsiteService dlsiteService, ITranslationService translationService, IDialogService dialogService, INavigationService navigationService, ILoggerFactory loggerFactory)
    {
        AudioService = audioService;
        _cacheService = cacheService;
        _dlsiteService = dlsiteService;
        _translationService = translationService;
        _dialog = dialogService;
        _navigation = navigationService;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<AlbumViewModel>();
    }

    [RelayCommand]
    private async Task Translate()
    {
        if (Album == null) return;

        if (_translationService.RateLimitRemaining is { } pending)
        {
            await _dialog.AlertAsync("Please wait",
                $"Translation services are busy. Try again in {(int)Math.Ceiling(pending.TotalSeconds)} seconds.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            _translationService.ClearError();
            _logger.LogInformation("Translating work: {Title}", Album.Title);
            await _dlsiteService.TranslateWorkAsync(Album);
            _cacheService.UpdateWork(Album);
            OnPropertyChanged(nameof(Album));
            OnPropertyChanged(nameof(GenreTags));

            if (_translationService.RateLimitRemaining is { } cooldown)
                await _dialog.AlertAsync("Please wait",
                    $"Too many translations at once. Try again in {(int)Math.Ceiling(cooldown.TotalSeconds)} seconds.", "OK");
            else if (_translationService.LastError != null)
                await _dialog.AlertAsync("Translation", _translationService.LastError, "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation error for work: {Title}", Album.Title);
            await _dialog.AlertAsync("Translation Error", $"Could not translate: {ex.Message}", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenEditDialog()
    {
        if (Album == null) return;

        var editPage = new EditWorkPage(Album, _cacheService, () =>
        {
            OnPropertyChanged(nameof(Album));
            OnPropertyChanged(nameof(GenreTags));
        }, _loggerFactory.CreateLogger<EditWorkPage>());

        await _navigation.PushModalAsync(editPage);
    }

    [RelayCommand]
    private void PlayTrack(Track track)
    {
        if (track == null || Album == null) return;
        
        var allTracks = Album.SubAlbums.SelectMany(sa => sa.Tracks).ToList();

        // The Android lock-screen notification reads metadata from the playing track, so copy album-level info onto each.
        foreach (var t in allTracks)
        {
            t.CoverImagePath = Album.CoverImagePath;
            t.VoiceActors = Album.VA;
        }

        AudioService.SetCurrentAlbumFolderPath(Album.FolderPath);
        AudioService.PlayTrack(track, allTracks);
    }

    [RelayCommand]
    private void ToggleExpand(SubAlbum subAlbum)
    {
        if (subAlbum == null) return;
        subAlbum.IsExpanded = !subAlbum.IsExpanded;
    }
}
