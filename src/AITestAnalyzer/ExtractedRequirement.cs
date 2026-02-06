namespace AITestAnalyzer
{
    /// <summary>
    /// Represents a requirement extracted from a requirement document.
    /// Uses semantic structure (Topic/Subtopic/Action) instead of IDs.
    /// </summary>
    public class ExtractedRequirement
    {
        /// <summary>
        /// High-level feature area (e.g., "Task Management", "User Authentication")
        /// </summary>
        public string Topic { get; set; } = string.Empty;

        /// <summary>
        /// Specific functionality within the topic (e.g., "Create Task", "Login")
        /// </summary>
        public string Subtopic { get; set; } = string.Empty;

        /// <summary>
        /// What the system should do (e.g., "Allow users to create tasks with title and description")
        /// </summary>
        public string ExpectedAction { get; set; } = string.Empty;

        /// <summary>
        /// Whether this requirement is testable
        /// </summary>
        public bool IsTestable { get; set; } = true;
    }
}
