using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Synesthesia_Audio.Services.Logging;

using Synesthesia_Audio.Services;

namespace Synesthesia_Audio.ViewModels;

public partial class LogViewerViewModel : BaseViewModel
{
    private readonly LogService _logService;
    private bool _showingAll;

    [ObservableProperty]
    private string _logContent = string.Empty;

    [ObservableProperty]
    private string _modeLabel = "Last 500 lines (tap for all)";

    private readonly IDialogService _dialog;

    public LogViewerViewModel(LogService logService, IDialogService dialogService)
    {
        _logService = logService;
        _dialog = dialogService;
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
        await _dialog.AlertAsync("Copied", "Log content copied to clipboard.", "OK");
    }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        await _logService.OpenLogFileAsync();
    }
}
