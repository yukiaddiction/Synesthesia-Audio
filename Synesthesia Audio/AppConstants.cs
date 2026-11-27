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

    // Any one Japanese character: hiragana + katakana (3040-30FF), CJK Extension A (3400-4DBF), CJK Unified Ideographs (4E00-9FFF).
    public static readonly System.Text.RegularExpressions.Regex JapaneseTextRegex =
        new(@"[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff]", System.Text.RegularExpressions.RegexOptions.Compiled);

    public const string DefaultArtist = "Unknown Artist";
    public const double PreviousTrackRestartThresholdSeconds = 3.0;
    public const double SessionSaveIntervalSeconds = 5.0;

    // Constants rather than appsettings.json: a packaged app's config can't be edited, and a mistyped key silently fell back to a default.
    public const int DlsiteClientTimeoutSeconds = 15;
    public const int DlsiteApiTimeoutSeconds = 8;
    public const int DlsiteHtmlTimeoutSeconds = 8;
    public const int DlsiteTagFetchTimeoutSeconds = 5;

    public const int TranslationRequestTimeoutSeconds = 6;
    public const int TranslationRateLimitBaseCooldownSeconds = 30;
    public const int TranslationRateLimitMaxCooldownSeconds = 300;

    public const int ProxyStreamTimeoutMs = 30000;

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

    /// <summary>Never treated as SFW, so a work whose metadata couldn't be read can't slip into the All-Age list.</summary>
    Unknown
}
