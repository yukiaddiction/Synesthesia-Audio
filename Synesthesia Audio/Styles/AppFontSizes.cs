namespace Synesthesia_Audio.Styles;

/// <summary>
/// Plain sizes with no manual scaling: MAUI already applies the system font scale (FontAutoScalingEnabled).
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
    // Uppercase prefixes such as "CV" and "CIRCLE" are labels, not body text, so they can sit one step down.
    public static double Overline => 11;
    // 12 is the minimum for readable body text; watch for overflow in dense metadata rows on narrow cards.
    public static double DetailText => 12;
    public static double CoverPlaceholderIcon => 50;
}