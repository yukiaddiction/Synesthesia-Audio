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

    public AlbumViewModel(AudioService audioService, LibraryCacheService cacheService, DLsiteService dlsiteService, ITranslationService translationService, ILogger<AlbumViewModel> logger)
    {
        AudioService = audioService;
        _cacheService = cacheService;
        _dlsiteService = dlsiteService;
        _translationService = translationService;
        _logger = logger;
    }

    [RelayCommand]
    private async Task Translate()
    {
        if (Album == null) return;

        if (_translationService.RateLimitRemaining is { } pending)
        {
            await Shell.Current.DisplayAlert("Please wait",
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
                await Shell.Current.DisplayAlert("Please wait",
                    $"Too many translations at once. Try again in {(int)Math.Ceiling(cooldown.TotalSeconds)} seconds.", "OK");
            else if (_translationService.LastError != null)
                await Shell.Current.DisplayAlert("Translation", _translationService.LastError, "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Translation error for work: {Title}", Album.Title);
            await Shell.Current.DisplayAlert("Translation Error", $"Could not translate: {ex.Message}", "OK");
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
        });

        await Shell.Current.Navigation.PushModalAsync(editPage);
    }

    [RelayCommand]
    private void PlayTrack(Track track)
    {
        if (track == null || Album == null) return;
        
        var allTracks = Album.SubAlbums.SelectMany(sa => sa.Tracks).ToList();

        // the android lock screen notification reads metadata from the currently playing track,
        // so push album-level cover art and voice actor info down to every track
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
