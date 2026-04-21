namespace AITestAnalyzer.Models
{
    /// <summary>
    /// Contains the full output of a GEN Mode run including all generated
    /// test cases, pass statistics, token usage, and requirements source.
    /// </summary>
    public class GenModeResult
    {
        /// <summary>Final list of generated test cases after all refinement passes</summary>
        public List<GeneratedTestCase> TestCases { get; set; } = new();

        /// <summary>
        /// Total number of refinement passes completed.
        /// 1 = generation only (no refinement needed),
        /// up to 3 = full Generate → Critique → Refine × 2 loop.
        /// </summary>
        public int TotalPasses { get; set; }

        /// <summary>Total tokens consumed across all passes (generation + critique + refinement)</summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Indicates where the requirements markdown came from.
        /// "provided" = user supplied a file.
        /// "generated" = no file found, AI generated the markdown.
        /// </summary>
        public string RequirementsSource { get; set; } = "provided";

        /// <summary>UTC timestamp when this GEN Mode run completed</summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
