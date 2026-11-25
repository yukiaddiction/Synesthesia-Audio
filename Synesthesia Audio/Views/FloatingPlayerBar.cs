using CommunityToolkit.Maui.Markup;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Models;
using Synesthesia_Audio.Services;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Converters;
using System.Collections.Generic;

namespace Synesthesia_Audio.Views;

public class FloatingPlayerBar : Border
{
    public FloatingPlayerBar(AudioService audioService)
    {
        AutomationId = "FloatingPlayerBar";
        VerticalOptions = LayoutOptions.End;
        Margin = AppLayout.FloatingPlayerMargin;
        StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassSheetRadius };
        Padding = AppLayout.FloatingPlayerPadding;
        BindingContext = audioService;

        var transport = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingMedium,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                GlassTheme.IconButton("\u23EE", AppFontSizes.ButtonLarge, 32)
                    .Bind(Button.CommandProperty, nameof(AudioService.PreviousTrackCommand))
                    .Invoke(b => b.AutomationId = "PreviousTrackButton")
                    .Invoke(b => SemanticProperties.SetDescription(b, "Previous Track")),

                new Button
                {
                    FontSize = AppFontSizes.Subtitle,
                    WidthRequest = 40,
                    HeightRequest = 40,
                    CornerRadius = 20,
                    Padding = 0
                }
                .Bind(Button.TextProperty, nameof(AudioService.IsPlaying), converter: new PlayPauseTextConverter())
                .Bind(Button.CommandProperty, nameof(AudioService.TogglePlayCommand))
                .Invoke(b => b.AutomationId = "PlayPauseButton")
                .Bind(SemanticProperties.DescriptionProperty, nameof(AudioService.PlayPauseAccessibleName))
                .GlassPill(accent: true),

                GlassTheme.IconButton("\u23ED", AppFontSizes.ButtonLarge, 32)
                    .Bind(Button.CommandProperty, nameof(AudioService.NextTrackCommand))
                    .Invoke(b => b.AutomationId = "NextTrackButton")
                    .Invoke(b => SemanticProperties.SetDescription(b, "Next Track"))
            }
        };

        var metadata = new VerticalStackLayout
        {
            Spacing = AppLayout.SpacingSmall,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { FontSize = AppFontSizes.FloatingPlayerTitle, FontAttributes = AppFonts.Bold, LineBreakMode = LineBreakMode.TailTruncation }
                    .Bind(Label.TextProperty, $"{nameof(AudioService.CurrentTrack)}.{nameof(Track.Title)}")
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark),

                new Label { FontSize = AppFontSizes.DetailText }
                    .Bind(Label.TextProperty, nameof(AudioService.PlaybackProgressText))
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark),

                new HorizontalStackLayout
                {
                    Spacing = 6,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new ActivityIndicator { HeightRequest = 14, WidthRequest = 14, Color = AppColors.PrimaryLight }
                            .Bind(ActivityIndicator.IsRunningProperty, nameof(AudioService.IsBuffering))
                            .Bind(VisualElement.IsVisibleProperty, nameof(AudioService.IsBuffering)),

                        new Label { Text = "Buffering...", FontSize = AppFontSizes.Caption }
                            .Bind(VisualElement.IsVisibleProperty, nameof(AudioService.IsBuffering))
                            .AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark)
                    }
                }
            }
        };

        var options = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingSmall,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                GlassTheme.IconButton("\uD83C\uDFAC", AppFontSizes.ButtonMedium, 32)
                    .Bind(Button.CommandProperty, nameof(AudioService.ToggleVideoVisibilityCommand))
                    .Bind(VisualElement.IsVisibleProperty, nameof(AudioService.IsVideoTrack))
                    .Invoke(b => b.AutomationId = "VideoToggleButton")
                    .Invoke(b => SemanticProperties.SetDescription(b, "Toggle Video Overlay")),

                GlassTheme.IconButton("\u26F6", AppFontSizes.ButtonMedium, 32)
                    .Bind(Button.CommandProperty, nameof(AudioService.ToggleFullScreenCommand))
                    .Bind(VisualElement.IsVisibleProperty, nameof(AudioService.IsVideoTrack))
                    .Invoke(b => b.AutomationId = "FullScreenButton")
                    .Invoke(b => SemanticProperties.SetDescription(b, "Toggle Full Screen")),

                GlassTheme.IconButton(string.Empty, AppFontSizes.ButtonMedium, 32)
                    .Bind(Button.TextProperty, nameof(AudioService.LoopModeText))
                    .Bind(Button.CommandProperty, nameof(AudioService.ToggleLoopCommand))
                    .Invoke(b => b.AutomationId = "LoopModeButton")
                    .Invoke(b => SemanticProperties.SetDescription(b, "Toggle Loop Mode")),

                GlassTheme.IconButton("\u2715", AppFontSizes.ButtonLarge, 32)
                    .Bind(Button.CommandProperty, nameof(AudioService.StopCommand))
                    .Invoke(b => b.AutomationId = "StopButton")
                    .Invoke(b => SemanticProperties.SetDescription(b, "Stop Playback"))
            }
        };

        Content = new Grid
        {
            RowDefinitions = Rows.Define(Star, Auto),
            Children =
            {
                new Grid
                {
                    ColumnDefinitions = Columns.Define(Auto, Star, Auto),
                    ColumnSpacing = AppLayout.SpacingSection,
                    Children =
                    {
                        transport.Column(0),
                        metadata.Column(1),
                        options.Column(2)
                    }
                }.Row(0),

                new Slider
                {
                    Minimum = 0,
                    Maximum = 1,
                    HeightRequest = 30,
                    Margin = AppLayout.FloatingPlayerSliderMargin,
                    ThumbColor = Colors.Transparent,
                    AutomationId = "SeekSlider"
                }
                .Row(1)
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
                })
            }
        };

        this.Glass(strong: true, elevated: true);
        this.Bind(VisualElement.IsVisibleProperty, nameof(AudioService.CurrentTrack), converter: new NotNullToTrueConverter());

        GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try
                {
                    var folderPath = audioService.CurrentAlbumFolderPath;
                    if (string.IsNullOrEmpty(folderPath)) return;
                    var cache = ServiceAccessor.GetService<LibraryCacheService>();
                    var work = cache?.GetWork(folderPath);
                    if (work != null)
                    {
                        // Guarded: clicking the bar repeatedly used to stack copies of the album page.
                        await NavigationGuard.RunAsync(() =>
                            Shell.Current.GoToAsync(nameof(AlbumPage), new Dictionary<string, object> { { "Album", work } }));
                    }
                }
                catch { }
            })
        });
    }
}
