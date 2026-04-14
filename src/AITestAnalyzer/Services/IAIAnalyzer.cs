using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    public interface IAIAnalyzer
    {
        Task<(string quality, int tokens)> AnalyzeTestQualityAsync(TestCase testCase);
        Task<(string reqFeedback, List<string> coverageIds, int tokens)> AnalyzeCoverageAndFeedbackAsync(
            TestCase testCase,
            List<ExtractedRequirement> requirements);
    }
}
