namespace Synesthesia_Audio.Core;

/// <summary>
/// Whether a published name-data revision is worth downloading.
///
/// The comparison must be against the revision actually in use (the one already downloaded and applied),
/// not against the bundled data. The bundled table only changes when the app ships a new build, so
/// comparing against it meant the app downloaded the same table again on every single launch.
/// </summary>
public static class NameDataFreshness
{
    public static bool IsNewRevision(string? publishedCreated, string? appliedCreated)
    {
        // Nothing published means nothing to fetch.
        if (string.IsNullOrWhiteSpace(publishedCreated)) return false;

        // Nothing applied yet (first run, or a cache the user cleared): take it.
        if (string.IsNullOrWhiteSpace(appliedCreated)) return true;

        return !string.Equals(publishedCreated.Trim(), appliedCreated.Trim(), StringComparison.Ordinal);
    }
}
