namespace Synesthesia_Audio.Core;

/// <summary>
/// Single definition of the persisted storage-type values, replacing scattered literals where a typo silently
/// changed behaviour instead of failing to compile.
/// </summary>
public static class StorageTypes
{
    public const string Local = "Local";

    public const string WebDav = "WebDAV";

    /// <summary>Case-insensitive because older builds may have written the settings value differently.</summary>
    public static bool IsWebDav(string? storageType) =>
        string.Equals(storageType?.Trim(), WebDav, StringComparison.OrdinalIgnoreCase);

    public static bool IsLocal(string? storageType) =>
        string.Equals(storageType?.Trim(), Local, StringComparison.OrdinalIgnoreCase);

    /// <summary>Unknown or empty values become local storage, the safe default.</summary>
    public static string Normalize(string? storageType) => IsWebDav(storageType) ? WebDav : Local;
}
