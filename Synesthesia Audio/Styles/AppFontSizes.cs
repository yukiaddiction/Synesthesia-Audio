namespace Synesthesia_Audio.Styles;

public static class AppFontSizes
{
    public static double ScaleFactor { get; private set; } = 1.0;

    public static void Initialize()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            ScaleFactor = context?.Resources?.Configuration?.FontScale ?? 1.0;
            if (ScaleFactor < 0.85) ScaleFactor = 0.85;
            if (ScaleFactor > 1.5) ScaleFactor = 1.5;
        }
        catch { ScaleFactor = 1.0; }
#endif
    }

    public static double PageTitle => 22 * ScaleFactor;
    public static double SectionTitle => 20 * ScaleFactor;
    public static double GroupTitle => 18 * ScaleFactor;
    public static double PathLabel => 16 * ScaleFactor;
    public static double Subtitle => 15 * ScaleFactor;
    public static double ButtonLarge => 14 * ScaleFactor;
    public static double BodyBold => 14 * ScaleFactor;
    public static double Body => 13 * ScaleFactor;
    public static double FloatingPlayerTitle => 13 * ScaleFactor;
    public static double ButtonMedium => 12 * ScaleFactor;
    public static double SmallText => 12 * ScaleFactor;
    public static double Caption => 11 * ScaleFactor;
    public static double ButtonSmall => 11 * ScaleFactor;
    public static double DetailText => 10 * ScaleFactor;
    public static double CoverPlaceholderIcon => 50 * ScaleFactor;
}
