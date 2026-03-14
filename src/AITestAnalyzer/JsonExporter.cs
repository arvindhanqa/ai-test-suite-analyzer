using System.Text.Json;

namespace AITestAnalyzer
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
    }
}
