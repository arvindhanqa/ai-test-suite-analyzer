namespace AITestAnalyzer.Models
{
    public class SectionTestPlan
    {
        public string SectionName { get; set; } = string.Empty;
        public string TestIdPrefix { get; set; } = string.Empty;
        public List<SubTopicTestPlan> SubTopics { get; set; } = new();
        public int TotalRecommended { get; set; }
        public string RiskLevel { get; set; } = string.Empty;
    }
}
