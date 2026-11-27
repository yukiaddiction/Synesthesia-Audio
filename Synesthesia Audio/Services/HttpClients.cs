namespace Synesthesia_Audio.Services;

/// <summary>
/// Named <c>HttpClient</c> keys registered in <c>MauiProgram</c>. Constants, because a typo'd literal silently
/// yields a default client with no timeout, headers or handler instead of failing to compile.
/// </summary>
public static class HttpClients
{
    /// <summary>Carries the age-gate cookie, referer and ja/en language headers.</summary>
    public const string DLsite = "dlsite";

    /// <summary>Has the Retry-After capture handler attached.</summary>
    public const string Translation = "translation";

    public const string Cover = "cover";

    /// <summary>EDRDG name data and the Kawazu dictionary: large files, so a long timeout.</summary>
    public const string NameData = "name-data";
}
