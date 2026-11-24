using System;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Cross-platform channel for shortcuts that need to reach a specific page.
/// Kept deliberately tiny: the Windows key handler raises these, pages opt in while visible.
/// </summary>
public static class UiEvents
{
    /// <summary>Raised when the user presses Ctrl+F; the library page focuses its search box.</summary>
    public static event Action? FocusSearchRequested;

    public static void RequestFocusSearch() => FocusSearchRequested?.Invoke();
}
