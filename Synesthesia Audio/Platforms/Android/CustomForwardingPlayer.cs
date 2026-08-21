using Com.Google.Android.Exoplayer2;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace Synesthesia_Audio.Platforms.Android;

public class CustomForwardingPlayer : ForwardingPlayer
{
    private const int CommandSeekToNext = 6;
    private const int CommandSeekToPrevious = 5;
    private const int CommandSeekToNextMediaItem = 17;
    private const int CommandSeekToPreviousMediaItem = 16;

    public CustomForwardingPlayer(IPlayer player) : base(player)
    {
    }

    public override IPlayer.Commands AvailableCommands
    {
        get
        {
            var commands = base.AvailableCommands?.BuildUpon();
            if (commands != null)
            {
                commands.Add(CommandSeekToNext);
                commands.Add(CommandSeekToPrevious);
                commands.Add(CommandSeekToNextMediaItem);
                commands.Add(CommandSeekToPreviousMediaItem);
                return commands.Build();
            }
            return base.AvailableCommands!;
        }
    }

    public override bool IsCommandAvailable(int command)
    {
        if (command == CommandSeekToNext || 
            command == CommandSeekToPrevious || 
            command == CommandSeekToNextMediaItem || 
            command == CommandSeekToPreviousMediaItem)
        {
            return true;
        }
        return base.IsCommandAvailable(command);
    }



    public override void SeekToNext()
    {
        var audioService = IPlatformApplication.Current?.Services?.GetService<Services.AudioService>();
        if (audioService == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                audioService.PlayNext();
            }
            catch (Exception ex)
            {
                Synesthesia_Audio.Services.Logging.AppLog.Get<CustomForwardingPlayer>().LogError(ex, "SeekToNext failed");
            }
        });
    }

    public override void SeekToPrevious()
    {
        var audioService = IPlatformApplication.Current?.Services?.GetService<Services.AudioService>();
        if (audioService == null) return;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                audioService.PlayPrevious();
            }
            catch (Exception ex)
            {
                Synesthesia_Audio.Services.Logging.AppLog.Get<CustomForwardingPlayer>().LogError(ex, "SeekToPrevious failed");
            }
        });
    }

    public override void SeekToNextMediaItem()
    {
        SeekToNext();
    }

    public override void SeekToPreviousMediaItem()
    {
        SeekToPrevious();
    }
}
