using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Services;

/// <summary>
/// One gate for every page push.
///
/// A double tap on an album card fires both the single-tap and the double-tap gesture recognisers, and each
/// handler pushed a page. Because <c>GoToAsync</c> takes a moment to animate, the second push started while
/// the first was still in flight, stacking two identical pages — the back button then needed two presses and
/// the album toolbar appeared twice. The same applied to clicking the mini player twice, and to double
/// clicking the "Manage Tag Mappings" button.
///
/// Callers wrap the push in <see cref="RunAsync"/>. The gate is process-wide rather than per-page: two
/// different pages must not race each other either. The primitive lives in Core so it is unit-tested.
/// </summary>
public static class NavigationGuard
{
    /// <summary>True while a guarded navigation is still running.</summary>
    public static bool IsNavigating => SingleFlight.IsRunning;

    /// <summary>
    /// Runs <paramref name="navigate"/> unless another guarded navigation is already in flight, in which case
    /// the call is dropped. A dropped push is the correct outcome: the page it would have shown is the one
    /// already on its way in.
    /// </summary>
    public static Task RunAsync(Func<Task> navigate) => SingleFlight.RunAsync(navigate);
}
