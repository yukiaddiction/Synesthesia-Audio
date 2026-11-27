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
    private readonly TagMappingViewModel _viewModel;
    private readonly Label _emptyLabel;
    private readonly Entry _jpEntry;
    private readonly Entry _engEntry;
    private readonly SearchBar _searchBar;

    private Timer? _searchDebounce;

    private readonly ILogger<TagMappingPage> _logger;

    public TagMappingPage(TagMappingViewModel viewModel, ILogger<TagMappingPage> logger)
    {
        _logger = logger;
        _viewModel = viewModel;
                BindingContext = viewModel;

        Title = "Tag Mapping Manager";
        Shell.SetNavBarIsVisible(this, false);

        _searchBar = new SearchBar { Placeholder = "Search Japanese or English tags...", BackgroundColor = Colors.Transparent };
        // Debounced: refiltering a large mapping list on every keystroke makes typing crawl.
        _searchBar.TextChanged += (s, e) =>
        {
            _searchDebounce ??= new Timer(_ => MainThread.BeginInvokeOnMainThread(() =>
        {
            _viewModel.SearchText = _searchBar.Text ?? string.Empty;
            _emptyLabel.IsVisible = _viewModel.IsEmpty;
        }), null, Timeout.Infinite, Timeout.Infinite);
            _searchDebounce.Change(250, Timeout.Infinite);
        };
        _searchBar
            .AppThemeBinding(SearchBar.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
            .AppThemeBinding(SearchBar.PlaceholderColorProperty, AppColors.TextTertiaryLight, AppColors.TextSecondaryDark);

        _jpEntry = GlassTheme.FormEntry(placeholder: "Japanese Tag", fillWidth: true);
        _engEntry = GlassTheme.FormEntry(placeholder: "English Translation", fillWidth: true);
        var addBtn = GlassTheme.PillButton("Add", AppFontSizes.Body, accent: true, height: AppLayout.ButtonHeightCompact);
        addBtn.Clicked += (_, _) =>
        {
            var jp = _jpEntry.Text?.Trim();
            var eng = _engEntry.Text?.Trim();
            if (!string.IsNullOrEmpty(jp) && !string.IsNullOrEmpty(eng))
            {
                _viewModel.AddMapping(jp, eng);
                _jpEntry.Text = "";
                _engEntry.Text = "";
                _emptyLabel.IsVisible = _viewModel.IsEmpty;
            }
        };

        var cleanBtn = GlassTheme.PillButton("Clean & Reset Bad Tags", AppFontSizes.Body, height: AppLayout.ButtonHeightCompact);
        cleanBtn.Clicked += async (_, _) =>
        {
            try
            {
                await _viewModel.CleanAndResetAsync();
                _emptyLabel.IsVisible = _viewModel.IsEmpty;
                await DisplayAlert("Tags Cleaned", Services.TagCleanup.SuccessMessage, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tag clean failed");
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

                    // Star columns with minimum widths so the fields share the space and nothing overflows a phone.
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

        // Bindable row view so the list is virtualised instead of rebuilt view by view.
        var listView = new CollectionView
        {
            ItemsSource = _viewModel.Rows,
            ItemTemplate = new DataTemplate(() =>
            {
                var row = new TagMappingRowView(_logger)
                {
                    OnSave = (jp, eng) => _viewModel.SaveMappingAsync(jp, eng),
                    OnDelete = jp =>
                    {
                        _viewModel.RemoveMapping(jp);
                        _emptyLabel.IsVisible = _viewModel.IsEmpty;
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
        _viewModel.Load();
        _emptyLabel.IsVisible = _viewModel.IsEmpty;
    }

}

/// <summary>Gives the CollectionView an item type to bind to.</summary>
public sealed record TagMappingRow(string Key, string Value);
