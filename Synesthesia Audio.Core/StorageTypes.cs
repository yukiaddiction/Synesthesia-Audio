namespace Synesthesia_Audio.Core;

/// <summary>
/// The storage-type values that are persisted in settings and compared all over the app.
///
/// They live here, as the single definition, because they were previously written as bare string literals in a
/// dozen places — including one <c>.Contains("WebDAV")</c> test that would have matched any value merely
/// containing the word. A typo in any one of those comparisons silently changed behaviour instead of failing to
/// compile (PAT-06).
/// </summary>
public static class StorageTypes
{
    /// <summary>Library files live on this device.</summary>
    public const string Local = "Local";

    /// <summary>Library files are streamed from a WebDAV server.</summary>
    public const string WebDav = "WebDAV";

    /// <summary>
    /// True when <paramref name="storageType"/> selects WebDAV. Case-insensitive because the value round-trips
    /// through a settings file that older builds may have written differently.
    /// </summary>
    public static bool IsWebDav(string? storageType) =>
        string.Equals(storageType?.Trim(), WebDav, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="storageType"/> selects local storage.</summary>
    public static bool IsLocal(string? storageType) =>
        string.Equals(storageType?.Trim(), Local, StringComparison.OrdinalIgnoreCase);

    /// <summary>Coerces an unknown or empty value to local storage, which is the safe default.</summary>
    public static string Normalize(string? storageType) => IsWebDav(storageType) ? WebDav : Local;
}
