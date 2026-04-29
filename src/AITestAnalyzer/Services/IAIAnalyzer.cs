using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    public interface IAIAnalyzer
    {
        Task<(string quality, int tokens)> AnalyzeTestQualityAsync(TestCase testCase);
        Task<(string reqFeedback, List<string> coverageIds, int tokens)> AnalyzeCoverageAndFeedbackAsync(
            TestCase testCase,
            List<ExtractedRequirement> requirements);

        /// <summary>
        /// GEN MODE: Generates test cases from requirements markdown.
        /// Uses gpt-4.1-mini and GenSystemMessage/GenUserTemplate from PromptConfig.
        /// </summary>
        Task<(List<GeneratedTestCase> TestCases, int Tokens)> GenerateTestCasesAsync(
            string requirementsMarkdown,
            int targetCount);
    }
}
