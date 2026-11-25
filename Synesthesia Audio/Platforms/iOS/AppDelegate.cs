using AVFoundation;
using Foundation;
using UIKit;
using MediaPlayer;

namespace Synesthesia_Audio;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
	{
		var session = AVAudioSession.SharedInstance();
		NSError error;
		session.SetCategory(AVAudioSession.CategoryPlayback, out error);
		session.SetActive(true, out error);

        var commandCenter = MPRemoteCommandCenter.Shared;
		
		commandCenter.PlayCommand.Enabled = true;
		commandCenter.PlayCommand.AddTarget(cmd =>
		{
			var audioService = IPlatformApplication.Current!.Services.GetRequiredService<Services.AudioService>();
			MainThread.BeginInvokeOnMainThread(() => audioService.Play());
			return MPRemoteCommandHandlerStatus.Success;
		});

		commandCenter.PauseCommand.Enabled = true;
		commandCenter.PauseCommand.AddTarget(cmd =>
		{
			var audioService = IPlatformApplication.Current!.Services.GetRequiredService<Services.AudioService>();
			MainThread.BeginInvokeOnMainThread(() => audioService.Pause());
			return MPRemoteCommandHandlerStatus.Success;
		});

		commandCenter.NextTrackCommand.Enabled = true;
		commandCenter.NextTrackCommand.AddTarget(cmd =>
		{
			var audioService = IPlatformApplication.Current!.Services.GetRequiredService<Services.AudioService>();
			MainThread.BeginInvokeOnMainThread(() => audioService.PlayNext());
			return MPRemoteCommandHandlerStatus.Success;
		});

		commandCenter.PreviousTrackCommand.Enabled = true;
		commandCenter.PreviousTrackCommand.AddTarget(cmd =>
		{
			var audioService = IPlatformApplication.Current!.Services.GetRequiredService<Services.AudioService>();
			MainThread.BeginInvokeOnMainThread(() => audioService.PlayPrevious());
			return MPRemoteCommandHandlerStatus.Success;
		});

		return base.FinishedLaunching(application, launchOptions);
	}
}
