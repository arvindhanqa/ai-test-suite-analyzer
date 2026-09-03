using AITestAnalyzer.Models;

namespace AITestAnalyzer.Models
{
    public class ArchModeResult
    {
        public ArchitecturePlan Plan { get; set; } = new();
        public List<GeneratedTestCase> AllTestCases { get; set; } = new();
        public int TotalPasses { get; set; }
        public int TotalTokens { get; set; }
        public string RequirementsSource { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
    }
}
