namespace AITestAnalyzer
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
    }
}
