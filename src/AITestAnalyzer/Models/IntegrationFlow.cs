public class IntegrationFlow
{
    public string FlowName { get; set; } = string.Empty;
    public List<string> SectionsInvolved { get; set; } = new();
    public int RecommendedTests { get; set; }
    public string Description { get; set; } = string.Empty;
}
