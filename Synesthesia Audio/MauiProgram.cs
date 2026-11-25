using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Services.Logging;
using Synesthesia_Audio.Services.Storage;
using Synesthesia_Audio.Services.Translation;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Views;

namespace Synesthesia_Audio;



public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMarkup()
            .UseMauiCommunityToolkitMediaElement()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Load appsettings.json configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

        // Register typed AppSettings
        var appSettings = new AppSettings();
        builder.Configuration.Bind(appSettings);
        builder.Services.AddSingleton(appSettings);
        AppConstants.Initialize(appSettings);

        builder.Services.AddLogging(logging =>
        {
            logging.AddDebug();
            logging.AddProvider(new FileLoggerProvider());
        });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services.AddSingleton<LogService>();
        builder.Services.AddSingleton<ISecureStore, SecureStore>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<TagMappingService>();
        builder.Services.AddSingleton<LibraryCacheService>();
        builder.Services.AddSingleton<HttpClientFactory>();
        builder.Services.AddSingleton<HttpClient>(sp =>
        {
            var factory = sp.GetRequiredService<HttpClientFactory>();
            return factory.GetOrCreate("translation", () => new HttpClient());
        });
        builder.Services.AddSingleton<ITranslationService, TranslationService>();
        builder.Services.AddSingleton<DLsiteService>();
        builder.Services.AddSingleton<Synesthesia_Audio.Services.Cloud.ICloudProvider, Synesthesia_Audio.Services.Cloud.WebDavCloudProvider>();
        builder.Services.AddSingleton<Synesthesia_Audio.Services.Cloud.LocalHttpProxy>();
        builder.Services.AddSingleton<ScanStateService>();
        builder.Services.AddSingleton<ScanFolderService>();
#if ANDROID
        builder.Services.AddSingleton<IAudioPlatformService, AndroidAudioPlatformService>();
#else
        builder.Services.AddSingleton<IAudioPlatformService, DefaultAudioPlatformService>();
#endif
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<PlaybackSessionService>();
        builder.Services.AddSingleton<CoverImageCache>();

        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<AlbumViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddTransient<LogViewerViewModel>();

        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<AlbumPage>();
        builder.Services.AddTransient<TagMappingPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<LogViewerPage>();
        // Per-window UI graph: a re-created activity gets its own unparented Shell. Keeping these
        // singletons let the same Shell be attached to a second window, which crashed the app
        // inside Window.OnPageChanged with "MauiContext is null".
        builder.Services.AddTransient<AppShell>();



        var app = builder.Build();
        ServiceAccessor.Initialize(app.Services);
        RegisterGlobalExceptionHandlers();
        return app;
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            try
            {
                var logger = AppLog.Get<App>();
                if (e.ExceptionObject is Exception ex)
                    logger.LogCritical(ex, "Unhandled AppDomain exception (IsTerminating: {IsTerminating})", e.IsTerminating);
                else
                    logger.LogCritical("Unhandled AppDomain exception (non-Exception object): {ExceptionObject}", e.ExceptionObject);
            }
            catch (Exception handlerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Global exception handler failed: {handlerEx}");
            }
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            try
            {
                AppLog.Get<App>().LogError(e.Exception, "Unobserved task exception");
            }
            catch (Exception handlerEx)
            {
                System.Diagnostics.Debug.WriteLine($"Unobserved task handler failed: {handlerEx}");
            }
            e.SetObserved();
        };
    }
}
