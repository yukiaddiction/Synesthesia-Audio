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

    public static Slider BuildSeekBar(AudioService audioService, string automationId)
    {
        var seek = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            // 44 px is the minimum comfortable touch target.
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
