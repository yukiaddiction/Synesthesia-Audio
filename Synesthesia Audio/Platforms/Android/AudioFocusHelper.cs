using Android.Content;
using Android.Media;
using Android.OS;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace Synesthesia_Audio.Platforms.Android;


/// <summary>One resolved logger for this file (framework-created classes cannot take a dependency) — PAT-01.</summary>
internal static class FileLog79371
{
    internal static readonly ILogger<AudioFocusHelper> Logger = Synesthesia_Audio.Services.Logging.AppLog.Get<AudioFocusHelper>();
}
internal class AudioFocusHelper : Java.Lang.Object, AudioManager.IOnAudioFocusChangeListener
{
    private static AudioFocusHelper? _instance;
    private static readonly object _instanceLock = new();

    private AudioFocusRequestClass? _focusRequest;
    private volatile bool _hasFocus;

    private AudioFocusHelper()
    {
    }

    public static AudioFocusHelper Instance
    {
        get
        {
            lock (_instanceLock)
            {
                _instance ??= new AudioFocusHelper();
                return _instance;
            }
        }
    }

    public bool RequestFocus()
    {
        try
        {
            if (_hasFocus) return true;

            var audioManager = (AudioManager?)global::Android.App.Application.Context.GetSystemService(Context.AudioService);
            if (audioManager == null) return true;

            AudioFocusRequest result;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                if (_focusRequest == null)
                {
                    var attributes = new AudioAttributes.Builder()
                        .SetUsage(AudioUsageKind.Media)?
                        .SetContentType(AudioContentType.Music)?
                        .Build();

                    var builder = new AudioFocusRequestClass.Builder(AudioFocus.Gain);
                    if (attributes != null)
                        builder = builder.SetAudioAttributes(attributes) ?? builder;
                    builder = builder.SetOnAudioFocusChangeListener(this) ?? builder;
                    _focusRequest = builder.Build();
                }

                result = _focusRequest != null
                    ? audioManager.RequestAudioFocus(_focusRequest)
                    : AudioFocusRequest.Failed;
            }
            else
            {
#pragma warning disable CS0618
                result = audioManager.RequestAudioFocus(this, global::Android.Media.Stream.Music, AudioFocus.Gain);
#pragma warning restore CS0618
            }

            _hasFocus = result == AudioFocusRequest.Granted;
            FileLog79371.Logger.LogInformation("AudioFocus request result: {Result}, hasFocus: {HasFocus}", result, _hasFocus);
            return _hasFocus;
        }
        catch (Exception ex)
        {
            FileLog79371.Logger.LogWarning(ex, "Audio focus request failed");
            return true;
        }
    }

    public void AbandonFocus()
    {
        try
        {
            if (!_hasFocus) return;

            var audioManager = (AudioManager?)global::Android.App.Application.Context.GetSystemService(Context.AudioService);
            if (audioManager == null) return;

            if (OperatingSystem.IsAndroidVersionAtLeast(26))
            {
                if (_focusRequest != null)
                    audioManager.AbandonAudioFocusRequest(_focusRequest);
            }
            else
            {
#pragma warning disable CS0618
                audioManager.AbandonAudioFocus(this);
#pragma warning restore CS0618
            }

            _hasFocus = false;
        }
        catch (Exception ex)
        {
            FileLog79371.Logger.LogWarning(ex, "Audio focus abandon failed");
        }
    }

    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        try
        {
            FileLog79371.Logger.LogInformation("AudioFocus changed: {Focus}", focusChange);
            var audioService = IPlatformApplication.Current?.Services?.GetService<Services.AudioService>();
            if (audioService == null) return;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    switch (focusChange)
                    {
                        case AudioFocus.Loss:
                            // Permanent loss: hand the focus back as well, and stop. There is nothing to
                            // resume later, so the AudioService records this as a focus-caused pause and
                            // AbandonFocus clears the request.
                            AbandonFocus();
                            audioService.PauseForFocusLoss();
                            break;

                        case AudioFocus.LossTransient:
                            // Keep the request so a Gain can resume, but stop playing now. Previously this
                            // only set flags, so audio kept playing over calls and other players.
                            _hasFocus = false;
                            audioService.PauseForFocusLoss();
                            break;

                        case AudioFocus.LossTransientCanDuck:
                            // We may keep playing, but quietly.
                            audioService.SetDucked(true);
                            break;

                        case AudioFocus.Gain:
                            _hasFocus = true;
                            // Restores volume, and resumes only if the focus loss is what paused playback.
                            audioService.ResumeAfterFocusGain();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    FileLog79371.Logger.LogWarning(ex, "Audio focus change handling failed");
                }
            });
        }
        catch (Exception ex)
        {
            FileLog79371.Logger.LogWarning(ex, "OnAudioFocusChange failed");
        }
    }
}

internal static class WakeLockHelper
{
    private static PowerManager.WakeLock? _wakeLock;
    private static readonly object _lock = new();

    public static void Acquire()
    {
        lock (_lock)
        {
            try
            {
                if (_wakeLock != null && _wakeLock.IsHeld) return;

                var powerManager = (PowerManager?)global::Android.App.Application.Context.GetSystemService(Context.PowerService);
                if (powerManager == null) return;

                _wakeLock ??= powerManager.NewWakeLock(WakeLockFlags.Partial, "SynesthesiaAudio:Playback");
                if (_wakeLock != null && !_wakeLock.IsHeld)
                {
                    _wakeLock.Acquire();
                }
            }
            catch (Exception ex)
            {
                FileLog79371.Logger.LogWarning(ex, "Wake lock acquire failed");
            }
        }
    }

    public static void Release()
    {
        lock (_lock)
        {
            try
            {
                if (_wakeLock != null && _wakeLock.IsHeld)
                {
                    _wakeLock.Release();
                }
            }
            catch (Exception ex)
            {
                FileLog79371.Logger.LogWarning(ex, "Wake lock release failed");
            }
        }
    }
}
