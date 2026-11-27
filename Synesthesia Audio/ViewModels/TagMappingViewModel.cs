using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Services;

namespace Synesthesia_Audio.ViewModels;

public sealed record TagMappingRow(string Key, string Value);

/// <summary>
/// Holds the tag mapping logic outside the page so filtering, remapping and cleanup can be tested without a UI.
/// </summary>
public class TagMappingViewModel
{
    private readonly TagMappingService _tagService;
    private readonly LibraryCacheService _cacheService;
    private readonly MainViewModel _mainViewModel;
    private readonly ILogger<TagMappingViewModel> _logger;

    private Dictionary<string, string> _allMappings = new();
    private string _searchText = string.Empty;

    public TagMappingViewModel(
        TagMappingService tagService,
        LibraryCacheService cacheService,
        MainViewModel mainViewModel,
        ILogger<TagMappingViewModel> logger)
    {
        _tagService = tagService;
        _cacheService = cacheService;
        _mainViewModel = mainViewModel;
        _logger = logger;
    }

    public ObservableCollection<TagMappingRow> Rows { get; } = new();

    // Lets the page explain a blank list instead of showing nothing.
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Re-filters on every set; debouncing is left to the page because it is a UI concern.</summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (string.Equals(_searchText, trimmed, StringComparison.Ordinal)) return;

            _searchText = trimmed;
            ApplyFilter();
        }
    }

    public void Load() => ApplyFilter(forceReload: true);

    public void AddMapping(string jpKey, string engValue)
    {
        _tagService.AddOrUpdateMapping(jpKey, engValue);
        Load();
    }

    public void RemoveMapping(string jpKey)
    {
        _tagService.RemoveMapping(jpKey);
        Load();
    }

    /// <summary>Remaps the whole library off the UI thread so saving a row doesn't freeze the page.</summary>
    public async Task SaveMappingAsync(string jpKey, string engValue)
    {
        _tagService.AddOrUpdateMapping(jpKey, engValue);

        try
        {
            await Task.Run(() => _cacheService.CleanAndReMapAllWorkGenres(_tagService)).ConfigureAwait(false);
            await _mainViewModel.InitializeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Library refresh after tag update failed");
        }
    }

    public async Task CleanAndResetAsync()
    {
        await Services.TagCleanup.RunAsync(_cacheService, _tagService, async () =>
        {
            await _mainViewModel.InitializeAsync();
            Load();
        }).ConfigureAwait(false);
    }

    private void ApplyFilter(bool forceReload = false)
    {
        if (forceReload) _allMappings = _tagService.GetAllMappings();

        IEnumerable<KeyValuePair<string, string>> filtered = _allMappings;
        if (_searchText.Length > 0)
        {
            filtered = filtered.Where(kvp =>
                kvp.Key.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                kvp.Value.Contains(_searchText, StringComparison.OrdinalIgnoreCase));
        }

        Rows.Clear();
        foreach (var kvp in filtered.OrderBy(k => k.Key))
            Rows.Add(new TagMappingRow(kvp.Key, kvp.Value));
    }
}
