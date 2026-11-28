using CommunityToolkit.Maui.Markup;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views.Components;

/// <summary>Transport buttons and seek bar shared by the floating bar and the dock so the two can't drift apart.</summary>
public static class TransportControls
{
    /// <param name="idPrefix">Keeps automation ids distinct so UI tests can tell the two bars apart.</param>
    public static HorizontalStackLayout BuildButtons(string idPrefix, double spacing, double playButtonSize)
    {
        var previous = GlassTheme.IconButton("\u23EE", AppFontSizes.ButtonLarge, 32)
            .Bind(Button.CommandProperty, nameof(AudioService.PreviousTrackCommand))
            .Invoke(b => b.AutomationId = idPrefix + "PreviousButton")
            .Invoke(b => SemanticProperties.SetDescription(b, "Previous track"));
        ToolTipProperties.SetText(previous, "Previous track");

        var playPause = new Button
        {
            FontSize = AppFontSizes.Subtitle,
            WidthRequest = playButtonSize,
            HeightRequest = playButtonSize,
            CornerRadius = (int)(playButtonSize / 2),
            Padding = 0
        }
        .Bind(Button.TextProperty, nameof(AudioService.IsPlaying), converter: new Converters.PlayPauseTextConverter())
        .Bind(Button.CommandProperty, nameof(AudioService.TogglePlayCommand))
        // Bound so a screen reader doesn't announce "Play" while already playing.
        .Bind(SemanticProperties.DescriptionProperty, nameof(AudioService.PlayPauseAccessibleName))
        .GlassPill(accent: true);
        playPause.AutomationId = idPrefix + "PlayPauseButton";
        ToolTipProperties.SetText(playPause, "Play / Pause");

        var next = GlassTheme.IconButton("\u23ED", AppFontSizes.ButtonLarge, 32)
            .Bind(Button.CommandProperty, nameof(AudioService.NextTrackCommand))
            .Invoke(b => b.AutomationId = idPrefix + "NextButton")
            .Invoke(b => SemanticProperties.SetDescription(b, "Next track"));
        ToolTipProperties.SetText(next, "Next track");

        return new HorizontalStackLayout
        {
            Spacing = spacing,
            VerticalOptions = LayoutOptions.Center,
            Children = { previous, playPause, next }
        };
    }
}
