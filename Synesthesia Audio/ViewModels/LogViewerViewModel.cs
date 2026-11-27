using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Synesthesia_Audio.Services;
using Synesthesia_Audio.Services.Logging;

namespace Synesthesia_Audio.ViewModels;

public partial class LogViewerViewModel : BaseViewModel
{
    private readonly LogService _logService;
    private readonly IDialogService _dialog;
    private bool _showingAll;
    private string _logContent = string.Empty;

    /// <summary>Split into lines so a virtualised CollectionView can show a huge log without laying it all out at once.</summary>
    public ObservableCollection<string> LogLines { get; } = new();

    public string LogContent => _logContent;

    [ObservableProperty]
    private string _modeLabel = "Last 500 lines (tap for all)";

    public LogViewerViewModel(LogService logService, IDialogService dialogService)
    {
        _logService = logService;
        _dialog = dialogService;
        Title = "Application Logs";
    }

    // Updates text and lines together so they can never disagree.
    private void SetLogContent(string content)
    {
        _logContent = content ?? string.Empty;
        OnPropertyChanged(nameof(LogContent));

        LogLines.Clear();
        foreach (var line in _logContent.Split('\n'))
            LogLines.Add(line.TrimEnd('\r'));
    }

    public async Task LoadTailAsync()
    {
        IsBusy = true;
        try
        {
            _showingAll = false;
            ModeLabel = "Last 500 lines (tap for all)";
            SetLogContent(await _logService.ReadTailAsync(500));
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
                SetLogContent(await _logService.ReadAllLogsAsync());
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
            try
            {
                SetLogContent(await _logService.ReadAllLogsAsync());
            }
            finally
            {
                IsBusy = false;
            }
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