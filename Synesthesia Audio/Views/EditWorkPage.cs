using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;

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

        this.AppThemeBinding(ContentPage.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);
        Title = "Edit Album Info";

        _titleEntry = new Entry { Text = work.Title, Placeholder = "Title" }
            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);
            
        _translatedTitleEntry = new Entry { Text = work.TranslatedTitle, Placeholder = "Translated Title" }
            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        _circleEntry = new Entry { Text = work.Circle, Placeholder = "Circle" }
            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        _vaEntry = new Entry { Text = work.VA, Placeholder = "Voice Actor (VA)" }
            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        _coverPathEntry = new Entry { Text = work.CoverImagePath, Placeholder = "Cover Image Path", IsReadOnly = true }
            .AppThemeBinding(Entry.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        _nsfwSwitch = new Switch { IsToggled = work.IsNsfw };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = AppLayout.StandardPadding,
                Spacing = AppLayout.SpacingSection,
                Children =
                {
                    new Label { Text = "Edit Album Details", FontSize = AppFontSizes.SectionTitle, FontAttributes = AppFonts.Bold, HorizontalTextAlignment = TextAlignment.Center }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                    new Label { Text = "Title", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),
                    _titleEntry,

                    new Label { Text = "Translated Title", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),
                    _translatedTitleEntry,

                    new Label { Text = "Circle / Maker", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),
                    _circleEntry,

                    new Label { Text = "Cast / Voice Actor", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),
                    _vaEntry,

                    new Label { Text = "Cover Image", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold }
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),
                    new Grid
                    {
                        ColumnDefinitions = Columns.Define(Star, Auto),
                        ColumnSpacing = 10,
                        Children =
                        {
                            _coverPathEntry.Column(0),
                            new Button { Text = "Browse", HeightRequest = 40 }
                                .Column(1)
                                .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark)
                                .Invoke(btn => btn.Clicked += OnBrowseCoverClicked)
                        }
                    },

                    new Grid
                    {
                        ColumnDefinitions = Columns.Define(Star, Auto),
                        Children =
                        {
                            new Label { Text = "Age Restriction (NSFW)", FontSize = AppFontSizes.BodyBold, FontAttributes = AppFonts.Bold, VerticalOptions = LayoutOptions.Center }
                                .Column(0)
                                .AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelAltDark),
                            _nsfwSwitch.Column(1)
                        }
                    },

                    new BoxView { HeightRequest = 1, Margin = new Thickness(0, 10) }
                        .AppThemeBinding(BoxView.ColorProperty, AppColors.StrokeLight, AppColors.StrokeDark),

                    new Grid
                    {
                        ColumnDefinitions = Columns.Define(Star, Star),
                        ColumnSpacing = 20,
                        Children =
                        {
                            new Button { Text = "Cancel", HeightRequest = AppLayout.ButtonHeightDefault }
                                .Column(0)
                                .AppThemeBinding(Button.TextColorProperty, AppColors.DangerTextLight, AppColors.DangerTextDark)
                                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.DangerBgLight, AppColors.DangerBgDark)
                                .Invoke(btn => btn.Clicked += OnCancelClicked),

                            new Button { Text = "Save Changes", HeightRequest = AppLayout.ButtonHeightDefault }
                                .Column(1)
                                .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.SuccessLight, AppColors.SuccessDark)
                                .Invoke(btn => btn.Clicked += OnSaveClicked)
                        }
                    }
                }
            }
        };
    }

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
