using System;
using CommunityToolkit.Maui.Markup;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// One row of the tag-mapping editor.
///
/// The inputs come from bindable properties rather than constructor arguments so a
/// <see cref="CollectionView"/> can recycle the view for a different mapping. That is what lets the page
/// virtualise the list instead of building a view per row up front (PERF-09).
/// </summary>
public class TagMappingRowView : Border
{
    public static readonly BindableProperty JpKeyProperty = BindableProperty.Create(
        nameof(JpKey), typeof(string), typeof(TagMappingRowView), string.Empty,
        propertyChanged: (b, _, _) => ((TagMappingRowView)b).SyncToBindings());

    public static readonly BindableProperty EngValueProperty = BindableProperty.Create(
        nameof(EngValue), typeof(string), typeof(TagMappingRowView), string.Empty,
        propertyChanged: (b, _, _) => ((TagMappingRowView)b).SyncToBindings());

    public string JpKey
    {
        get => (string)GetValue(JpKeyProperty);
        set => SetValue(JpKeyProperty, value);
    }

    public string EngValue
    {
        get => (string)GetValue(EngValueProperty);
        set => SetValue(EngValueProperty, value);
    }

    /// <summary>Called with the typed key and value when the row is saved.</summary>
    public Action<string, string>? OnSave { get; set; }

    /// <summary>Called with the key after the user confirms deletion.</summary>
    public Action<string>? OnDelete { get; set; }

    private readonly Label _jpLabel;
    private readonly Entry _engEntry;

    public TagMappingRowView()
    {
        _jpLabel = new Label
        {
            FontAttributes = AppFonts.Bold,
            FontSize = AppFontSizes.Body,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        }
        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        _engEntry = new Entry
        {
            FontSize = AppFontSizes.Body,
            VerticalOptions = LayoutOptions.Center
        }
        .AppThemeBinding(Entry.TextColorProperty, AppColors.TextFormInputLight, AppColors.TextFormInputDark)
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.GlassFillSoftLight, AppColors.GlassFillSoftDark);

        var saveItemBtn = GlassTheme.PillButton("Save", AppFontSizes.ButtonSmall, accent: true, height: AppLayout.ButtonHeightSmall);
        saveItemBtn.Clicked += (_, _) =>
        {
            var newEng = _engEntry.Text?.Trim();
            if (!string.IsNullOrEmpty(newEng)) OnSave?.Invoke(JpKey, newEng);
        };

        var delBtn = GlassTheme.IconButton("\u2715", AppFontSizes.ButtonMedium, AppLayout.ButtonHeightSmall);
        delBtn.Clicked += async (_, _) =>
        {
            // Deleting used to be a single tap with no undo (UX-05).
            if (await ConfirmDeleteAsync(JpKey)) OnDelete?.Invoke(JpKey);
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
                _jpLabel.Column(0),
                _engEntry.Column(1),
                saveItemBtn.Column(2),
                delBtn.Column(3)
            }
        };

        this.GlassSoft();
    }

    /// <summary>
    /// Pushes the bound values into the controls. A recycled view keeps its entry, so the text is only replaced
    /// when it actually differs - otherwise typing would be interrupted by a rebind.
    /// </summary>
    private void SyncToBindings()
    {
        _jpLabel.Text = JpKey;

        var current = _engEntry.Text ?? string.Empty;
        if (!string.Equals(current, EngValue ?? string.Empty, StringComparison.Ordinal))
            _engEntry.Text = EngValue ?? string.Empty;
    }

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
}
