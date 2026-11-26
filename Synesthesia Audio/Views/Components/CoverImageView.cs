using CommunityToolkit.Maui.Markup;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// A cover image with its "NO COVER" placeholder, in one place.
///
/// The card, the album hero and the preview pane each built the same two-element stack - a placeholder label
/// swapped for an image depending on whether a path exists - with slightly different sizes and no shared
/// thumbnail rule (DUP-07). The thumbnail edge is a parameter here, so every surface states which size it wants.
/// </summary>
public static class CoverImageView
{
    /// <summary>
    /// Builds the cover view. <paramref name="pathBinding"/> is the binding expression for the cover path, so the
    /// same component can bind to a work on a card or to the selected work in a preview pane.
    /// </summary>
    /// <param name="placeholderContent">What to show when there is no cover. Defaults to a "NO COVER" label.</param>
    /// <param name="overlay">An element drawn on top of the image, such as the hero scrim.</param>
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

    /// <summary>The card-sized cover, which is the most common case.</summary>
    public static View Card(string pathBinding) =>
        Create(pathBinding, CoverThumbnails.CardEdge, AppLayout.GlassCardCoverHeight);
}
