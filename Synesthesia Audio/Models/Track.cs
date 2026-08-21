using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Synesthesia_Audio.Models;

public partial class Track : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _translatedTitle = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    [JsonIgnore]
    public string CoverImagePath { get; set; } = string.Empty;

    [JsonIgnore]
    public string VoiceActors { get; set; } = string.Empty;

    [JsonIgnore]
    [ObservableProperty]
    private TimeSpan _duration;
}
