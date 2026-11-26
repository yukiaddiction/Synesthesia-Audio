using System.Collections.ObjectModel;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.ViewModels;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Views.Components;

namespace Synesthesia_Audio.Views;

public class TagMappingPage : ContentPage
{
    private readonly TagMappingService _tagService;
    private readonly LibraryCacheService _cacheService;
    private readonly MainViewModel _mainViewModel;
    private readonly ObservableCollection<TagMappingRow> _rows = new();
    private readonly Label _emptyLabel;
    private readonly Entry _jpEntry;
    private readonly Entry _engEntry;
    private readonly SearchBar _searchBar;

    private Dictionary<string, string> _allMappings = new();
    private Timer? _searchDebounce;

    public TagMappingPage(TagMappingService tagService, LibraryCacheService cacheService, MainViewModel mainViewModel)
    {
        _tagService = tagService;
        _cacheService = cacheService;
        _mainViewModel = mainViewModel;

        Title = "Tag Mapping Manager";
        Shell.SetNavBarIsVisible(this, false);

        _searchBar = new SearchBar { Placeholder = "Search Japanese or English tags...", BackgroundColor = Colors.Transparent };
        // Debounced: rebuilding every row on each keystroke made typing in the filter crawl on a large
        // mapping list (PERF-09).
        _searchBar.TextChanged += (s, e) =>
        {
            _searchDebounce ??= new Timer(_ => MainThread.BeginInvokeOnMainThread(RefreshList), null, Timeout.Infinite, Timeout.Infinite);
            _searchDebounce.Change(250, Timeout.Infinite);
        };
        _searchBar
            .AppThemeBinding(SearchBar.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
            .AppThemeBinding(SearchBar.PlaceholderColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark);

        // No fixed widths: the row is a Grid with star columns now, so it fits a phone instead of
        // running off the edge (UX-09).
        _jpEntry = GlassTheme.FormEntry(placeholder: "Japanese Tag", fillWidth: true);
        _engEntry = GlassTheme.FormEntry(placeholder: "English Translation", fillWidth: true);
        var addBtn = GlassTheme.PillButton("Add", AppFontSizes.Body, accent: true, height: AppLayout.ButtonHeightCompact);
        addBtn.Clicked += (_, _) =>
        {
            var jp = _jpEntry.Text?.Trim();
            var eng = _engEntry.Text?.Trim();
            if (!string.IsNullOrEmpty(jp) && !string.IsNullOrEmpty(eng))
            {
                _tagService.AddOrUpdateMapping(jp, eng);
                _jpEntry.Text = "";
                _engEntry.Text = "";
                LoadData();
            }
        };

        var cleanBtn = GlassTheme.PillButton("Clean & Reset Bad Tags", AppFontSizes.Body, height: AppLayout.ButtonHeightCompact);
        cleanBtn.Clicked += async (_, _) =>
        {
            try
            {
                await Services.TagCleanup.RunAsync(_cacheService, _tagService, async () =>
                {
                    await _mainViewModel.InitializeAsync();
                    LoadData();
                });
                await DisplayAlert("Tags Cleaned", Services.TagCleanup.SuccessMessage, "OK");
            }
            catch (Exception ex)
            {
                Services.Logging.AppLog.Get<TagMappingPage>().LogError(ex, "Tag clean failed");
            }
        };

        var composer = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius },
            Padding = new Thickness(16),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingExtraLarge,
                Children =
                {
                    new Label { Text = "Add Custom Mapping", FontAttributes = AppFonts.Bold, FontSize = AppFontSizes.Body }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                    // Star columns with a minimum width each: the fields share the available width and the
                    // button keeps its natural size, so nothing overflows on a narrow phone (UX-09).
                    new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Star),
                            new ColumnDefinition(GridLength.Auto)
                        },
                        ColumnSpacing = AppLayout.SpacingLarge,
                        Children = { _jpEntry.Column(0), _engEntry.Column(1), addBtn.Column(2) }
                    },

                    cleanBtn
                }
            }
        }.Glass();

        var searchPill = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassPillRadius },
            Padding = new Thickness(4, 0),
            Content = _searchBar
        }.Glass();

        _emptyLabel = new Label
        {
            Text = "No tag mappings match your filter",
            FontSize = AppFontSizes.Body,
            IsVisible = false,
            Margin = new Thickness(0, 12, 0, 0)
        }
            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        // The row view is bindable now, so the list can be virtualised instead of rebuilt view by view (PERF-09).
        var listView = new CollectionView
        {
            ItemsSource = _rows,
            ItemTemplate = new DataTemplate(() =>
            {
                var row = new TagMappingRowView
                {
                    OnSave = (jp, eng) =>
                    {
                        _tagService.AddOrUpdateMapping(jp, eng);
                        Task.Run(() => _cacheService.CleanAndReMapAllWorkGenres(_tagService))
                            .ContinueWith(_ => RefreshLibraryAsync(), TaskScheduler.FromCurrentSynchronizationContext());
                    },
                    OnDelete = jp =>
                    {
                        _tagService.RemoveMapping(jp);
                        LoadData();
                    }
                };

                row.SetBinding(TagMappingRowView.JpKeyProperty, nameof(TagMappingRow.Key));
                row.SetBinding(TagMappingRowView.EngValueProperty, nameof(TagMappingRow.Value));
                return row;
            })
        };

        var body = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Star),
            Padding = new Thickness(16, 0, 16, 16),
            RowSpacing = AppLayout.SpacingLarge,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = AppLayout.SpacingLarge,
                    Children = { composer, searchPill }
                }.Row(0),

                new Grid
                {
                    RowDefinitions = Rows.Define(Star, Auto),
                    Children = { listView.Row(0), _emptyLabel.Row(1) }
                }.Row(1)
            }
        };

        var scaffold = new GlassScaffold("Tag Mappings", "Japanese to English dictionary", back: true);
        scaffold.SetBody(body);

        Content = scaffold;

        LoadData();
    }

    private void LoadData()
    {
        _allMappings = _tagService.GetAllMappings();
        RefreshList();
    }

    private void RefreshList()
    {
        var query = _searchBar.Text?.Trim();

        var filtered = _allMappings.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(kvp =>
                kvp.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                kvp.Value.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        // Replaces the contents of a bound collection: the CollectionView virtualises and recycles its rows, so
        // the page no longer builds a view per mapping (PERF-09).
        _rows.Clear();
        foreach (var kvp in filtered.OrderBy(k => k.Key))
            _rows.Add(new TagMappingRow(kvp.Key, kvp.Value));

        _emptyLabel.IsVisible = _rows.Count == 0;
    }
    private async Task RefreshLibraryAsync()
    {
        try
        {
            await _mainViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<TagMappingPage>().LogError(ex, "Library refresh after tag update failed");
        }
    }
}

/// <summary>One key/value pair in the mapping list, so the CollectionView has an item type to bind to.</summary>
public sealed record TagMappingRow(string Key, string Value);
