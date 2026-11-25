using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Views.Components;

namespace Synesthesia_Audio.Views;

public class EditWorkPage : ContentPage
{
    private readonly WorkMetadata _work;
    private readonly LibraryCacheService _cacheService;
    private readonly Action _onSaved;

    private readonly Entry _titleEntry;
    private readonly Entry _translatedTitleEntry;
    private readonly Entry _circleEntry;
    private readonly Entry _vaEntry;
    private readonly Entry _coverPathEntry;
    private readonly Switch _nsfwSwitch;
    private bool _isSaving;

    public EditWorkPage(WorkMetadata work, LibraryCacheService cacheService, Action onSaved)
    {
        _work = work;
        _cacheService = cacheService;
        _onSaved = onSaved;

        Title = "Edit Album Info";
        Shell.SetNavBarIsVisible(this, false);

        _titleEntry = CreateEntry(work.Title, "Title");
        _translatedTitleEntry = CreateEntry(work.TranslatedTitle, "Translated Title");
        _circleEntry = CreateEntry(work.Circle, "Circle");
        _vaEntry = CreateEntry(work.VA, "Voice Actor (VA)");
        _coverPathEntry = CreateEntry(work.CoverImagePath, "Cover Image Path");
        _coverPathEntry.IsReadOnly = true;

        _nsfwSwitch = new Switch { IsToggled = work.IsNsfw }
            .Invoke(s => SemanticProperties.SetDescription(s, "Adult content"))
            .GlassSwitch();

        var actions = new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Star),
            ColumnSpacing = 20,
            Children =
            {
                new Button { Text = "Cancel", HeightRequest = AppLayout.ButtonHeightDefault, CornerRadius = 22, BorderWidth = 1 }
                    .Column(0)
                    .AppThemeBinding(Button.TextColorProperty, AppColors.DangerTextLight, AppColors.DangerTextDark)
                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.DangerBgLight, AppColors.DangerBgDark)
                    .AppThemeBinding(Button.BorderColorProperty, Colors.Transparent, Colors.Transparent)
                    .Invoke(btn => btn.Clicked += OnCancelClicked),

                GlassTheme.PillButton("Save Changes", AppFontSizes.BodyBold, accent: true, height: AppLayout.ButtonHeightDefault)
                    .Column(1)
                    .Invoke(btn => btn.Clicked += OnSaveClicked)
            }
        };

        var sheet = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassSheetRadius },
            Padding = new Thickness(20),
            Content = new VerticalStackLayout
            {
                Spacing = AppLayout.SpacingSection,
                Children =
                {
                    CreateFormLabel("Title"),
                    _titleEntry,

                    CreateFormLabel("Translated Title"),
                    _translatedTitleEntry,

                    CreateFormLabel("Circle / Maker"),
                    _circleEntry,

                    CreateFormLabel("Cast / Voice Actor"),
                    _vaEntry,

                    CreateFormLabel("Cover Image"),
                    new Grid
                    {
                        ColumnDefinitions = Columns.Define(Star, Auto),
                        ColumnSpacing = AppLayout.SpacingExtraLarge,
                        Children =
                        {
                            _coverPathEntry.Column(0),
                            GlassTheme.PillButton("Browse", AppFontSizes.Body)
                                .Column(1)
                                .Invoke(btn => btn.Clicked += OnBrowseCoverClicked)
                        }
                    },

                    new Grid
                    {
                        ColumnDefinitions = Columns.Define(Star, Auto),
                        Children =
                        {
                            CreateFormLabel("Age Restriction (NSFW)")
                                .Column(0)
                                .Invoke(l => l.VerticalOptions = LayoutOptions.Center),
                            _nsfwSwitch.Column(1)
                        }
                    },

                    new BoxView { HeightRequest = 1 }
                        .AppThemeBinding(BoxView.ColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark),

                    actions
                }
            }
        }.Glass(strong: true, elevated: true);

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = AppLayout.GlassContentPadding,
                Children = { sheet }
            }
        };

        var scaffold = new GlassScaffold("Edit Album Info", menu: false);
        scaffold.SetBody(scroll);

        Content = scaffold;
    }

    private static Entry CreateEntry(string? text, string placeholder) =>
        GlassTheme.FormEntry(text, placeholder);

    private static Label CreateFormLabel(string text) => GlassTheme.FormLabel(text);

    private async void OnBrowseCoverClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Cover Image",
                FileTypes = FilePickerFileType.Images
            });
            if (result != null)
            {
                _coverPathEntry.Text = result.FullPath;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not pick image: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        try
        {
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<EditWorkPage>().LogWarning(ex, "Cancel navigation failed");
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_isSaving) return;
        try
        {
            _isSaving = true;
            if (string.IsNullOrWhiteSpace(_titleEntry.Text))
            {
                await DisplayAlert("Validation", "Title cannot be empty.", "OK");
                return;
            }

            _work.Title = _titleEntry.Text.Trim();
            _work.TranslatedTitle = _translatedTitleEntry.Text?.Trim() ?? string.Empty;
            _work.Circle = _circleEntry.Text?.Trim() ?? string.Empty;
            _work.VA = _vaEntry.Text?.Trim() ?? string.Empty;
            _work.CoverImagePath = _coverPathEntry.Text?.Trim() ?? string.Empty;
            _work.AgeRating = _nsfwSwitch.IsToggled ? "NSFW" : "SFW";

            _cacheService.UpdateWork(_work);
            _onSaved();

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<EditWorkPage>().LogError(ex, "Saving work edits failed");
            await DisplayAlert("Error", $"Could not save changes: {ex.Message}", "OK");
        }
        finally
        {
            _isSaving = false;
        }
    }
}
