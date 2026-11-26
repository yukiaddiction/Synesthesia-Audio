using System;
using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Views.Components;

public class TagMappingRowView : Border
{
    /// <summary>
    /// Asks before deleting a mapping. Deleting used to be a single tap with no undo (UX-05).
    /// </summary>
    private static async Task<bool> ConfirmDeleteAsync(string jpKey)
    {
        var page = Shell.Current;
        if (page == null) return false;

        try
        {
            return await page.DisplayAlert("Delete mapping", $"Remove the mapping for “{jpKey}”?", "Delete", "Cancel");
        }
        catch (Exception ex)
        {
            Services.Logging.AppLog.Get<TagMappingRowView>().LogDebug(ex, "Delete confirmation failed");
            return false;
        }
    }

    public TagMappingRowView(string jpKey, string engVal, Action<string, string> onSave, Action<string> onDelete)
    {
        var jpLabel = new Label
        {
            Text = jpKey,
            FontAttributes = AppFonts.Bold,
            FontSize = AppFontSizes.Body,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        }
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        var engEntry = new Entry
        {
            Text = engVal,
            FontSize = AppFontSizes.Body,
            VerticalOptions = LayoutOptions.Center
        }
        .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);

        var saveItemBtn = GlassTheme.PillButton("Save", AppFontSizes.ButtonSmall, accent: true, height: AppLayout.ButtonHeightSmall);

        saveItemBtn.Clicked += (_, _) =>
        {
            var newEng = engEntry.Text?.Trim();
            if (!string.IsNullOrEmpty(newEng))
            {
                onSave(jpKey, newEng);
            }
        };

        var delBtn = GlassTheme.IconButton("\u2715", AppFontSizes.ButtonMedium, AppLayout.ButtonHeightSmall);
        // One tap used to delete a mapping irreversibly (UX-05).
        delBtn.Clicked += async (_, _) =>
        {
            var confirmed = await ConfirmDeleteAsync(jpKey);
            if (confirmed) onDelete(jpKey);
        };
        SemanticProperties.SetDescription(delBtn, "Delete mapping");
        SemanticProperties.SetHint(delBtn, "Asks for confirmation first");

        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassChipRadius };
        StrokeThickness = 0;
        Padding = AppLayout.TagRowPadding;
        Content = new Grid
        {
            ColumnDefinitions = Columns.Define(Star, Star, Auto, Auto),
            ColumnSpacing = AppLayout.SpacingLarge,
            Children =
            {
                jpLabel.Column(0),
                engEntry.Column(1),
                saveItemBtn.Column(2),
                delBtn.Column(3)
            }
        };

        this.GlassSoft();
    }
}
