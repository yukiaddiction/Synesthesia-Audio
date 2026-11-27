using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Holds "the user tapped a notification for this album" until the UI is ready. A singleton rather than static
/// state on the Activity, which would outlive a re-created activity and could open the wrong album.
/// </summary>
public sealed class StartupNavigation
{
    private readonly object _lock = new();
    private string? _pendingAlbumFolderPath;

    private readonly ILogger<StartupNavigation> _logger;

    public StartupNavigation(ILogger<StartupNavigation> logger) => _logger = logger;

    public void RequestAlbum(string? albumFolderPath)
    {
        if (string.IsNullOrEmpty(albumFolderPath)) return;

        lock (_lock) _pendingAlbumFolderPath = albumFolderPath;
    }

    /// <summary>Clears the request as it is taken, so it is acted on at most once.</summary>
    public string? TakePendingAlbum()
    {
        lock (_lock)
        {
            var pending = _pendingAlbumFolderPath;
            _pendingAlbumFolderPath = null;
            return pending;
        }
    }

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
            // An intent can name anything; an unknown work would open an empty album.
            _logger.LogInformation("Album navigation request did not match a cached work; dropping it");
            return;
        }

        await navigationService.GoToAsync("//Library").ConfigureAwait(false);
        await navigationService.GoToAsync(
            nameof(Views.AlbumPage),
            new Dictionary<string, object> { { "Album", work } }).ConfigureAwait(false);
    }
}
