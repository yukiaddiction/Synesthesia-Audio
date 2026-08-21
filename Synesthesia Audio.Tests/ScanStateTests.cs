using System.Text.Json;
using Synesthesia_Audio.Core;

namespace Synesthesia_Audio.Tests;

public class ScanStateTests
{
    [Fact]
    public void DefaultsToIdle()
    {
        var state = new ScanState();
        Assert.Equal(ScanStatus.Idle, state.Status);
        Assert.False(state.IsRunning);
        Assert.Empty(state.FailedFolders);
        Assert.Equal(string.Empty, state.ProgressText);
    }

    [Fact]
    public void RoundTripsRunningState()
    {
        var state = new ScanState
        {
            Status = ScanStatus.Running,
            Progress = 0.42,
            ProgressText = "Scan Library Found 3 Works, 12 Audio Files",
            WorksFound = 3,
            TotalFiles = 12,
            StartedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<ScanState>(json);

        Assert.NotNull(restored);
        Assert.Equal(ScanStatus.Running, restored!.Status);
        Assert.Equal(0.42, restored.Progress);
        Assert.Equal("Scan Library Found 3 Works, 12 Audio Files", restored.ProgressText);
        Assert.Equal(3, restored.WorksFound);
        Assert.Equal(12, restored.TotalFiles);
        Assert.True(restored.IsRunning);
        Assert.Equal(state.StartedAt, restored.StartedAt);
    }

    [Fact]
    public void RoundTripsFailedStateWithFolders()
    {
        var state = new ScanState
        {
            Status = ScanStatus.Failed,
            Progress = 1.0,
            ErrorMessage = "Scan finished but 2 folders could not be accessed",
            FailedFolderCount = 2,
            FailedFolders = new List<string> { "/mnt/a", "/mnt/b" },
            FinishedAt = DateTimeOffset.UtcNow,
            AlertShown = true
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<ScanState>(json);

        Assert.NotNull(restored);
        Assert.Equal(ScanStatus.Failed, restored!.Status);
        Assert.Equal("Scan finished but 2 folders could not be accessed", restored.ErrorMessage);
        Assert.Equal(2, restored.FailedFolderCount);
        Assert.Equal(2, restored.FailedFolders.Count);
        Assert.Equal("/mnt/b", restored.FailedFolders[1]);
        Assert.True(restored.AlertShown);
        Assert.False(restored.IsRunning);
    }

    [Fact]
    public void RoundTripsCompletedState()
    {
        var state = new ScanState
        {
            Status = ScanStatus.Completed,
            Progress = 1.0,
            WorksFound = 25,
            TotalFiles = 300,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            FinishedAt = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<ScanState>(json);

        Assert.NotNull(restored);
        Assert.Equal(ScanStatus.Completed, restored!.Status);
        Assert.Equal(25, restored.WorksFound);
        Assert.Equal(300, restored.TotalFiles);
        Assert.False(restored.IsRunning);
    }

    [Fact]
    public void RoundTripsInterruptedState()
    {
        var state = new ScanState
        {
            Status = ScanStatus.Interrupted,
            ErrorMessage = "The previous scan was interrupted while the app was closed.",
            AlertShown = false
        };

        var json = JsonSerializer.Serialize(state);
        var restored = JsonSerializer.Deserialize<ScanState>(json);

        Assert.NotNull(restored);
        Assert.Equal(ScanStatus.Interrupted, restored!.Status);
        Assert.Equal("The previous scan was interrupted while the app was closed.", restored.ErrorMessage);
        Assert.False(restored.AlertShown);
    }
}
