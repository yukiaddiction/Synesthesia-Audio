using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// A seek control drawn as a quiet waveform: the played portion is tinted, the rest stays ghosted.
/// The invisible slider on top keeps the native drag/seek behaviour and the 44 px touch target.
/// </summary>
public sealed class WaveformSeekBar : Grid
{
    public WaveformSeekBar(AudioService audioService, string automationId)
    {
        HeightRequest = 44;
        BackgroundColor = Colors.Transparent;

        var waveform = new WaveformView { VerticalOptions = LayoutOptions.Fill, InputTransparent = true }
            .Bind(WaveformView.ProgressProperty, nameof(AudioService.PlaybackProgress));

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            HeightRequest = 44,
            BackgroundColor = Colors.Transparent,
            AutomationId = automationId
        }
        .Invoke(s => SemanticProperties.SetDescription(s, "Playback position"))
        .Invoke(s => SemanticProperties.SetHint(s, "Drag to seek within the scene"))
        .Bind(Slider.ValueProperty, nameof(AudioService.PlaybackProgress), BindingMode.OneWay)
        .AppThemeBinding(Slider.ThumbColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
        .AppThemeBinding(Slider.MinimumTrackColorProperty, Colors.Transparent, Colors.Transparent)
        .AppThemeBinding(Slider.MaximumTrackColorProperty, Colors.Transparent, Colors.Transparent);

        slider.DragStarted += (_, _) =>
        {
            if (audioService.BeginSeekCommand.CanExecute(null))
                audioService.BeginSeekCommand.Execute(null);
        };

        slider.DragCompleted += (_, _) =>
        {
            audioService.SeekProgress = slider.Value;
            if (audioService.EndSeekCommand.CanExecute(null))
                audioService.EndSeekCommand.Execute(null);
        };

        Children.Add(waveform);
        Children.Add(slider);
    }
}

/// <summary>Bars are deterministic so the waveform is the same "recording" every time a scene is played.</summary>
public sealed class WaveformView : GraphicsView
{
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress),
        typeof(double),
        typeof(WaveformView),
        0d,
        propertyChanged: OnProgressChanged);

    public WaveformView()
    {
        Drawable = new WaveformDrawable(this);
        BackgroundColor = Colors.Transparent;
        Loaded += (_, _) => Invalidate();
    }

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    private static void OnProgressChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is WaveformView view)
            view.Invalidate();
    }
}

internal sealed class WaveformDrawable : IDrawable
{
    private const int BarCount = 52;

    private readonly WaveformView _owner;

    // Fixed silhouette: a screen of noise would read as decoration, a steady ridge reads as a recording.
    private static readonly float[] BarHeights = BuildBars();

    public WaveformDrawable(WaveformView owner) => _owner = owner;

    private static float[] BuildBars()
    {
        var bars = new float[BarCount];

        for (var i = 0; i < BarCount; i++)
        {
            var t = i * 0.72f;
            var slow = (MathF.Sin(t) + 1f) / 2f;
            var fast = (MathF.Sin((t * 2.3f) + 1.7f) + 1f) / 2f;
            bars[i] = 0.24f + (slow * 0.46f) + (fast * 0.30f);
        }

        return bars;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        canvas.Antialias = true;

        var gap = MathF.Max(1.5f, dirtyRect.Width / BarCount * 0.38f);
        var barWidth = MathF.Max(2f, (dirtyRect.Width - (gap * (BarCount - 1))) / BarCount);
        var played = (float)Math.Clamp(_owner.Progress, 0d, 1d) * BarCount;

        for (var i = 0; i < BarCount; i++)
        {
            var height = 5f + ((dirtyRect.Height * 0.62f) - 5f) * BarHeights[i];
            var x = dirtyRect.X + (i * (barWidth + gap));
            var y = dirtyRect.Y + ((dirtyRect.Height - height) / 2f);

            canvas.FillColor = i < played
                ? AppColors.PrimaryLight.WithAlpha(0.92f)
                : AppColors.StrokeLight.WithAlpha(0.38f);

            canvas.FillRoundedRectangle(x, y, barWidth, height, barWidth / 2f);
        }
    }
}
