#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;
using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Platforms.Android;

public static class BatteryOptimizationHelper
{
    private sealed class Log { }
    public static void RequestIgnoreBatteryOptimizations()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var powerManager = (PowerManager?)context.GetSystemService(Context.PowerService);
            var packageName = context.PackageName;

            if (powerManager != null && !powerManager.IsIgnoringBatteryOptimizations(packageName))
            {
                var intent = new Intent(Settings.ActionRequestIgnoreBatteryOptimizations);
                intent.SetData(global::Android.Net.Uri.Parse($"package:{packageName}"));
                intent.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
        catch (System.Exception ex)
        {
            Services.Logging.AppLog.Get<BatteryOptimizationHelper.Log>().LogWarning(ex, "Battery optimization request failed");
        }
    }
}
#endif
