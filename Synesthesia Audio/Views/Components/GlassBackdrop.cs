using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Synesthesia_Audio.Styles;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// Aurora backdrop of soft radial gradient blobs, which fake frosted glass since MAUI has no backdrop blur.
/// A vignette and a fixed dust field turn the wallpaper into a dim room, so the page reads as a place to listen.
/// Theme arrives through a bound <see cref="IsDark"/>: subscribing to RequestedThemeChanged would leak transient pages.
/// </summary>
public sealed class GlassBackdrop : GraphicsView
{
    public static readonly BindableProperty IsDarkProperty = BindableProperty.Create(
        nameof(IsDark),
        typeof(bool),
        typeof(GlassBackdrop),
        false,
        propertyChanged: OnIsDarkChanged);

    public GlassBackdrop()
    {
        Drawable = new AuroraDrawable(this);
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
        Loaded += (_, _) => Invalidate();
    }

    public bool IsDark
    {
        get => (bool)GetValue(IsDarkProperty);
        set => SetValue(IsDarkProperty, value);
    }

    private static void OnIsDarkChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is GlassBackdrop backdrop)
            backdrop.Invalidate();
    }
}

internal sealed class AuroraDrawable : IDrawable
{
    // Centres as fractions of width/height; diameters as fractions of the shorter side so blobs stay round.
    private static readonly (double X, double Y, double Size)[] BlobLayout =
    {
        (0.06, -0.04, 1.20),
        (0.98, 0.08, 0.90),
        (0.84, 0.94, 1.05),
        (0.12, 0.88, 0.80),
        (0.50, 0.38, 0.70)
    };

    // Fixed dust motes, not random: a field that never settles would read as noise rather than atmosphere.
    private static readonly (double X, double Y, double Size, double Alpha)[] DustField =
    {
        (0.07, 0.18, 0.0045, 0.55), (0.16, 0.42, 0.0030, 0.40), (0.23, 0.11, 0.0055, 0.35),
        (0.31, 0.66, 0.0035, 0.30), (0.38, 0.29, 0.0040, 0.45), (0.44, 0.82, 0.0030, 0.28),
        (0.51, 0.14, 0.0060, 0.30), (0.57, 0.56, 0.0035, 0.38), (0.63, 0.36, 0.0045, 0.28),
        (0.69, 0.72, 0.0030, 0.34), (0.75, 0.22, 0.0050, 0.30), (0.82, 0.49, 0.0040, 0.36),
        (0.88, 0.12, 0.0030, 0.26), (0.93, 0.63, 0.0055, 0.30), (0.96, 0.86, 0.0035, 0.24),
        (0.12, 0.75, 0.0060, 0.26), (0.28, 0.92, 0.0040, 0.22), (0.72, 0.94, 0.0045, 0.22)
    };

    private readonly GlassBackdrop _owner;

    // Built once per theme, not per repaint: paints don't depend on blob bounds and the backdrop repaints on every resize.
    private static readonly RadialGradientPaint[] LightPaints = BuildPaints(LightBlobs());
    private static readonly RadialGradientPaint[] DarkPaints = BuildPaints(DarkBlobs());
    private static readonly RadialGradientPaint LightVignette = BuildVignette(AppColors.VignetteLight);
    private static readonly RadialGradientPaint DarkVignette = BuildVignette(AppColors.VignetteDark);

    public AuroraDrawable(GlassBackdrop owner) => _owner = owner;

    private static RadialGradientPaint[] BuildPaints(Color[] colours)
    {
        var paints = new RadialGradientPaint[colours.Length];

        for (var i = 0; i < colours.Length; i++)
        {
            var colour = colours[i];
            paints[i] = new RadialGradientPaint
            {
                Center = new Point(0.5, 0.5),
                Radius = 0.5,
                GradientStops =
                [
                    new PaintGradientStop(0f, colour),
                    new PaintGradientStop(0.72f, colour.WithAlpha(colour.Alpha * 0.35f)),
                    new PaintGradientStop(1f, colour.WithAlpha(0f))
                ]
            };
        }

        return paints;
    }

    /// <summary>Transparent through the middle, darkening only the edges, like a room lit from one side.</summary>
    private static RadialGradientPaint BuildVignette(Color edge)
    {
        return new RadialGradientPaint
        {
            Center = new Point(0.5, 0.42),
            Radius = 0.92,
            GradientStops =
            [
                new PaintGradientStop(0f, edge.WithAlpha(0f)),
                new PaintGradientStop(0.58f, edge.WithAlpha(0f)),
                new PaintGradientStop(1f, edge)
            ]
        };
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        var isDark = _owner.IsDark;
        canvas.FillColor = isDark ? AppColors.BackgroundDark : AppColors.BackgroundLight;
        canvas.FillRectangle(dirtyRect.X, dirtyRect.Y, dirtyRect.Width, dirtyRect.Height);

        var paints = isDark ? DarkPaints : LightPaints;
        var shortSide = MathF.Min(dirtyRect.Width, dirtyRect.Height);
        canvas.Antialias = true;

        for (var i = 0; i < BlobLayout.Length; i++)
        {
            var (normalizedX, normalizedY, normalizedSize) = BlobLayout[i];
            var size = shortSide * (float)normalizedSize;
            var centreX = dirtyRect.X + (dirtyRect.Width * (float)normalizedX);
            var centreY = dirtyRect.Y + (dirtyRect.Height * (float)normalizedY);
            var blob = new RectF(centreX - (size / 2f), centreY - (size / 2f), size, size);
            // Fitted to the blob's own bounds so scaling doesn't depend on the platform's gradient normalisation.
            canvas.SetFillPaint(paints[i], blob);

            canvas.FillEllipse(blob.X, blob.Y, blob.Width, blob.Height);
        }

        canvas.SetFillPaint(isDark ? DarkVignette : LightVignette, dirtyRect);
        canvas.FillRectangle(dirtyRect);

        var dust = isDark ? AppColors.MoteDark : AppColors.MoteLight;
        foreach (var (x, y, size, alpha) in DustField)
        {
            var radius = MathF.Max(1f, shortSide * (float)size * 0.72f);
            canvas.FillColor = dust.WithAlpha((float)alpha * 0.72f);
            canvas.FillEllipse(
                dirtyRect.X + (dirtyRect.Width * (float)x) - radius,
                dirtyRect.Y + (dirtyRect.Height * (float)y) - radius,
                radius * 2f,
                radius * 2f);
        }

        // A last faint pool of light on the floor of the page, under the glass panels.
        var poolWidth = dirtyRect.Width * 0.9f;
        var poolHeight = dirtyRect.Height * 0.22f;
        canvas.FillColor = (isDark ? AppColors.PrimaryLight : Colors.White).WithAlpha(isDark ? 0.10f : 0.35f);
        canvas.FillEllipse(
            dirtyRect.X + (dirtyRect.Width / 2f) - (poolWidth / 2f),
            dirtyRect.Y + dirtyRect.Height - (poolHeight / 1.4f),
            poolWidth,
            poolHeight);
    }

    private static Color[] LightBlobs() => new[]
    {
        AppColors.ControlBgLight.WithAlpha(0.85f),
        AppColors.SurfaceLight.WithAlpha(1.00f),
        AppColors.StrokeLight.WithAlpha(0.60f),
        AppColors.PrimaryLight.WithAlpha(0.22f),
        Colors.White.WithAlpha(0.55f)
    };

    private static Color[] DarkBlobs() => new[]
    {
        AppColors.BackgroundAltDark.WithAlpha(0.95f),
        AppColors.SurfaceDark.WithAlpha(0.85f),
        AppColors.PrimaryLight.WithAlpha(0.42f),
        AppColors.StrokeDark.WithAlpha(0.30f),
        AppColors.BackgroundDark.WithAlpha(0.70f)
    };
}
