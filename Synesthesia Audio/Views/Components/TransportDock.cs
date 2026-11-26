using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Converters;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

using Synesthesia_Audio.Views.Components;

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
                        .Bind(Image.SourceProperty, $"{nameof(AudioService.CurrentTrack)}.{nameof(Track.CoverImagePath)}",
                            converter: new CoverThumbnailConverter { Edge = Synesthesia_Audio.Core.CoverThumbnails.SmallEdge })
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

        var transport = TransportControls.BuildButtons("Dock", AppLayout.SpacingLarge, playButtonSize: 42);

        var seek = TransportControls.BuildSeekBar(audioService, "DockSeekSlider");

        var mute = GlassTheme.IconButton(string.Empty, AppFontSizes.ButtonMedium, 32)
            .Bind(Button.TextProperty, nameof(AudioService.VolumeIcon))
            .Bind(Button.CommandProperty, nameof(AudioService.ToggleMuteCommand))
            .Invoke(b => b.AutomationId = "DockMuteButton");
        ToolTipProperties.SetText(mute, "Mute");
        SemanticProperties.SetDescription(mute, "Mute");
        SemanticProperties.SetHint(mute, "Toggles audio output on and off");

        var volumeSlider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            WidthRequest = 96,
            HeightRequest = 26,
            VerticalOptions = LayoutOptions.Center,
            AutomationId = "DockVolumeSlider"
        }
        // Theme-bound: the fixed light accent stayed pale against the dark panel (UX-01).
        .AppThemeBinding(Slider.ThumbColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
        .Bind(Slider.ValueProperty, nameof(AudioService.Volume), BindingMode.TwoWay)
        .AppThemeBinding(Slider.MinimumTrackColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
        .AppThemeBinding(Slider.MaximumTrackColorProperty, AppColors.GlassStrokeEdgeLight, AppColors.GlassStrokeEdgeDark);
        ToolTipProperties.SetText(volumeSlider, "Volume");
        SemanticProperties.SetDescription(volumeSlider, "Volume");
        SemanticProperties.SetHint(volumeSlider, "Adjusts playback volume");

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
