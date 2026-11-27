using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services.Cloud;

/// <summary>
/// Explicit endpoint and credentials, so Browse and Test Connection can use unsaved form values instead of
/// temporarily overwriting the shared settings (which went stale once the modal page opened).
/// </summary>
public sealed record WebDavConnection(string ServerUrl, string Username, string Password)
{
    // An empty password is allowed because some servers take anonymous access.
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(Username);

    public string NormalizedServerUrl =>
        Synesthesia_Audio.Core.WebDavUrl.NormalizeBase(ServerUrl);

    public static WebDavConnection FromSettings(GeneralSettings settings) =>
        new(settings.CloudServerUrl, settings.CloudUsername, settings.CloudPassword);
}
