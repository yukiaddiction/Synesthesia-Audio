namespace Synesthesia_Audio.Services;

/// <summary>
/// Navigation for view models, so they don't depend on Shell and can run outside a live app. Pages still use
/// Shell directly; they are the UI.
/// </summary>
public interface INavigationService
{
    Task GoToAsync(string route);

    Task GoToAsync(string route, IDictionary<string, object> parameters);

    /// <summary>Wraps the page in its own navigation stack when it needs one.</summary>
    Task PushModalAsync(Page page);

    Task GoBackAsync();
}
