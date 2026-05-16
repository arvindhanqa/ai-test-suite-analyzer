using System.Text.Json;
using AITestAnalyzer.Config;
using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    public static class JsonExporter
    {
        public static string Export(
            List<(string TestId, string Result, int Tokens, string Coverage)> results,
            AnalysisMode analysisMode,
            int cacheHits,
            int apiCalls,
            DateTime startTime,
            DateTime endTime,
            PromptConfig promptConfig,
            string outputPath)
        {
            int totalTokens = results.Sum(r => r.Tokens);
            double totalCost = totalTokens * promptConfig.CostPerToken;
            double durationSeconds = (endTime - startTime).TotalSeconds;

            int goodTests = results.Count(r => r.Result.StartsWith(Constants.RESULT_GOOD));
            int errorTests = results.Count(r => r.Result.StartsWith(Constants.RESULT_ERROR_PREFIX));
            int issueTests = results.Count - goodTests - errorTests;
            double qualityScore = results.Count > 0 ? goodTests * 100.0 / results.Count : 0;

            var export = new
            {
                metadata = new
                {
                    generatedAt = DateTime.Now,
                    analysisMode = analysisMode.ToString(),
                    totalTests = results.Count,
                    cacheHits,
                    apiCalls,
                    totalTokens,
                    estimatedCostUsd = Math.Round(totalCost, 6),
                    durationSeconds = Math.Round(durationSeconds, 1)
                },
                summary = new
                {
                    goodTests,
                    testsWithIssues = issueTests,
                    errors = errorTests,
                    qualityScorePct = Math.Round(qualityScore, 1)
                },
                results = results.Select(r => new
                {
                    testId = r.TestId,
                    analysis = r.Result,
                    coverage = r.Coverage,
                    tokens = r.Tokens
                })
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(export, options);

            string jsonPath = outputPath.Replace(".xlsx", ".json");
            File.WriteAllText(jsonPath, json);

            return jsonPath;
        }

        /// <summary>
        /// GEN MODE: Exports a GenModeResult to a JSON file alongside the Excel output.
        /// Output filename matches the Excel file with .json extension.
        /// </summary>
        /// <param name="result">GEN Mode result containing test cases and statistics.</param>
        /// <param name="promptConfig">Prompt configuration for cost calculations.</param>
        /// <param name="elapsed">Total elapsed time for the GEN Mode run.</param>
        /// <param name="outputPath">Path to the Excel output file — JSON saved alongside it.</param>
        /// <returns>Full path to the created JSON file.</returns>
        public static string Export(
            GenModeResult result,
            PromptConfig promptConfig,
            TimeSpan elapsed,
            string outputPath)
        {
            int total = result.TestCases.Count;
            int goodCount = result.TestCases.Count(t =>
                t.QAScore.StartsWith(Constants.RESULT_GOOD, StringComparison.OrdinalIgnoreCase));
            int errorCount = result.TestCases.Count(t =>
                t.QAScore.StartsWith(Constants.RESULT_ERROR_PREFIX, StringComparison.OrdinalIgnoreCase));
            int issueCount = total - goodCount - errorCount;
            int revisedCount = result.TestCases.Count(t => t.PassNumber > 1);
            double totalCost = result.TotalTokens * promptConfig.CostPerToken;

            var export = new
            {
                metadata = new
                {
                    generatedAt = result.GeneratedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    requirementsSource = result.RequirementsSource,
                    totalPasses = result.TotalPasses,
                    totalTokens = result.TotalTokens,
                    estimatedCostUsd = Math.Round(totalCost, 6),
                    durationSeconds = Math.Round(elapsed.TotalSeconds, 1)
                },
                summary = new
                {
                    testsGenerated = total,
                    testsPassed = goodCount,
                    testsRevised = revisedCount,
                    testsDropped = 0,
                    qaScoreGood = total > 0 ? Math.Round(goodCount * 100.0 / total, 1) : 0,
                    qaScoreIssues = total > 0 ? Math.Round(issueCount * 100.0 / total, 1) : 0
                },
                testCases = result.TestCases.Select(t => new
                {
                    testId = t.TestId,
                    feature = t.Feature,
                    scenario = t.Scenario,
                    priority = t.Priority,
                    steps = t.Steps,
                    expectedResult = t.ExpectedResult,
                    passNumber = t.PassNumber,
                    qaScore = t.QAScore
                })
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(export, options);
            string jsonPath = outputPath.Replace(".xlsx", ".json");
            File.WriteAllText(jsonPath, json);

            return jsonPath;
        }
    }
}
