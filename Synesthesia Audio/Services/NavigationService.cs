using Microsoft.Extensions.Logging;

namespace Synesthesia_Audio.Services;

/// <summary>
/// The <see cref="INavigationService"/> implementation over the current Shell.
///
/// Every method degrades to doing nothing when there is no window: a background operation that finishes after the
/// window closed must not throw, and "go back" with nothing to go back to is not an error (PAT-04).
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly ILogger<NavigationService> _logger;

    public NavigationService(ILogger<NavigationService> logger) => _logger = logger;

    public Task GoToAsync(string route) => GoToAsync(route, new Dictionary<string, object>());

    public async Task GoToAsync(string route, IDictionary<string, object> parameters)
    {
        if (Shell.Current is not { } shell)
        {
            _logger.LogInformation("Navigation to {Route} ignored: no window", route);
            return;
        }

        try
        {
            if (parameters.Count > 0)
                await shell.GoToAsync(route, parameters);
            else
                await shell.GoToAsync(route);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Navigation to {Route} failed", route);
        }
    }

    public async Task PushModalAsync(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (Shell.Current is not { } shell)
        {
            _logger.LogInformation("Modal push ignored: no window");
            return;
        }

        try
        {
            await shell.Navigation.PushModalAsync(page);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pushing a modal page failed");
        }
    }

    public async Task GoBackAsync()
    {
        if (Shell.Current is not { } shell)
            return;

        try
        {
            if (shell.Navigation.NavigationStack.Count > 1)
                await shell.Navigation.PopAsync();
            else
                await shell.GoToAsync("//Library");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Going back failed");
        }
    }
}
