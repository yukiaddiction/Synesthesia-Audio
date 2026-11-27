namespace Synesthesia_Audio.Core;

/// <summary>
/// Compares against the revision already applied, not the bundled table: the bundled one only changes with a new
/// build, so comparing against it re-downloaded the same data on every launch.
/// </summary>
public static class NameDataFreshness
{
    public static bool IsNewRevision(string? publishedCreated, string? appliedCreated)
    {
        if (string.IsNullOrWhiteSpace(publishedCreated)) return false;

        // First run, or the user cleared the cache.
        if (string.IsNullOrWhiteSpace(appliedCreated)) return true;

        return !string.Equals(publishedCreated.Trim(), appliedCreated.Trim(), StringComparison.Ordinal);
    }
}
