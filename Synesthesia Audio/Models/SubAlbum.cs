using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Synesthesia_Audio.Models;

public class SubAlbum : INotifyPropertyChanged
{
    public string Title { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public ObservableCollection<Track> Tracks { get; set; } = new();

    private bool _isExpanded = true;
    [JsonIgnore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
