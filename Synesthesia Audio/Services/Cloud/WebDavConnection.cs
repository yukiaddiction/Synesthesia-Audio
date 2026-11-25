using Synesthesia_Audio.Models;

namespace Synesthesia_Audio.Services.Cloud;

/// <summary>
/// An explicit WebDAV endpoint and credentials.
///
/// Operations that need to work with values the user has typed but not yet saved (Browse, Test
/// Connection) pass one of these instead of temporarily overwriting the shared saved settings. The old
/// swap-then-restore approach only stayed applied until the modal page was shown, so the first folder
/// tap used stale credentials and the browse failed.
/// </summary>
public sealed record WebDavConnection(string ServerUrl, string Username, string Password)
{
    /// <summary>A connection is usable once it names a server and a user; an empty password is allowed
    /// because some servers take anonymous access.</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(ServerUrl) && !string.IsNullOrWhiteSpace(Username);

    /// <summary>Server URL with exactly one trailing slash, ready for path concatenation.</summary>
    public string NormalizedServerUrl =>
        string.IsNullOrWhiteSpace(ServerUrl) ? string.Empty : ServerUrl.TrimEnd('/') + "/";

    public static WebDavConnection FromSettings(GeneralSettings settings) =>
        new(settings.CloudServerUrl, settings.CloudUsername, settings.CloudPassword);
}
