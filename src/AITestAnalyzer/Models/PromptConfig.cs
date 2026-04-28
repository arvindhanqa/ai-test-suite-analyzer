namespace AITestAnalyzer.Models
{
    public class PromptConfig
    {
        public int MaxTokens { get; set; }
        public string Model { get; set; } = "";
        public double Temperature { get; set; }
        public string SystemMessage { get; set; } = "";
        public string UserTemplate { get; set; } = "";
        public double CostPerToken { get; set; }

        /// <summary>
        /// OpenAI model used for GEN Mode (generate, critique, refine passes).
        /// Defaults to gpt-4.1-mini — better instruction following and 1M token
        /// context window vs gpt-4o-mini's 128K. QA and BA modes use Model instead.
        /// </summary>
        public string GenModel { get; set; } = "gpt-4.1-mini";

        /// <summary>
        /// System message for the GENERATE pass.
        /// Instructs the model to produce pipe-delimited test cases from requirements.
        /// Format: TC-GEN-001|Feature|Scenario|Priority|Steps|ExpectedResult
        /// </summary>
        public string GenSystemMessage { get; set; } = "";

        /// <summary>
        /// User message template for the GENERATE pass.
        /// Placeholders: {targetCount}, {requirementsMarkdown}
        /// </summary>
        public string GenUserTemplate { get; set; } = "";

        /// <summary>
        /// System message for the CRITIQUE pass.
        /// Instructs the model to review generated test cases and output
        /// pipe-delimited critique: TC-GEN-001|KEEP|No issues
        /// Action values: KEEP, REVISE, DROP
        /// </summary>
        public string CritiqueSystemMessage { get; set; } = "";

        /// <summary>
        /// User message template for the CRITIQUE pass.
        /// Placeholders: {requirementsMarkdown}, {testCases}
        /// </summary>
        public string CritiqueUserTemplate { get; set; } = "";

        /// <summary>
        /// System message for the REFINE pass.
        /// Instructs the model to apply critique feedback — drop DROP items,
        /// revise REVISE items, keep KEEP items unchanged.
        /// Output format identical to GENERATE pass.
        /// </summary>
        public string RefineSystemMessage { get; set; } = "";
        /// <summary>
        /// User message template for the REFINE pass.
        /// Placeholders: {testCases}, {critiqueResults}
        /// </summary>
        public string RefineUserTemplate { get; set; } = "";
    }
}
