namespace Synesthesia_Audio.Services;

/// <summary>
/// The navigation a view model may request without knowing what Shell is.
///
/// View models called <c>Shell.Current.GoToAsync</c> and <c>Shell.Current.Navigation.PushModalAsync</c> directly,
/// which welded the decision "where should the user go next" to the Shell, so a view model could only be run
/// inside a live app (PAT-04). Pages themselves still use Shell directly - they are the UI.
/// </summary>
public interface INavigationService
{
    /// <summary>Navigates to a Shell route, optionally with query/parameter values.</summary>
    Task GoToAsync(string route);

    /// <summary>Navigates to a Shell route carrying construction arguments, as the album page needs.</summary>
    Task GoToAsync(string route, IDictionary<string, object> parameters);

    /// <summary>Presents a page as a modal, wrapped in its own navigation stack when it needs one.</summary>
    Task PushModalAsync(Page page);

    /// <summary>Navigates back one page when there is somewhere to go back to.</summary>
    Task GoBackAsync();
}
