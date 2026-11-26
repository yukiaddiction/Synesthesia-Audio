using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services;

/// <summary>
/// The <see cref="IDialogService"/> implementation backed by the current Shell.
///
/// Every method degrades to "no answer" when there is no Shell to show: an alert is logged, a confirmation is
/// declined, and a choice is cancelled. That is deliberate - a background scan finishing while the window is gone
/// must not throw, and a confirmation that cannot be shown must never be treated as accepted (PAT-04).
/// </summary>
public sealed class DialogService : IDialogService
{
    private readonly ILogger<DialogService> _logger;

    public DialogService(ILogger<DialogService> logger) => _logger = logger;

    public async Task AlertAsync(string title, string message, string ok = "OK")
    {
        if (Shell.Current is not { } shell)
        {
            _logger.LogInformation("Alert without a window ({Title}): {Message}", title, message);
            return;
        }

        try
        {
            await shell.DisplayAlert(title, message, ok);
        }
        catch (Exception ex)
        {
            // A dialog failing must not take down the operation that wanted to report something.
            _logger.LogDebug(ex, "Showing an alert failed ({Title})", title);
        }
    }

    public async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        if (Shell.Current is not { } shell)
        {
            _logger.LogInformation("Confirmation without a window, declined ({Title}): {Message}", title, message);
            return false;
        }

        try
        {
            return await shell.DisplayAlert(title, message, accept, cancel);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Showing a confirmation failed ({Title})", title);
            return false;
        }
    }

    public async Task<string?> ChooseAsync(string title, string cancel, params string[] options)
    {
        if (Shell.Current is not { } shell)
        {
            _logger.LogInformation("Choice without a window, cancelled ({Title})", title);
            return null;
        }

        try
        {
            return await shell.DisplayActionSheet(title, cancel, null, options);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Showing a choice failed ({Title})", title);
            return null;
        }
    }
}
