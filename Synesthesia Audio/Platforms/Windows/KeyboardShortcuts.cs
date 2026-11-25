#if WINDOWS
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace Synesthesia_Audio.Desktop;

/// <summary>
/// Desktop keyboard shortcuts.
///
/// Two rules keep them from fighting the focused control (UX-08):
/// the handler is registered WITHOUT <c>handledEventsToo</c>, so a key a control already consumed never reaches
/// us; and keys are ignored while anything that handles its own Space/arrows has focus. Before this, pressing
/// Space to toggle a focused button also started or stopped playback, because the handler ran for keys the button
/// had already handled and only text fields were treated as "typing".
/// </summary>
internal static class KeyboardShortcuts
{
    public static void Attach(Microsoft.UI.Xaml.Window? window)
    {
        if (window?.Content is not UIElement root)
            return;

        // Not handledEventsToo: a key the focused control already acted on is not ours to act on again.
        root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), handledEventsToo: false);
    }

    /// <summary>
    /// True for anything that legitimately consumes Space, the arrow keys or Escape itself. A bare
    /// <c>TextBox</c> check was not enough: buttons, sliders, switches and list items all handle Space.
    ///
    /// Fully qualified on purpose — these files import both Microsoft.Maui.Controls and
    /// Microsoft.UI.Xaml.Controls, so a bare "Button" is ambiguous and MAUI's type is the wrong one here.
    /// </summary>
    private static bool HandlesItsOwnKeys(object? focused) => focused is
        Microsoft.UI.Xaml.Controls.TextBox or
        Microsoft.UI.Xaml.Controls.PasswordBox or
        Microsoft.UI.Xaml.Controls.RichEditBox or
        Microsoft.UI.Xaml.Controls.AutoSuggestBox or
        Microsoft.UI.Xaml.Controls.Button or
        Microsoft.UI.Xaml.Controls.Primitives.RepeatButton or
        Microsoft.UI.Xaml.Controls.HyperlinkButton or
        Microsoft.UI.Xaml.Controls.CheckBox or
        Microsoft.UI.Xaml.Controls.RadioButton or
        Microsoft.UI.Xaml.Controls.Primitives.ToggleButton or
        Microsoft.UI.Xaml.Controls.ToggleSwitch or
        Microsoft.UI.Xaml.Controls.Slider or
        Microsoft.UI.Xaml.Controls.ComboBox or
        Microsoft.UI.Xaml.Controls.ListBox or
        Microsoft.UI.Xaml.Controls.ListView or
        Microsoft.UI.Xaml.Controls.ListViewItem or
        Microsoft.UI.Xaml.Controls.GridView or
        Microsoft.UI.Xaml.Controls.GridViewItem or
        Microsoft.UI.Xaml.Controls.ScrollViewer or
        Microsoft.UI.Xaml.Controls.DatePicker or
        Microsoft.UI.Xaml.Controls.TimePicker or
        Microsoft.UI.Xaml.Controls.CalendarDatePicker;

    private static void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            if (sender is not UIElement root || root.XamlRoot is null)
                return;

            var focused = FocusManager.GetFocusedElement(root.XamlRoot);

            // Ctrl+F stays global so search is always reachable, even mid-typing.
            if (e.Key == VirtualKey.F && IsDown(VirtualKey.Control))
            {
                UiEvents.RequestFocusSearch();
                e.Handled = true;
                return;
            }

            if (HandlesItsOwnKeys(focused))
                return;

            var audio = ServiceAccessor.GetService<AudioService>();

            switch (e.Key)
            {
                case VirtualKey.Space:
                    if (audio?.TogglePlayCommand.CanExecute(null) == true)
                        audio.TogglePlayCommand.Execute(null);
                    e.Handled = true;
                    break;

                case VirtualKey.Left:
                    audio?.SeekBy(TimeSpan.FromSeconds(-5));
                    e.Handled = true;
                    break;

                case VirtualKey.Right:
                    audio?.SeekBy(TimeSpan.FromSeconds(5));
                    e.Handled = true;
                    break;

                case VirtualKey.F5:
                    var main = ServiceAccessor.GetService<MainViewModel>();
                    if (main?.ScanLibraryCommand.CanExecute(null) == true)
                        main.ScanLibraryCommand.Execute(null);
                    e.Handled = true;
                    break;

                case VirtualKey.Escape:
                    // Marked handled so the key does not also reach the page underneath.
                    e.Handled = true;
                    _ = GoBackAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            // A shortcut must never break input handling, but a silent failure made a broken shortcut invisible.
            System.Diagnostics.Debug.WriteLine($"KeyboardShortcuts: handling a key failed: {ex.Message}");
        }
    }

    private static async Task GoBackAsync()
    {
        try
        {
            var navigation = Shell.Current?.Navigation;
            if (navigation != null && navigation.NavigationStack.Count > 1)
            {
                await navigation.PopAsync();
                return;
            }

            if (Shell.Current is { } shell)
                await shell.GoToAsync("//Library");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"KeyboardShortcuts: back navigation failed: {ex.Message}");
        }
    }

    private static bool IsDown(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
}
#endif
