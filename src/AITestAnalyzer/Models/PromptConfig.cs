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

        // GEN Mode model — uses gpt-4.1-mini for better instruction following
        public string GenModel { get; set; } = "gpt-4.1-mini";

        // GEN Mode — Generate prompt
        public string GenSystemMessage { get; set; } = "";
        public string GenUserTemplate { get; set; } = "";

        // GEN Mode — Critique prompt
        public string CritiqueSystemMessage { get; set; } = "";
        public string CritiqueUserTemplate { get; set; } = "";

        // GEN Mode — Refine prompt
        public string RefineSystemMessage { get; set; } = "";
        public string RefineUserTemplate { get; set; } = "";
    }
}
