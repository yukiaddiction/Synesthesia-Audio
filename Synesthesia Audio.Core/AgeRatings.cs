namespace Synesthesia_Audio.Core;

/// <summary>
/// Shared by the parser and the app's filter. <see cref="Unknown"/> is never SFW: it means metadata could not
/// be read, and defaulting it to SFW leaked R-18 works into the All-Age list whenever DLsite was unreachable.
/// </summary>
public static class AgeRatings
{
    public const string Sfw = "SFW";
    public const string Nsfw = "NSFW";
    public const string Unknown = "Unknown";

    public static bool IsNsfw(string? value) =>
        string.Equals(value, Nsfw, StringComparison.OrdinalIgnoreCase);

    public static bool IsSfw(string? value) =>
        string.Equals(value, Sfw, StringComparison.OrdinalIgnoreCase);

    public static bool IsUnknown(string? value) => !IsSfw(value) && !IsNsfw(value);
}
