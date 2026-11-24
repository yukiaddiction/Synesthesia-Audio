#if WINDOWS
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.ViewModels;
using Windows.System;
using Windows.UI.Core;

namespace Synesthesia_Audio.Desktop;

/// <summary>
/// Desktop keyboard shortcuts. Plain keys are ignored while a text field has focus, otherwise
/// typing "s" in the search box would start and stop playback.
/// </summary>
internal static class KeyboardShortcuts
{
    public static void Attach(Microsoft.UI.Xaml.Window? window)
    {
        if (window?.Content is not UIElement root)
            return;

        root.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), handledEventsToo: true);
    }

    private static void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            if (sender is not UIElement root || root.XamlRoot is null)
                return;

            var focused = FocusManager.GetFocusedElement(root.XamlRoot);
            var isTyping = focused is TextBox or PasswordBox or RichEditBox or AutoSuggestBox;
            var ctrlDown = IsDown(VirtualKey.Control);

            // Ctrl+F stays global so search is always reachable.
            if (ctrlDown && e.Key == VirtualKey.F)
            {
                UiEvents.RequestFocusSearch();
                e.Handled = true;
                return;
            }

            if (isTyping)
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
                    _ = GoBackAsync();
                    break;
            }
        }
        catch
        {
            // A shortcut must never break input handling.
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
        catch
        {
            // Navigation failure is not worth surfacing for an Escape keypress.
        }
    }

    private static bool IsDown(VirtualKey key)
        => (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down;
}
#endif
