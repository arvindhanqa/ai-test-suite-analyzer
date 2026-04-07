namespace AITestAnalyzer.Models
{
    /// <summary>
    /// Represents a requirement extracted from a requirement document.
    /// Supports both verbose format (Topic/Subtopic/ExpectedAction) and 
    /// compressed format (Id/Key/Description) for token efficiency.
    /// </summary>
    public class ExtractedRequirement
    {
        // === COMPRESSED FORMAT (NEW) ===
        /// <summary>
        /// Short unique identifier (e.g., UA-01, TM-05)
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Hierarchical path (e.g., user.auth.login, task.create)
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 8-15 word compressed description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Testable flag (1 or 0 in compressed format)
        /// </summary>
        public int TestableFlag { get; set; } = 1;

        // === VERBOSE FORMAT (LEGACY - for backward compatibility) ===
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

        // === HELPER METHODS ===

        /// <summary>
        /// Check if this uses compressed format (has Id and Key)
        /// </summary>
        public bool IsCompressedFormat()
        {
            return !string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Key);
        }

        /// <summary>
        /// Parse from pipe-delimited format: ID|key|description|testable
        /// Example: UA-01|user.auth.login|users login with email and password|1
        /// </summary>
        public static ExtractedRequirement ParsePipeDelimited(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentException("Line cannot be empty", nameof(line));

            var parts = line.Split('|');
            if (parts.Length != 4)
                throw new FormatException($"Invalid format. Expected 4 parts, got {parts.Length}. Line: {line}");

            return new ExtractedRequirement
            {
                Id = parts[0].Trim(),
                Key = parts[1].Trim(),
                Description = parts[2].Trim(),
                TestableFlag = parts[3].Trim() == "1" ? 1 : 0
            };
        }

        /// <summary>
        /// Get human-readable display format for Excel/UI
        /// Compressed: "user.auth.login" → "User → Auth → Login"
        /// Verbose: Returns Subtopic or Topic
        /// </summary>
        public string GetDisplayText()
        {
            if (IsCompressedFormat())
            {
                // Convert key to readable: user.auth.login → User → Auth → Login
                var parts = Key.Split('.');
                var formatted = parts.Select(part =>
                    System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(part.Replace("_", " "))
                );
                return string.Join(" → ", formatted);
            }
            else
            {
                // Legacy format
                return !string.IsNullOrWhiteSpace(Subtopic) ? Subtopic : Topic;
            }
        }

        /// <summary>
        /// Format for AI prompt (includes description/action)
        /// </summary>
        public string FormatForPrompt()
        {
            if (IsCompressedFormat())
            {
                return $"• {GetDisplayText()}: {Description}";
            }
            else
            {
                return $"• {Topic} → {Subtopic}: {ExpectedAction}";
            }
        }
    }
}
