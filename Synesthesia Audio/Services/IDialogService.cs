namespace Synesthesia_Audio.Services;

/// <summary>
/// Lets view models and services ask the user things without depending on Shell, so that logic runs without a
/// live app.
/// </summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message, string ok = "OK");

    /// <summary>False when the UI is unavailable, so a caller never proceeds on an unasked confirmation.</summary>
    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Null when the user cancels or the UI is unavailable.</summary>
    Task<string?> ChooseAsync(string title, string cancel, params string[] options);
}
