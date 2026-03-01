namespace AITestAnalyzer
{
    /// <summary>
    /// Represents the saved state of an in-progress batch run.
    /// Stored as checkpoint.json in the batch folder.
    /// </summary>
    public class BatchCheckpoint
    {
        public string BatchId { get; set; } = "";
        public string FolderPath { get; set; } = "";
        public int TotalFiles { get; set; }
        public List<string> CompletedFileNames { get; set; } = new();
        public DateTime StartedAt { get; set; }
        public DateTime LastUpdatedAt { get; set; }
    }
}
