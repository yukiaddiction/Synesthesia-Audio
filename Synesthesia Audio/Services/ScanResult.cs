namespace Synesthesia_Audio.Services;

public class ScanResult
{
    public List<Models.WorkMetadata> Works { get; set; } = new();
    public List<string> FailedFolders { get; set; } = new();
    public int TotalFiles { get; set; }
    public int TotalWorks => Works.Count;
}
