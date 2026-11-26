namespace Synesthesia_Audio.Services;

/// <summary>
/// The user-facing questions a view model or service may ask, without knowing what shows them.
///
/// View models called <c>Shell.Current.DisplayAlert</c> directly, which meant the code that decides "should this
/// be announced?" was welded to the UI that announces it: none of it could be exercised without a running app, and
/// a service raising an alert was pulling a page into a layer that should not know pages exist (PAT-04).
/// </summary>
public interface IDialogService
{
    /// <summary>Shows a message with a single dismiss button.</summary>
    Task AlertAsync(string title, string message, string ok = "OK");

    /// <summary>
    /// Asks a yes/no question. Returns true when the user accepts. If the UI is not available the answer is
    /// false, so a caller never proceeds on an unasked confirmation.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>
    /// Shows a list of choices. Returns the chosen value, or null when the user cancels or the UI is unavailable.
    /// </summary>
    Task<string?> ChooseAsync(string title, string cancel, params string[] options);
}
