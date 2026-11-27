using System;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Lets the Windows key handler reach a specific page; pages subscribe only while visible.
/// </summary>
public static class UiEvents
{
    /// <summary>Ctrl+F.</summary>
    public static event Action? FocusSearchRequested;

    public static void RequestFocusSearch() => FocusSearchRequested?.Invoke();
}
