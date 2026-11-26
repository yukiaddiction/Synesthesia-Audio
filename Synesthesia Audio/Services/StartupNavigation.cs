using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Holds "the user tapped a notification for this album" until the UI is ready to act on it.
///
/// This lived as a private static field on <c>MainActivity</c>, read and written through two static methods that a
/// page also called directly (PAT-07). Static mutable state on an Activity outlives the Activity, so a stale path
/// could survive a re-created activity and navigate to the wrong album. As a singleton this has one owner, and the
/// request is cleared the moment it is consumed.
/// </summary>
public sealed class StartupNavigation
{
    private readonly object _lock = new();
    private string? _pendingAlbumFolderPath;

    private readonly ILogger<StartupNavigation> _logger;

    public StartupNavigation(ILogger<StartupNavigation> logger) => _logger = logger;

    /// <summary>Records the album an intent asked for. Null or empty is ignored.</summary>
    public void RequestAlbum(string? albumFolderPath)
    {
        if (string.IsNullOrEmpty(albumFolderPath)) return;

        lock (_lock) _pendingAlbumFolderPath = albumFolderPath;
    }

    /// <summary>
    /// Takes the pending request, clearing it so it is acted on at most once. Null when there is nothing waiting.
    /// </summary>
    public string? TakePendingAlbum()
    {
        lock (_lock)
        {
            var pending = _pendingAlbumFolderPath;
            _pendingAlbumFolderPath = null;
            return pending;
        }
    }

    /// <summary>
    /// Acts on a pending request against the library cache: resolves it to a known work and navigates there.
    /// Anything unknown is dropped rather than navigated to, and the reason is logged.
    /// </summary>
    public async Task TryNavigateAsync(
        LibraryCacheService cacheService,
        INavigationService navigationService)
    {
        ArgumentNullException.ThrowIfNull(cacheService);
        ArgumentNullException.ThrowIfNull(navigationService);

        var pending = TakePendingAlbum();
        if (string.IsNullOrEmpty(pending)) return;

        var work = cacheService.GetWork(pending);
        if (work == null)
        {
            // An intent can name anything. Navigating to a work we do not have would open an empty album.
            _logger.LogInformation("Album navigation request did not match a cached work; dropping it");
            return;
        }

        await navigationService.GoToAsync("//Library").ConfigureAwait(false);
        await navigationService.GoToAsync(
            nameof(Views.AlbumPage),
            new Dictionary<string, object> { { "Album", work } }).ConfigureAwait(false);
    }
}
