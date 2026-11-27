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
/// Shortcuts must not fight the focused control: the handler skips keys a control already handled, and ignores
/// keys while a control that handles Space/arrows itself has focus.
/// </summary>
internal static class KeyboardShortcuts
{
    private static AudioService? _audio;
    private static MainViewModel? _main;

    public static void Attach(Microsoft.UI.Xaml.Window? window, AudioService? audio, MainViewModel? main)
    {
        if (window?.Content is not UIElement root)
            return;

        _audio = audio;
        _main = main;

        // Not handledEventsToo: a key the focused control already acted on is not ours to act on again.
        root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), handledEventsToo: false);
    }

    // Buttons, sliders, switches and list items handle Space too, not just text boxes. Types are fully qualified
    // because the bare names also resolve to MAUI's controls.
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

            var audio = _audio;
            var main = _main;

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

                    if (main?.ScanLibraryCommand.CanExecute(null) == true)
                        main.ScanLibraryCommand.Execute(null);
                    e.Handled = true;
                    break;

                case VirtualKey.Escape:
                    // Marked handled so Escape does not also reach the page underneath.
                    e.Handled = true;
                    _ = GoBackAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            // A shortcut must never break input handling, but the failure should still be visible.
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
