using AITestAnalyzer.Models;
using AITestAnalyzer.Services;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace AITestAnalyzer.Tests
{
    public class JsonExporterTests
    {
        private string CreateOutputDir() =>
            Path.Combine(Directory.GetCurrentDirectory(),
                $"TestJsonOutput_{Guid.NewGuid():N}");

        private PromptConfig CreatePromptConfig() => new PromptConfig
        {
            Model = "gpt-4o-mini",
            GenModel = "gpt-4.1-mini",
            CostPerToken = 0.00000015
        };

        private GenModeResult CreateFakeGenModeResult(int testCount = 3) => new GenModeResult
        {
            TestCases = Enumerable.Range(1, testCount).Select(i => new GeneratedTestCase
            {
                TestId = $"TC-GEN-{i:D3}",
                Feature = "User Registration",
                Scenario = $"Scenario {i}",
                Priority = "High",
                Steps = "Step 1. Do X\nStep 2. Do Y",
                ExpectedResult = "Expected result",
                PassNumber = 1,
                QAScore = "GOOD"
            }).ToList(),
            TotalPasses = 2,
            TotalTokens = 5000,
            RequirementsSource = "provided",
            GeneratedAt = DateTime.UtcNow
        };

        // ============================================================
        // FILE CREATION TESTS
        // ============================================================

        [Fact]
        public void Export_GenModeResult_CreatesJsonFile()
        {
            // ARRANGE
            string outputDir = CreateOutputDir();
            Directory.CreateDirectory(outputDir);
            string excelPath = Path.Combine(outputDir, "generated_tests_test.xlsx");
            string expectedJsonPath = excelPath.Replace(".xlsx", ".json");

            var result = CreateFakeGenModeResult();

            // ACT
            string jsonPath = JsonExporter.Export(
                result, CreatePromptConfig(), TimeSpan.FromSeconds(10), excelPath);

            // ASSERT
            jsonPath.Should().Be(expectedJsonPath);
            File.Exists(jsonPath).Should().BeTrue("JSON file should be created");

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }

        // ============================================================
        // JSON STRUCTURE TESTS
        // ============================================================

        [Fact]
        public void Export_GenModeResult_ContainsMetadataSection()
        {
            // ARRANGE
            string outputDir = CreateOutputDir();
            Directory.CreateDirectory(outputDir);
            string excelPath = Path.Combine(outputDir, "generated_tests_test.xlsx");

            var result = CreateFakeGenModeResult();

            // ACT
            string jsonPath = JsonExporter.Export(
                result, CreatePromptConfig(), TimeSpan.FromSeconds(10), excelPath);

            string json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            // ASSERT
            doc.RootElement.TryGetProperty("metadata", out var metadata)
                .Should().BeTrue("JSON must contain metadata section");
            metadata.TryGetProperty("totalPasses", out _)
                .Should().BeTrue("metadata must contain totalPasses");
            metadata.TryGetProperty("totalTokens", out _)
                .Should().BeTrue("metadata must contain totalTokens");
            metadata.TryGetProperty("requirementsSource", out _)
                .Should().BeTrue("metadata must contain requirementsSource");

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }

        [Fact]
        public void Export_GenModeResult_ContainsSummarySection()
        {
            // ARRANGE
            string outputDir = CreateOutputDir();
            Directory.CreateDirectory(outputDir);
            string excelPath = Path.Combine(outputDir, "generated_tests_test.xlsx");

            var result = CreateFakeGenModeResult(3);

            // ACT
            string jsonPath = JsonExporter.Export(
                result, CreatePromptConfig(), TimeSpan.FromSeconds(10), excelPath);

            string json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            // ASSERT
            doc.RootElement.TryGetProperty("summary", out var summary)
                .Should().BeTrue("JSON must contain summary section");
            summary.GetProperty("testsGenerated").GetInt32()
                .Should().Be(3, "summary.testsGenerated should match test case count");

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }

        [Fact]
        public void Export_GenModeResult_ContainsTestCasesArray()
        {
            // ARRANGE
            string outputDir = CreateOutputDir();
            Directory.CreateDirectory(outputDir);
            string excelPath = Path.Combine(outputDir, "generated_tests_test.xlsx");

            var result = CreateFakeGenModeResult(5);

            // ACT
            string jsonPath = JsonExporter.Export(
                result, CreatePromptConfig(), TimeSpan.FromSeconds(10), excelPath);

            string json = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(json);

            // ASSERT
            doc.RootElement.TryGetProperty("testCases", out var testCases)
                .Should().BeTrue("JSON must contain testCases array");
            testCases.GetArrayLength()
                .Should().Be(5, "testCases array should contain all generated tests");
            testCases[0].TryGetProperty("testId", out _)
                .Should().BeTrue("each test case must have testId");
            testCases[0].TryGetProperty("qaScore", out _)
                .Should().BeTrue("each test case must have qaScore");

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }

        [Fact]
        public void Export_EmptyTestCases_ProducesValidJson()
        {
            // ARRANGE
            string outputDir = CreateOutputDir();
            Directory.CreateDirectory(outputDir);
            string excelPath = Path.Combine(outputDir, "generated_tests_test.xlsx");

            var result = new GenModeResult
            {
                TestCases = new List<GeneratedTestCase>(),
                TotalPasses = 1,
                TotalTokens = 100,
                RequirementsSource = "provided",
                GeneratedAt = DateTime.UtcNow
            };

            // ACT
            Action act = () => JsonExporter.Export(
                result, CreatePromptConfig(), TimeSpan.FromSeconds(1), excelPath);

            // ASSERT
            act.Should().NotThrow("empty test cases should produce valid JSON not throw");

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
        }
    }
}
