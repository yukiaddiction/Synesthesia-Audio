namespace Synesthesia_Audio.Services;

/// <summary>
/// The "Clean &amp; Reset Bad Tags" action, shared by two pages so their steps and wording can't drift apart.
/// </summary>
public static class TagCleanup
{
    public const string SuccessMessage =
        "Successfully removed bad dictionary entries and re-mapped all library work tags to clean English!";

    /// <summary>Throws if the remap fails; callers report it.</summary>
    public static async Task RunAsync(
        LibraryCacheService cacheService,
        TagMappingService tagService,
        Func<Task> refreshLibrary)
    {
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(tagService);
        ArgumentNullException.ThrowIfNull(refreshLibrary);

        cacheService.CleanAndReMapAllWorkGenres(tagService);
        await refreshLibrary().ConfigureAwait(false);
    }
}