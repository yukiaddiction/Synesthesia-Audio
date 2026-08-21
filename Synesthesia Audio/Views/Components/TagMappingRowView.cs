using System;
using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

public class TagMappingRowView : Border
{
    public TagMappingRowView(string jpKey, string engVal, Action<string, string> onSave, Action<string> onDelete)
    {
        var jpLabel = new Label
        {
            Text = jpKey,
            FontAttributes = AppFonts.Bold,
            FontSize = AppFontSizes.Body,
            VerticalOptions = LayoutOptions.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var engEntry = new Entry
        {
            Text = engVal,
            FontSize = AppFontSizes.Body,
            VerticalOptions = LayoutOptions.Center
        };

        var saveItemBtn = new Button
        {
            Text = "Save",
            FontSize = AppFontSizes.ButtonSmall,
            Padding = new Thickness(6, 0),
            HeightRequest = AppLayout.ButtonHeightSmall
        }
        .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.SuccessLight, AppColors.SuccessAccentDark);

        saveItemBtn.Clicked += (_, _) =>
        {
            var newEng = engEntry.Text?.Trim();
            if (!string.IsNullOrEmpty(newEng))
            {
                onSave(jpKey, newEng);
            }
        };

        var delBtn = new Button
        {
            Text = "✕",
            FontSize = AppFontSizes.ButtonMedium,
            Padding = 0,
            WidthRequest = AppLayout.ButtonHeightSmall,
            HeightRequest = AppLayout.ButtonHeightSmall
        }
        .AppThemeBinding(Button.TextColorProperty, Colors.White, Colors.White)
        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.DangerLight, AppColors.DangerDark);

        delBtn.Clicked += (_, _) => onDelete(jpKey);

        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.ButtonCornerRadius };
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

        this.AppThemeBinding(Border.BackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceDark);
        this.AppThemeBinding(Border.StrokeProperty, AppColors.StrokeLight, AppColors.StrokeDark);
    }
}
