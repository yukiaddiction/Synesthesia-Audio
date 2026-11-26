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
    private readonly VerticalStackLayout _listLayout;
    private readonly Entry _jpEntry;
    private readonly Entry _engEntry;
    private readonly SearchBar _searchBar;

    private Dictionary<string, string> _allMappings = new();

    public TagMappingPage(TagMappingService tagService, LibraryCacheService cacheService, MainViewModel mainViewModel)
    {
        _tagService = tagService;
        _cacheService = cacheService;
        _mainViewModel = mainViewModel;

        Title = "Tag Mapping Manager";
        Shell.SetNavBarIsVisible(this, false);

        _searchBar = new SearchBar { Placeholder = "Search Japanese or English tags...", BackgroundColor = Colors.Transparent };
        _searchBar.TextChanged += (s, e) => RefreshList();
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
                _cacheService.CleanAndReMapAllWorkGenres(_tagService);
                await _mainViewModel.InitializeAsync();
                LoadData();
                await DisplayAlert("Tags Cleaned", "Successfully removed bad dictionary entries and re-mapped all library work tags to clean English!", "OK");
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

        _listLayout = new VerticalStackLayout { Spacing = AppLayout.SpacingLarge };

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

                new ScrollView { Content = _listLayout }.Row(1)
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
        _listLayout.Children.Clear();
        var query = _searchBar.Text?.Trim();

        var filtered = _allMappings.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(kvp =>
                kvp.Key.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                kvp.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
            );
        }

        foreach (var kvp in filtered.OrderBy(k => k.Key))
        {
            var jpKey = kvp.Key;
            var engVal = kvp.Value;

            var rowBorder = new TagMappingRowView(
                jpKey,
                engVal,
                (jp, eng) =>
                {
                    _tagService.AddOrUpdateMapping(jp, eng);
                    _cacheService.CleanAndReMapAllWorkGenres(_tagService);
                    _ = RefreshLibraryAsync();
                },
                (jp) =>
                {
                    _tagService.RemoveMapping(jp);
                    LoadData();
                });

            _listLayout.Children.Add(rowBorder);
        }
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
