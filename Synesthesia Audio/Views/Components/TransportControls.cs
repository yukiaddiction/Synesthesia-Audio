using CommunityToolkit.Maui.Markup;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// The transport buttons and the seek bar, built once for both places that show them.
///
/// The floating bar and the dock each spelled out their own previous / play-pause / next buttons and their own
/// seek slider with its drag handlers. That is four copies of the same wiring, and it showed: the two bars had
/// different sizes, different automation ids and slightly different accessibility text, and a fix to one (the
/// seek thumb, the state-bound play/pause name) had to be made twice or was missed in one (DUP-06).
/// </summary>
public static class TransportControls
{
    /// <summary>
    /// Previous / play-pause / next. <paramref name="idPrefix"/> keeps the automation ids distinct so UI tests can
    /// tell the two bars apart.
    /// </summary>
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
        // Bound, not a fixed string: the name has to follow the state or a screen reader says "Play" while it plays.
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

    /// <summary>
    /// The seek bar, with its drag handlers wired to the audio service's begin/end seek commands.
    /// </summary>
    public static Slider BuildSeekBar(AudioService audioService, string automationId)
    {
        var seek = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            // 44 px is the smallest comfortable touch target, and a visible thumb is what makes it grabbable
            // (UX-03).
            HeightRequest = 44,
            AutomationId = automationId
        }
        .Invoke(s => SemanticProperties.SetDescription(s, "Playback position"))
        .Invoke(s => SemanticProperties.SetHint(s, "Drag to seek within the track"))
        .Bind(Slider.ValueProperty, nameof(AudioService.PlaybackProgress), BindingMode.OneWay)
        .AppThemeBinding(Slider.ThumbColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
        .AppThemeBinding(Slider.MinimumTrackColorProperty, AppColors.SuccessLight, AppColors.PrimaryDark)
        .AppThemeBinding(Slider.MaximumTrackColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark);

        seek.DragStarted += (_, _) =>
        {
            if (audioService.BeginSeekCommand.CanExecute(null))
                audioService.BeginSeekCommand.Execute(null);
        };

        seek.DragCompleted += (_, _) =>
        {
            audioService.SeekProgress = seek.Value;
            if (audioService.EndSeekCommand.CanExecute(null))
                audioService.EndSeekCommand.Execute(null);
        };

        return seek;
    }
}
