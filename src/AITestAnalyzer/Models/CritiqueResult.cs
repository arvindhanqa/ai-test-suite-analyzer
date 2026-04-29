namespace AITestAnalyzer.Models
{
    /// <summary>
    /// Represents the critique result for a single generated test case.
    /// Produced by the CRITIQUE pass in GEN Mode.
    /// </summary>
    public class CritiqueResult
    {
        /// <summary>
        /// Test case ID this critique applies to (e.g. TC-GEN-001).
        /// Must match the TestId of the corresponding GeneratedTestCase.
        /// </summary>
        public string TestId { get; set; } = "";

        /// <summary>
        /// Action to take on this test case.
        /// KEEP = complete and clear, no changes needed.
        /// REVISE = valid but needs improvement — apply Reason as feedback.
        /// DROP = duplicate, irrelevant, or untestable — remove from output.
        /// </summary>
        public string Action { get; set; } = "KEEP";

        /// <summary>
        /// Specific and actionable reason for the action.
        /// "No issues" for KEEP items.
        /// References requirement IDs where relevant for REVISE and DROP items.
        /// </summary>
        public string Reason { get; set; } = "";
    }
}
