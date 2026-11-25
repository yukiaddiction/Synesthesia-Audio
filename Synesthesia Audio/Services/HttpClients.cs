namespace Synesthesia_Audio.Services;

/// <summary>
/// The named <c>HttpClient</c> purposes registered in <c>MauiProgram</c>.
///
/// A string constant per purpose rather than a literal at each call site: the name is the only link between the
/// registration and the consumer, so a typo would silently produce a default client with no timeout, headers or
/// handler instead of failing to compile (DUP-02).
/// </summary>
public static class HttpClients
{
    /// <summary>DLsite HTML and API pages: age-gate cookie, referer, ja/en language headers.</summary>
    public const string DLsite = "dlsite";

    /// <summary>Machine-translation providers, with the Retry-After capture handler attached.</summary>
    public const string Translation = "translation";

    /// <summary>Cover artwork downloads from the image hosts.</summary>
    public const string Cover = "cover";

    /// <summary>EDRDG name data and the Kawazu dictionary: large files, long timeout.</summary>
    public const string NameData = "name-data";
}
