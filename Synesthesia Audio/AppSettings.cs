namespace Synesthesia_Audio;

public class AppSettings
{
    public HttpSettings Http { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public ProxySettings Proxy { get; set; } = new();
    public ScanSettings Scan { get; set; } = new();
    public ApplicationSettings App { get; set; } = new();
}

public class HttpSettings
{
    public int DlsiteClientTimeoutSeconds { get; set; } = 15;
    public int DlsiteApiTimeoutSeconds { get; set; } = 8;
    public int DlsiteHtmlTimeoutSeconds { get; set; } = 8;
    public int DlsiteTagFetchTimeoutSeconds { get; set; } = 5;
}

public class TranslationSettings
{
    public int RequestTimeoutSeconds { get; set; } = 6;
    public int RateLimitBaseCooldownSeconds { get; set; } = 30;
    public int RateLimitMaxCooldownSeconds { get; set; } = 300;
}

public class ProxySettings
{
    public int StreamTimeoutMs { get; set; } = 30000;
    public int MaxLogEntries { get; set; } = 500;
}

public class ScanSettings
{
    public int MaxParallelDlsiteRequests { get; set; } = 4;
    public int ProgressYieldDelayMs { get; set; } = 15;
}

public class ApplicationSettings
{
    public string DefaultTheme { get; set; } = "Dark";
}
