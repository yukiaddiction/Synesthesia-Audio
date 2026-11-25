using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Services.Cloud;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Views.Components;

namespace Synesthesia_Audio.Views;

public class CloudFolderPickerPage : ContentPage
{
    private readonly ICloudProvider _cloudProvider;
    private readonly WebDavConnection? _connection;
    private readonly Action<string> _onFolderSelected;
    private string _currentPath;
    private readonly ObservableCollection<CloudFileInfo> _directories = new();
    private readonly Label _pathLabel;
    private readonly Button _upButton;
    private readonly ActivityIndicator _loader;
    private readonly Button _selectButton;

    public CloudFolderPickerPage(ICloudProvider cloudProvider, WebDavConnection? connection, string startPath, Action<string> onFolderSelected)
    {
        _cloudProvider = cloudProvider;
        _connection = connection;
        _onFolderSelected = onFolderSelected;

        _currentPath = (startPath ?? string.Empty).Replace('\\', '/').Trim('/');

        Title = "Choose Cloud Folder";
        this.AppThemeBinding(ContentPage.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);

        var cancelBtn = new ToolbarItem { Text = "Cancel" };
        cancelBtn.Clicked += async (_, _) =>
        {
            try { await Navigation.PopModalAsync(); }
            catch { }
        };
        ToolbarItems.Add(cancelBtn);

        _pathLabel = new Label
        {
            FontAttributes = AppFonts.Bold,
            FontSize = AppFontSizes.PathLabel,
            LineBreakMode = LineBreakMode.MiddleTruncation,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        _upButton = GlassTheme.PillButton("\u2B06 Up", AppFontSizes.ButtonSmall, height: AppLayout.ButtonHeightSmall);
        _upButton.Clicked += OnUpClicked;

        _loader = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            Color = AppColors.PrimaryLight,
            HeightRequest = 30,
            WidthRequest = 30
        };

        var collectionView = new CollectionView
        {
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical),
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var icon = new Label
                {
                    Text = "\uD83D\uDCC1",
                    FontSize = AppFontSizes.GroupTitle,
                    VerticalOptions = LayoutOptions.Center
                };

                var label = new Label
                {
                    VerticalOptions = LayoutOptions.Center,
                    FontSize = AppFontSizes.Subtitle,
                    LineBreakMode = LineBreakMode.TailTruncation
                }.AppThemeBinding(Label.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark);
                label.SetBinding(Label.TextProperty, "Name");

                return new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
                    Margin = new Thickness(16, 3),
                    Padding = new Thickness(14, 12),
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children = { icon, label }
                    }
                }.GlassSoft();
            })
        };
        collectionView.SelectionChanged += OnDirectorySelected;
        collectionView.ItemsSource = _directories;

        _selectButton = GlassTheme.PillButton("Select This Folder", AppFontSizes.Body, accent: true, height: AppLayout.ButtonHeightDefault);
        _selectButton.Clicked += OnSelectClicked;
        _selectButton.Margin = new Thickness(16, 10);

        var pathHeader = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius },
            Margin = new Thickness(16, 4, 16, 8),
            Padding = AppLayout.PathHeaderPadding,
            Content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8,
                Children =
                {
                    _upButton,
                    _pathLabel.Column(1)
                }
            }
        }.Glass();

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                pathHeader.Row(0),

                new Grid
                {
                    Children = { collectionView, _loader }
                }.Row(1),

                _selectButton.Row(2)
            }
        };

        var backdrop = new GlassBackdrop();
        backdrop.AppThemeBinding(GlassBackdrop.IsDarkProperty, false, true);

        Content = new Grid
        {
            Children = { backdrop, content }
        };

        _ = LoadDirectoriesAsync();
    }

    private async Task LoadDirectoriesAsync()
    {
        _loader.IsRunning = true;
        _loader.IsVisible = true;
        _directories.Clear();

        _upButton.IsEnabled = !string.IsNullOrEmpty(_currentPath);
        _pathLabel.Text = string.IsNullOrEmpty(_currentPath) ? "Root ( / )" : "/" + _currentPath;

        try
        {
            // Every level re-uses the connection captured when the picker opened, so a user who has not
            // saved yet can still browse several levels deep.
            var files = await _cloudProvider.ListFilesAsync(_currentPath, _connection);
            var dirs = files.Where(f => f.IsDirectory).OrderBy(d => d.Name).ToList();

            foreach (var dir in dirs)
                _directories.Add(dir);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to load folders: {ex.Message}", "OK");
        }
        finally
        {
            _loader.IsRunning = false;
            _loader.IsVisible = false;
        }
    }

    private async void OnDirectorySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CloudFileInfo dir) return;
        try
        {
            _currentPath = dir.Path.Replace('\\', '/').Trim('/');
            await LoadDirectoriesAsync();
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<CloudFolderPickerPage>().LogError(ex, "Directory navigation failed");
        }
    }

    private async void OnUpClicked(object? sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentPath)) return;
            var normalized = _currentPath.Replace('\\', '/').TrimEnd('/');
            var lastSlash = normalized.LastIndexOf('/');
            _currentPath = lastSlash >= 0 ? normalized.Substring(0, lastSlash) : string.Empty;
            await LoadDirectoriesAsync();
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<CloudFolderPickerPage>().LogError(ex, "Up navigation failed");
        }
    }

    private async void OnSelectClicked(object? sender, EventArgs e)
    {
        try
        {
            var resultPath = string.IsNullOrEmpty(_currentPath) ? "/" : "/" + _currentPath;
            _onFolderSelected(resultPath);
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<CloudFolderPickerPage>().LogError(ex, "Folder selection failed");
        }
    }
}
