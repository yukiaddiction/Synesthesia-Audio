namespace Synesthesia_Audio.Core;

public enum ScanStatus
{
    Idle,
    Running,
    Completed,
    Failed,
    Interrupted
}

public class ScanState
{
    public ScanStatus Status { get; set; } = ScanStatus.Idle;
    public double Progress { get; set; }
    public string ProgressText { get; set; } = string.Empty;
    public int WorksFound { get; set; }
    public int TotalFiles { get; set; }
    public int FailedFolderCount { get; set; }
    public List<string> FailedFolders { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public bool AlertShown { get; set; }
    public bool IsRunning => Status == ScanStatus.Running;
}
