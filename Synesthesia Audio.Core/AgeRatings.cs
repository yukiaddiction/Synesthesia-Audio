namespace Synesthesia_Audio.Core;

/// <summary>
/// The serialized age-rating values. They live in Core because the parser produces them and the app
/// filters on them, and both sides must agree.
///
/// <see cref="Unknown"/> is not a synonym for <see cref="Sfw"/>: it means metadata could not be read, so
/// the work must never be presented as all-ages. Defaulting unknown metadata to SFW put R-18 works in the
/// All-Age list whenever DLsite was blocked or unreachable.
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

    /// <summary>True for an explicit <see cref="Unknown"/> and for any value that is neither SFW nor NSFW.</summary>
    public static bool IsUnknown(string? value) => !IsSfw(value) && !IsNsfw(value);
}
