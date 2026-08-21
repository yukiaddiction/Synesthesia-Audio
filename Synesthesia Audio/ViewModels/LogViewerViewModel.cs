using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Synesthesia_Audio.Services.Logging;

namespace Synesthesia_Audio.ViewModels;

public partial class LogViewerViewModel : BaseViewModel
{
    private readonly LogService _logService;
    private bool _showingAll;

    [ObservableProperty]
    private string _logContent = string.Empty;

    [ObservableProperty]
    private string _modeLabel = "Last 500 lines (tap for all)";

    public LogViewerViewModel(LogService logService)
    {
        _logService = logService;
        Title = "Application Logs";
    }

    public async Task LoadTailAsync()
    {
        IsBusy = true;
        try
        {
            _showingAll = false;
            ModeLabel = "Last 500 lines (tap for all)";
            LogContent = await _logService.ReadTailAsync(500);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleModeAsync()
    {
        IsBusy = true;
        try
        {
            if (_showingAll)
            {
                await LoadTailAsync();
            }
            else
            {
                _showingAll = true;
                ModeLabel = "Showing all logs (tap for recent)";
                LogContent = await _logService.ReadAllLogsAsync();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_showingAll)
        {
            IsBusy = true;
            LogContent = await _logService.ReadAllLogsAsync();
            IsBusy = false;
        }
        else
        {
            await LoadTailAsync();
        }
    }

    [RelayCommand]
    private async Task CopyAsync()
    {
        await _logService.CopyLogsToClipboardAsync();
        await Shell.Current.DisplayAlert("Copied", "Log content copied to clipboard.", "OK");
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        await _logService.OpenLogFileAsync();
    }
}
