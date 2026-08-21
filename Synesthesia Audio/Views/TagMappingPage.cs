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
        this.AppThemeBinding(ContentPage.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);

        _searchBar = new SearchBar { Placeholder = "Search Japanese or English tags..." };
        _searchBar.TextChanged += (s, e) => RefreshList();

        _jpEntry = new Entry { Placeholder = "Japanese Tag (e.g. 催眠)", WidthRequest = 150 };
        _engEntry = new Entry { Placeholder = "English Translation", WidthRequest = 150 };

        var addBtn = new Button { Text = "Add Mapping", HeightRequest = AppLayout.ButtonHeightCompact }
            .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark);

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

        var cleanBtn = new Button { Text = "Clean & Reset Bad Tags", HeightRequest = AppLayout.ButtonHeightCompact, Margin = new Thickness(0, 5, 0, 10) }
            .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.WarningLight, AppColors.WarningDark);

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

        _listLayout = new VerticalStackLayout { Spacing = AppLayout.SpacingLarge };

        Content = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Star),
            Children =
            {
                new VerticalStackLayout
                {
                    Padding = AppLayout.MainHeaderPadding,
                    Spacing = AppLayout.SpacingExtraLarge,
                    Children =
                    {
                        cleanBtn,
                        _searchBar,

                        new Label { Text = "Add Custom Mapping:", FontAttributes = AppFonts.Bold, FontSize = AppFontSizes.Body },
                        new HorizontalStackLayout
                        {
                            Spacing = AppLayout.SpacingLarge,
                            Children = { _jpEntry, _engEntry, addBtn }
                        },
                        new BoxView { HeightRequest = 1, Margin = new Thickness(0, 5) }
                            .AppThemeBinding(BoxView.ColorProperty, AppColors.StrokeAltLight, AppColors.StrokeAltDark)
                    }
                }.Row(0),

                new ScrollView
                {
                    Content = _listLayout,
                    Padding = AppLayout.MainScrollPadding
                }.Row(1)
            }
        };

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
