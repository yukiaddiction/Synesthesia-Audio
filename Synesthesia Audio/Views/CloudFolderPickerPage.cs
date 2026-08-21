using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Services.Cloud;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views;

public class CloudFolderPickerPage : ContentPage
{
    private readonly ICloudProvider _cloudProvider;
    private readonly Action<string> _onFolderSelected;
    private string _currentPath;
    private readonly ObservableCollection<CloudFileInfo> _directories = new();
    private readonly Label _pathLabel;
    private readonly Button _upButton;
    private readonly ActivityIndicator _loader;
    private readonly Button _selectButton;

    public CloudFolderPickerPage(ICloudProvider cloudProvider, string startPath, Action<string> onFolderSelected)
    {
        _cloudProvider = cloudProvider;
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

        _upButton = new Button
        {
            Text = "⬆ Up",
            FontSize = AppFontSizes.ButtonSmall,
            HeightRequest = AppLayout.ButtonHeightSmall
        }.AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
         .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark);

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
                    Text = "📁",
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

                var row = new HorizontalStackLayout
                {
                    Spacing = 10,
                    Padding = new Thickness(16, 14),
                    Children = { icon, label }
                };
                return row;
            })
        };
        collectionView.SelectionChanged += OnDirectorySelected;
        collectionView.ItemsSource = _directories;

        _selectButton = new Button
        {
            Text = "Select This Folder",
            FontSize = AppFontSizes.Body,
            HeightRequest = AppLayout.ButtonHeightDefault,
            Margin = new Thickness(20, 10),
            HorizontalOptions = LayoutOptions.Fill
        }.AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
         .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark);
        _selectButton.Clicked += OnSelectClicked;

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Children =
            {
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 8,
                    Padding = AppLayout.PathHeaderPadding,
                    Children =
                    {
                        _upButton,
                        _pathLabel.Column(1)
                    }
                }.AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.PickerBgDark),

                new Grid
                {
                    Children = { collectionView, _loader }
                }.Row(1),

                _selectButton.Row(2)
            }
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
            var files = await _cloudProvider.ListFilesAsync(_currentPath);
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
