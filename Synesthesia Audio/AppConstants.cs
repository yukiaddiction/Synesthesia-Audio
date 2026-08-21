namespace Synesthesia_Audio;

public static class AppConstants
{
    private static AppSettings _settings = new();

    public static void Initialize(AppSettings settings)
    {
        _settings = settings;
    }

    public static readonly HashSet<string> SupportedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".m4a", ".mp4"
    };

    public static bool IsMediaFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path.Replace('\\', '/'));
        return SupportedMediaExtensions.Contains(ext);
    }

    public static readonly HashSet<string> SystemDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".AppleDouble", "__MACOSX"
    };

    public static readonly HashSet<string> SystemFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".DS_Store", "Thumbs.db", "desktop.ini"
    };

    public static bool IsSystemFileOrDir(string path)
    {
        var name = System.IO.Path.GetFileName(path.Replace('\\', '/').TrimEnd('/'));
        if (string.IsNullOrEmpty(name)) return true;
        return name.StartsWith('.') || SystemDirectoryNames.Contains(name) || SystemFileNames.Contains(name);
    }

    public static readonly System.Text.RegularExpressions.Regex JapaneseTextRegex =
        new(@"[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff]", System.Text.RegularExpressions.RegexOptions.Compiled);

    public const string DefaultArtist = "Unknown Artist";
    public const double PreviousTrackRestartThresholdSeconds = 3.0;
    public const double SessionSaveIntervalSeconds = 5.0;

    // HTTP / DLsite scraping
    public static int DlsiteClientTimeoutSeconds => _settings.Http.DlsiteClientTimeoutSeconds;
    public static int DlsiteApiTimeoutSeconds => _settings.Http.DlsiteApiTimeoutSeconds;
    public static int DlsiteHtmlTimeoutSeconds => _settings.Http.DlsiteHtmlTimeoutSeconds;
    public static int DlsiteTagFetchTimeoutSeconds => _settings.Http.DlsiteTagFetchTimeoutSeconds;

    // Translation
    public static int TranslationRequestTimeoutSeconds => _settings.Translation.RequestTimeoutSeconds;

    // Local HTTP Proxy
    public static int ProxyStreamTimeoutMs => _settings.Proxy.StreamTimeoutMs;
    public static int ProxyMaxLogEntries => _settings.Proxy.MaxLogEntries;

    // Library Scan
    public static int MaxParallelDlsiteRequests => _settings.Scan.MaxParallelDlsiteRequests;
    public static int ScanProgressYieldDelayMs => _settings.Scan.ProgressYieldDelayMs;

    // App
    public static string DefaultAppTheme => _settings.App.DefaultTheme;
}

public enum StorageType
{
    Local,
    WebDAV
}

public enum AgeRating
{
    SFW,
    NSFW
}
