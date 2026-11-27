using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services;

/// <summary>
/// Shell-backed dialogs that degrade to "no answer" without a window: alerts are logged, confirmations declined,
/// choices cancelled. A background scan finishing with no window must not throw, and an unshown confirmation must
/// never count as accepted.
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
