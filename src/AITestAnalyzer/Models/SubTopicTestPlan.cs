namespace AITestAnalyzer.Models
{
    public class SubTopicTestPlan
    {
        public string SubTopicName { get; set; } = string.Empty;
        public int RecommendedTests { get; set; }
        public int PositiveTests { get; set; }
        public int NegativeTests { get; set; }
        public string Rationale { get; set; } = string.Empty;
    }
}
