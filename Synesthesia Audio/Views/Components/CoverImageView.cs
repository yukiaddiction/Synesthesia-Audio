using CommunityToolkit.Maui.Markup;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// Cover image with its placeholder, shared by card, hero and preview pane. The thumbnail edge is a parameter
/// so each surface states which size it decodes.
/// </summary>
public static class CoverImageView
{
    /// <param name="pathBinding">A binding path, so the same view can bind to a card's work or the selected work.</param>
    public static Grid Create(string pathBinding, int thumbnailEdge, double? heightRequest = null,
        Aspect aspect = Aspect.AspectFill, View? placeholderContent = null, View? overlay = null)
    {
        var placeholder = new Grid
        {
            BackgroundColor = AppColors.CoverPlaceholder,
            Children =
            {
                placeholderContent ?? new Label
                {
                    Text = "NO COVER",
                    FontSize = AppFontSizes.SmallText,
                    FontAttributes = AppFonts.Bold,
                    TextColor = AppColors.CoverPlaceholderText,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            }
        }
        .Bind(VisualElement.IsVisibleProperty, pathBinding, converter: new Converters.EmptyStringToTrueConverter());

        var image = new Image { Aspect = aspect }
            .Bind(Image.SourceProperty, pathBinding, converter: new Converters.CoverThumbnailConverter { Edge = thumbnailEdge })
            .Bind(VisualElement.IsVisibleProperty, pathBinding, converter: new Converters.EmptyStringToFalseConverter());

        if (heightRequest is { } height)
        {
            placeholder.HeightRequest = height;
            image.HeightRequest = height;
        }

        var grid = new Grid { Children = { placeholder, image } };

        if (overlay != null) grid.Children.Add(overlay);

        return grid;
    }

    public static View Card(string pathBinding) =>
        Create(pathBinding, CoverThumbnails.CardEdge, AppLayout.GlassCardCoverHeight);
}
