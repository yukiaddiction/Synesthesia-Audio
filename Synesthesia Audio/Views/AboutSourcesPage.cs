using System.Text;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views;

/// <summary>
/// Opened from Settings, never a splash screen: the EDRDG licence requires attribution on a separate
/// menu-reached screen, and shipping stale name data without its date violates it.
/// </summary>
public sealed class AboutSourcesPage : ContentPage
{
    private readonly ILogger<AboutSourcesPage> _logger;

    public AboutSourcesPage(string version, string? nameDataCreated, ILogger<AboutSourcesPage> logger)
    {
        _logger = logger;
        Title = "About & Sources";

        var stack = new VerticalStackLayout
        {
            Padding = AppLayout.StandardPadding,
            Spacing = AppLayout.SpacingLarge
        };

        stack.Add(new Label
        {
            Text = "Synesthesia Audio",
            FontSize = AppFontSizes.SectionTitle,
            FontAttributes = AppFonts.Bold
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark));

        stack.Add(new Label
        {
            Text = $"Version {version}",
            FontSize = AppFontSizes.Body
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark));

        var dataLine = string.IsNullOrEmpty(nameDataCreated)
            ? "Name data: EDRDG JMnedict (creation date unavailable)"
            : $"Name data: EDRDG JMnedict, created {nameDataCreated}";

        stack.Add(new Label
        {
            Text = dataLine,
            FontSize = AppFontSizes.Body
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextFormLabelLight, AppColors.TextFormLabelDark));

        stack.Add(new Label
        {
            Text = "Name data is CC BY-SA 4.0 and is modified: only kanji to romanisation pairs for personal names are included.",
            FontSize = AppFontSizes.Caption
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark));

        var notices = new Label
        {
            Text = "Loading notices…",
            FontSize = AppFontSizes.Caption,
            LineBreakMode = LineBreakMode.WordWrap
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        stack.Add(notices);

        // Shown as a bare modal with no navigation bar, and desktop has no back gesture, so this is the only way out.
        var doneButton = new Button
        {
            Text = "Done",
            Margin = new Thickness(0, AppLayout.SpacingLarge, 0, 0)
        }
        .AppThemeBinding(Button.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
        .AppThemeBinding(Button.TextColorProperty, Colors.White, AppColors.BackgroundDark);

        doneButton.Clicked += async (_, _) => await CloseAsync();
        stack.Add(doneButton);

        Content = new ScrollView { Content = stack };

        _ = LoadNoticesAsync(notices);
    }

    /// <summary>Leaves the page whether it was opened modally or navigated to.</summary>
    private async Task CloseAsync()
    {
        try
        {
            var navigation = Navigation;
            if (navigation.ModalStack.Count > 0 && ReferenceEquals(navigation.ModalStack[^1], this))
            {
                await navigation.PopModalAsync();
                return;
            }

            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//Settings");
                return;
            }

            await navigation.PopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Closing the About page failed");
        }
    }

    private async Task LoadNoticesAsync(Label target)
    {
        try
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("LICENSES.txt");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            target.Text = await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            target.Text = $"Could not load the licence notices: {ex.Message}";
        }
    }
}
