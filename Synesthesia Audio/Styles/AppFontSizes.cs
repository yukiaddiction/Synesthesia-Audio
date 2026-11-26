namespace Synesthesia_Audio.Styles;

/// <summary>
/// Type sizes.
///
/// These used to be multiplied by a hand-rolled Android FontScale factor, clamped to 0.85-1.5. MAUI already
/// applies the system font scale to a Label through FontAutoScalingEnabled, which defaults to true, so Android
/// users were getting the scale applied twice and the other platforms got a size the OS could not adjust at all
/// (OVR-08). The sizes are plain numbers now and the platform scales them.
/// </summary>
public static class AppFontSizes
{    public static double PageTitle => 22;
    public static double SectionTitle => 20;
    public static double GroupTitle => 18;
    public static double PathLabel => 16;
    public static double Subtitle => 15;
    public static double ButtonLarge => 14;
    public static double BodyBold => 14;
    public static double Body => 13;
    public static double FloatingPlayerTitle => 13;
    public static double ButtonMedium => 12;
    public static double SmallText => 12;
    public static double Caption => 12;
    public static double ButtonSmall => 12;
    // 11 is the floor here on purpose: DetailText labels dense metadata rows, and pushing it to 12 without a
    // visual pass risks the phone overflow UX-09 describes. Caption and ButtonSmall are at 12 (UX-01).
    public static double DetailText => 11;
    public static double CoverPlaceholderIcon => 50;
}