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


        builder.Services.AddLogging(logging =>
        {
            logging.AddDebug();
            logging.AddProvider(new FileLoggerProvider());
        });

        // The Debug provider was registered twice: inside AddLogging above and again through
        // builder.Logging. One registration is enough, and the duplicate emitted every line twice (PAT-10).

        builder.Services.AddSingleton<LogService>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<ISecureStore, SecureStore>();
        builder.Services.AddSingleton<SettingsService>();
        builder.Services.AddSingleton<TagMappingService>();
        builder.Services.AddSingleton<LibraryCacheService>();
        // Named clients (DUP-02 / OVR-05). Each service asks for its purpose by name, so the timeout, headers and
        // handler chain for that purpose live in exactly one place, and the handler pools last the process
        // lifetime instead of being rebuilt per service instance.
        builder.Services.AddHttpClient(HttpClients.DLsite, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(AppConstants.DlsiteClientTimeoutSeconds);
            client.DefaultRequestHeaders.Add("User-Agent", DLsiteService.DefaultUserAgent);
            client.DefaultRequestHeaders.Add("Accept-Language", "ja,en-US;q=0.9,en;q=0.8");
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("Referer", "https://www.dlsite.com/");
            // The age gate is a cookie, not a flag: without it DLsite answers with the interstitial page.
            client.DefaultRequestHeaders.Add("Cookie", "age_check_done=1; adultchecked=1; work_view_mode=2;");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { MaxConnectionsPerServer = 16 });

        builder.Services.AddHttpClient(HttpClients.Translation, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(AppConstants.TranslationRequestTimeoutSeconds);
        }).AddHttpMessageHandler<RetryAfterCaptureHandler>();

        builder.Services.AddHttpClient(HttpClients.Cover, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", DLsiteService.DefaultUserAgent);
        });

        builder.Services.AddHttpClient(HttpClients.NameData, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Add("User-Agent", DLsiteService.DefaultUserAgent);
        });

        builder.Services.AddSingleton<RetryAfterCaptureHandler>();
        builder.Services.AddSingleton<ITranslationService, TranslationService>();
        builder.Services.AddSingleton<NameRomanizationService>();
        builder.Services.AddSingleton<KawazuKanjiRomanizer>();
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
        builder.Services.AddSingleton<CoverThumbnailer>();

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
