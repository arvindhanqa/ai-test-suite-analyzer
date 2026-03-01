using System.Text.Json;

namespace AITestAnalyzer
{
    /// <summary>
    /// Manages batch checkpoint file for resume capability.
    /// Checkpoint is saved in the batch folder as checkpoint.json.
    /// </summary>
    public class CheckpointManager
    {
        private readonly string _checkpointPath;

        public CheckpointManager(string folderPath)
        {
            _checkpointPath = Path.Combine(folderPath, "checkpoint.json");
        }

        public bool CheckpointExists() => File.Exists(_checkpointPath);

        public BatchCheckpoint? Load()
        {
            if (!File.Exists(_checkpointPath))
                return null;

            try
            {
                string json = File.ReadAllText(_checkpointPath);
                return JsonSerializer.Deserialize<BatchCheckpoint>(json);
            }
            catch
            {
                Console.WriteLine("⚠️  Could not read checkpoint file — starting from beginning.");
                return null;
            }
        }

        public void Save(BatchCheckpoint checkpoint)
        {
            try
            {
                checkpoint.LastUpdatedAt = DateTime.Now;
                string json = JsonSerializer.Serialize(checkpoint, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_checkpointPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Could not save checkpoint: {ex.Message}");
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(_checkpointPath))
                    File.Delete(_checkpointPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Could not delete checkpoint: {ex.Message}");
            }
        }
    }
}
