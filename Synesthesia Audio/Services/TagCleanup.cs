namespace Synesthesia_Audio.Services;

/// <summary>
/// The "Clean &amp; Reset Bad Tags" action.
///
/// Two pages offered the same button and each spelled out the same three steps followed by the same alert, so the
/// wording and the order could drift apart (DUP-07). The work lives here; each page only decides how to tell the
/// user.
/// </summary>
public static class TagCleanup
{
    /// <summary>The message shown after a successful clean, in one place so both entry points agree.</summary>
    public const string SuccessMessage =
        "Successfully removed bad dictionary entries and re-mapped all library work tags to clean English!";

    /// <summary>
    /// Remaps every cached work's genres and refreshes the library view. Throws if the remap fails; callers report
    /// it.
    /// </summary>
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