using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Services;

namespace Synesthesia_Audio.ViewModels;

/// <summary>One key/value pair in the mapping list, so a CollectionView has an item type to bind to.</summary>
public sealed record TagMappingRow(string Key, string Value);

/// <summary>
/// The tag mapping page's state: the mapping list, the filter, and the save/delete/clean operations.
///
/// This existed only inside the page, which meant the filtering, the remap-after-save and the cleanup could not be
/// exercised without a UI (PAT-09 asks for the view model by name). The page now owns layout and binding only.
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

    /// <summary>The rows currently shown, already filtered and ordered.</summary>
    public ObservableCollection<TagMappingRow> Rows { get; } = new();

    /// <summary>True when the filter matches nothing, so the page can explain the blank area (UX-05).</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Filter text. Setting it re-filters; the caller debounces, because that is a UI concern.</summary>
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

    /// <summary>Removes a mapping and refreshes the list.</summary>
    public void RemoveMapping(string jpKey)
    {
        _tagService.RemoveMapping(jpKey);
        Load();
    }

    /// <summary>
    /// Saves a row's translation and remaps every cached work off the UI thread - it used to block the page for the
    /// whole library on every row save (PERF-09).
    /// </summary>
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

    /// <summary>Clean and reset bad tags, then reload this list and the library.</summary>
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
