using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Process-wide gate for page pushes. A double tap fires both tap recognisers, and since <c>GoToAsync</c>
/// animates, the second push would stack a duplicate page; different pages must not race each other either.
/// </summary>
public static class NavigationGuard
{
    public static bool IsNavigating => SingleFlight.IsRunning;

    /// <summary>
    /// Drops the call if a guarded navigation is already in flight; the page it would show is already on its way in.
    /// </summary>
    public static Task RunAsync(Func<Task> navigate) => SingleFlight.RunAsync(navigate);
}
