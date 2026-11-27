using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Core;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public class PlaybackSessionService
{
    private readonly JsonFileStore<PlaybackSessionState> _store;
    private readonly ILogger<PlaybackSessionService> _logger;

    public PlaybackSessionService(ILogger<PlaybackSessionService> logger)
    {
        _logger = logger;
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // Debounced, atomic writer: rapid track changes would otherwise race on the file and throw IOException.
        _store = new JsonFileStore<PlaybackSessionState>(
            Path.Combine(appDataDir, "SynesthesiaAudio_Session.json"),
            saveDebounceMs: 400);
        _store.LoadFailed += ex => _logger.LogWarning(ex, "PlaybackSession: unreadable session file kept as .bak");
        _store.SaveFailed += ex => _logger.LogWarning(ex, "PlaybackSession: Save failed");
    }

    public Task<PlaybackSessionState> Loaded => _store.Loaded;

    public async Task<PlaybackSessionState?> LoadAsync()
    {
        try
        {
            var state = await _store.Loaded.ConfigureAwait(false);
            return state.CurrentTrackIndex < 0 && state.Playlist.Count == 0 ? null : state;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaybackSession: Load failed");
            return null;
        }
    }

    public Task SaveAsync(PlaybackSessionState state)
    {
        _store.Replace(state);
        return Task.CompletedTask;
    }

    /// <summary>Cancels any queued save first, so a debounced write cannot recreate the deleted file.</summary>
    public async Task ClearAsync()
    {
        _store.ResetWithoutSaving();
        try
        {
            await Task.Run(() =>
            {
                if (File.Exists(_store.Path)) File.Delete(_store.Path);
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaybackSession: Clear failed");
        }
    }
}
