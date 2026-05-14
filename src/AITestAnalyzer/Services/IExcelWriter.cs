using AITestAnalyzer.Config;
using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    public interface IExcelWriter
    {
        void RenameOriginalSheet();
        void AddAnalysisColumnHeader(AnalysisMode mode = AnalysisMode.BA);
        void WriteAnalysis(int rowNumber, string analysis, string coverage, AnalysisMode mode = AnalysisMode.BA);
        void FlushAnalysis();
        void CreateQualityIssuesSheet(List<(string TestId, string Result, int Tokens, string Coverage)> results);
        void CreateStatisticsDashboard(List<(string TestId, string Result, int Tokens, string Coverage)> results, DateTime startTime, DateTime endTime);
        void CreateCoverageGapSheet(List<(string TestId, string Result, int Tokens, string Coverage)> results, List<ExtractedRequirement> requirements);
        void CreateBAStatisticsDashboard(List<(string TestId, string Result, int Tokens, string Coverage)> results, List<ExtractedRequirement> requirements, int totalTokens, int cacheHits, TimeSpan elapsed);

        /// <summary>
        /// GEN MODE: Creates the "Generated Tests" sheet containing all AI-generated test cases.
        /// Columns: Test ID | Feature | Scenario | Priority | Steps | Expected Result | Pass | QA Score
        /// Header row: dark green (distinct from QA blue and BA coral).
        /// </summary>
        void CreateGeneratedTestsSheet(List<GeneratedTestCase> testCases);

        /// <summary>
        /// GEN MODE: Creates the "Gen Statistics Dashboard" sheet with generation metrics.
        /// Sections: Generation Summary, QA Score Summary, Cost and Performance, Requirements Source.
        /// </summary>
        void CreateGenStatisticsDashboard(GenModeResult result, TimeSpan elapsed);
    }
}
