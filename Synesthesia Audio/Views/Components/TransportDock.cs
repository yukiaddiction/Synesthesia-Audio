using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// The desktop transport dock: now-playing cluster, transport with seek, and volume/options.
/// Only shown in the wide composition; the compact layout keeps <see cref="Views.FloatingPlayerBar"/>.
/// </summary>
public sealed class TransportDock : Border
{
    public TransportDock(AudioService audioService)
    {
        AutomationId = "TransportDock";
        VerticalOptions = LayoutOptions.End;
        Margin = new Thickness(16, 0, 16, 12);
        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassSheetRadius };
        Padding = new Thickness(14, 10);
        BindingContext = audioService;

        var nowPlaying = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingLarge,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Border
                {
                    StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassFooterRadius },
                    StrokeThickness = 0,
                    WidthRequest = 44,
                    HeightRequest = 44,
                    Padding = 0,
                    BackgroundColor = AppColors.CoverPlaceholder,
                    Content = new Image { Aspect = Aspect.AspectFill }
                        .Bind(Image.SourceProperty, $"{nameof(AudioService.CurrentTrack)}.{nameof(Track.CoverImagePath)}")
                },

                new VerticalStackLayout
                {
                    Spacing = 1,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label { FontSize = AppFontSizes.FloatingPlayerTitle, FontAttributes = AppFonts.Bold, LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
                            .Bind(Label.TextProperty, $"{nameof(AudioService.CurrentTrack)}.{nameof(Track.Title)}")
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                        new Label { FontSize = AppFontSizes.DetailText }
                            .Bind(Label.TextProperty, nameof(AudioService.PlaybackProgressText))
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                    }
                }
            }
        };

        var previous = GlassTheme.IconButton("\u23EE", AppFontSizes.ButtonLarge, 32)
            .Bind(Button.CommandProperty, nameof(AudioService.PreviousTrackCommand))
            .Invoke(b => b.AutomationId = "DockPreviousButton");
        ToolTipProperties.SetText(previous, "Previous track");

        var playPause = new Button
        {
            FontSize = AppFontSizes.Subtitle,
            WidthRequest = 42,
            HeightRequest = 42,
            CornerRadius = 21,
            Padding = 0
        }
        .Bind(Button.TextProperty, nameof(AudioService.IsPlaying), converter: new PlayPauseTextConverter())
        .Bind(Button.CommandProperty, nameof(AudioService.TogglePlayCommand))
        .GlassPill(accent: true);
        playPause.AutomationId = "DockPlayPauseButton";
        ToolTipProperties.SetText(playPause, "Play / Pause");

        var next = GlassTheme.IconButton("\u23ED", AppFontSizes.ButtonLarge, 32)
            .Bind(Button.CommandProperty, nameof(AudioService.NextTrackCommand))
            .Invoke(b => b.AutomationId = "DockNextButton");
        ToolTipProperties.SetText(next, "Next track");

        var transport = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingLarge,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { previous, playPause, next }
        };

        var seek = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            HeightRequest = 26,
            ThumbColor = Colors.Transparent,
            AutomationId = "DockSeekSlider"
        }
        .Bind(Slider.ValueProperty, nameof(AudioService.PlaybackProgress), BindingMode.OneWay)
        .AppThemeBinding(Slider.MinimumTrackColorProperty, AppColors.SuccessLight, AppColors.PrimaryDark)
        .AppThemeBinding(Slider.MaximumTrackColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark)
        .Invoke(s =>
        {
            s.DragStarted += (_, _) =>
            {
                if (audioService.BeginSeekCommand.CanExecute(null))
                    audioService.BeginSeekCommand.Execute(null);
            };
            s.DragCompleted += (_, _) =>
            {
                audioService.SeekProgress = s.Value;
                if (audioService.EndSeekCommand.CanExecute(null))
                    audioService.EndSeekCommand.Execute(null);
            };
        });

        var mute = GlassTheme.IconButton(string.Empty, AppFontSizes.ButtonMedium, 32)
            .Bind(Button.TextProperty, nameof(AudioService.VolumeIcon))
            .Bind(Button.CommandProperty, nameof(AudioService.ToggleMuteCommand))
            .Invoke(b => b.AutomationId = "DockMuteButton");
        ToolTipProperties.SetText(mute, "Mute");

        var volumeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            WidthRequest = 96,
            HeightRequest = 26,
            VerticalOptions = LayoutOptions.Center,
            ThumbColor = AppColors.PrimaryLight,
            AutomationId = "DockVolumeSlider"
        }
        .Bind(Slider.ValueProperty, nameof(AudioService.Volume), BindingMode.TwoWay)
        .AppThemeBinding(Slider.MinimumTrackColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
        .AppThemeBinding(Slider.MaximumTrackColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark);
        ToolTipProperties.SetText(volumeSlider, "Volume");

        var loop = GlassTheme.IconButton(string.Empty, AppFontSizes.ButtonSmall, 32)
            .Bind(Button.TextProperty, nameof(AudioService.LoopModeText))
            .Bind(Button.CommandProperty, nameof(AudioService.ToggleLoopCommand))
            .Invoke(b => b.AutomationId = "DockLoopButton")
            .Invoke(b => b.WidthRequest = 78);
        ToolTipProperties.SetText(loop, "Loop mode");

        var stop = GlassTheme.IconButton("\u2715", AppFontSizes.ButtonLarge, 32)
            .Bind(Button.CommandProperty, nameof(AudioService.StopCommand))
            .Invoke(b => b.AutomationId = "DockStopButton");
        ToolTipProperties.SetText(stop, "Stop");

        var volume = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingSmall,
            VerticalOptions = LayoutOptions.Center,
            Children = { mute, volumeSlider, loop, stop }
        };

        Content = new Grid
        {
            ColumnDefinitions = Columns.Define(new GridLength(280), Star, Auto),
            ColumnSpacing = AppLayout.SpacingSection,
            Children =
            {
                nowPlaying.Column(0),

                new Grid
                {
                    RowDefinitions = Rows.Define(Auto, Auto),
                    Children =
                    {
                        transport.Row(0),
                        seek.Row(1)
                    }
                }.Column(1),

                volume.Column(2)
            }
        };

        this.Glass(strong: true, elevated: true);
        this.Bind(VisualElement.IsVisibleProperty, nameof(AudioService.CurrentTrack), converter: new NotNullToTrueConverter());
    }
}
