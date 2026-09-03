namespace AITestAnalyzer.Models
{
    public class ArchitecturePlan
    {
        public List<SectionTestPlan> Sections { get; set; } = new();
        public List<IntegrationFlow> IntegrationFlows { get; set; } = new();
        public int TotalSectionTests { get; set; }
        public int TotalIntegrationTests { get; set; }
    }
}
