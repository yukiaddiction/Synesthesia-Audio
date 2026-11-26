namespace Synesthesia_Audio;

public static class AppConstants
{
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

    // These were bound from appsettings.json through an AppSettings POCO. Nobody can edit that file after the
    // app is packaged, so the binding only made the values harder to find and a mis-typed key silently produced
    // a default (OVR-06). They are constants in code now.
    //
    // HTTP / DLsite scraping
    public const int DlsiteClientTimeoutSeconds = 15;
    public const int DlsiteApiTimeoutSeconds = 8;
    public const int DlsiteHtmlTimeoutSeconds = 8;
    public const int DlsiteTagFetchTimeoutSeconds = 5;

    // Translation
    public const int TranslationRequestTimeoutSeconds = 6;
    public const int TranslationRateLimitBaseCooldownSeconds = 30;
    public const int TranslationRateLimitMaxCooldownSeconds = 300;

    // Local HTTP Proxy
    public const int ProxyStreamTimeoutMs = 30000;

    // Library Scan
    public const int MaxParallelDlsiteRequests = 4;
}

public enum StorageType
{
    Local,
    WebDAV
}

public enum AgeRating
{
    SFW,
    NSFW,

    /// <summary>Metadata could not be read. Never treated as SFW, so an unknown work cannot slip into
    /// the All-Age list.</summary>
    Unknown
}
