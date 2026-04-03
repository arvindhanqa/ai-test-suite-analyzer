using Xunit;
using FluentAssertions;
using Moq;
using AITestAnalyzer;
using OfficeOpenXml;

namespace AITestAnalyzer.IntegrationTests
{
    public class PipelineTests
    {
        static PipelineTests()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Aravindhan Rajasekaran");
        }

        [Fact]
        public void TestData_SampleExcel_Exists()
        {
            // ARRANGE
            string testDataPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "TestData",
                "test_cases_shopease.xlsx");

            // ASSERT
            File.Exists(testDataPath).Should().BeTrue(
                "because sample Excel file must exist in TestData folder");
        }

        [Fact]
        public void FullAnalysisPipeline_QAMode_ProducesValidExcelOutput()
        {
            // ARRANGE
            string testDataPath = Path.Combine(
                Directory.GetCurrentDirectory(), "TestData", "test_cases_shopease.xlsx");

            string outputDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestOutput");

            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir,
                $"integration_test_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            File.Copy(testDataPath, outputPath, overwrite: true);

            var promptConfig = new PromptConfig
            {
                Model = "gpt-4o-mini",
                MaxTokens = 150,
                Temperature = 0.2,
                CostPerToken = 0.00000015,
                SystemMessage = "Test",
                UserTemplate = "Test"
            };

            // Use real ExcelReader and ExcelWriter
            var excelReader = new ExcelReader(testDataPath, 0);
            var excelWriter = new ExcelWriter(outputPath, promptConfig, 0);

            // Mock IAIAnalyzer — no real API calls
            var mockAnalyzer = new Mock<IAIAnalyzer>();
            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()))
                .ReturnsAsync(("GOOD - Test passes quality standards", 150));

            // ACT
            excelWriter.RenameOriginalSheet();
            excelWriter.AddAnalysisColumnHeader(AnalysisMode.QA);

            var testCases = excelReader.ReadAllTestCases(5); // Only 5 tests
            int row = 2;
            foreach (var testCase in testCases)
            {
                excelWriter.WriteAnalysis(row, "GOOD - Test passes quality standards", "", AnalysisMode.QA);
                row++;
            }
            excelWriter.FlushAnalysis();

            var results = testCases.Select((tc, i) =>
                (tc.TestId, "GOOD - Test passes quality standards", 150, "")).ToList();

            excelWriter.CreateQualityIssuesSheet(results);
            excelWriter.CreateStatisticsDashboard(results, DateTime.Now.AddSeconds(-5), DateTime.Now);

            // ASSERT
            File.Exists(outputPath).Should().BeTrue("output Excel file should be created");

            using (var package = new OfficeOpenXml.ExcelPackage(new FileInfo(outputPath)))
            {
                var sheetNames = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();

                sheetNames.Should().Contain("AI Detailed Analysis",
                    "because main analysis sheet should exist");
                sheetNames.Should().Contain("Quality Issues Summary",
                    "because quality issues sheet should exist");
                sheetNames.Should().Contain("Statistics Dashboard",
                    "because statistics dashboard should exist");
            }
        }

        [Fact]
        public void FullAnalysisPipeline_BAMode_ProducesCoverageGapSheet()
        {
            // ARRANGE
            string testDataPath = Path.Combine(
                Directory.GetCurrentDirectory(), "TestData", "test_cases_shopease.xlsx");

            string outputDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestOutput");

            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir,
                $"integration_test_ba_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            File.Copy(testDataPath, outputPath, overwrite: true);

            var promptConfig = new PromptConfig
            {
                Model = "gpt-4o-mini",
                MaxTokens = 1000,
                Temperature = 0.2,
                CostPerToken = 0.00000015,
                SystemMessage = "Test",
                UserTemplate = "Test"
            };

            var excelReader = new ExcelReader(testDataPath, 0);
            var excelWriter = new ExcelWriter(outputPath, promptConfig, 0);

            // Mock IAIAnalyzer — no real API calls
            var mockAnalyzer = new Mock<IAIAnalyzer>();
            mockAnalyzer
                .Setup(a => a.AnalyzeCoverageAndFeedbackAsync(
                    It.IsAny<TestCase>(),
                    It.IsAny<List<ExtractedRequirement>>()))
                .ReturnsAsync(("", new List<string> { "FR-AUTH-001", "FR-AUTH-002" }, 800));

            // Minimal requirements list
            var requirements = new List<ExtractedRequirement>
    {
        new ExtractedRequirement { Id = "FR-AUTH-001", Description = "User login" },
        new ExtractedRequirement { Id = "FR-AUTH-002", Description = "User registration" },
        new ExtractedRequirement { Id = "FR-AUTH-003", Description = "Password reset" }
    };

            // ACT
            excelWriter.RenameOriginalSheet();
            excelWriter.AddAnalysisColumnHeader(AnalysisMode.BA);

            var testCases = excelReader.ReadAllTestCases(5);
            int row = 2;
            foreach (var testCase in testCases)
            {
                excelWriter.WriteAnalysis(row, "", "FR-AUTH-001, FR-AUTH-002", AnalysisMode.BA);
                row++;
            }
            excelWriter.FlushAnalysis();

            var results = testCases.Select((tc, i) =>
                (tc.TestId, "", 800, "FR-AUTH-001, FR-AUTH-002")).ToList();

            excelWriter.CreateCoverageGapSheet(results, requirements);
            excelWriter.CreateBAStatisticsDashboard(
                results, requirements,
                results.Sum(r => r.Item3),
                0,
                TimeSpan.FromSeconds(5));

            // ASSERT
            File.Exists(outputPath).Should().BeTrue("output Excel file should be created");

            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                var sheetNames = package.Workbook.Worksheets.Select(ws => ws.Name).ToList();

                sheetNames.Should().Contain("AI Detailed Analysis",
                    "because main analysis sheet should exist");
                sheetNames.Should().Contain("Coverage Gap Analysis",
                    "because coverage gap sheet should exist in BA mode");
                sheetNames.Should().Contain("BA Statistics Dashboard",
                    "because BA statistics dashboard should exist");
            }
        }
    }
}
