using System.Text.Json;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services;

public class PlaybackSessionService
{
    private readonly string _sessionFilePath;
    private readonly ILogger<PlaybackSessionService> _logger;

    public PlaybackSessionService(ILogger<PlaybackSessionService> logger)
    {
        _logger = logger;
        var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _sessionFilePath = Path.Combine(appDataDir, "SynesthesiaAudio_Session.json");
    }

    public async Task SaveAsync(PlaybackSessionState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state);
            await File.WriteAllTextAsync(_sessionFilePath, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaybackSession: Save failed");
        }
    }

    public PlaybackSessionState? Load()
    {
        try
        {
            if (!File.Exists(_sessionFilePath)) return null;
            var json = File.ReadAllText(_sessionFilePath);
            return JsonSerializer.Deserialize<PlaybackSessionState>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaybackSession: Load failed");
            return null;
        }
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_sessionFilePath))
                File.Delete(_sessionFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PlaybackSession: Clear failed");
        }
    }
}
