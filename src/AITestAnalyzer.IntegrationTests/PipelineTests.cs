using Xunit;
using FluentAssertions;
using Moq;
using OfficeOpenXml;
using System.Text.Json;
using AITestAnalyzer.Models;
using AITestAnalyzer.Services;
using AITestAnalyzer.Infrastructure;
using AITestAnalyzer.Config;



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

        [Fact]
        public void CacheHit_SecondRun_MakesZeroApiCalls()
        {
            // ARRANGE
            string cacheDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestCache");

            // Clean up any existing test cache
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);

            var cache = new TestCaseCache(cacheDir);

            var testCase = new TestCase
            {
                TestId = "TC-001",
                Feature = "Login",
                Scenario = "Valid credentials",
                Steps = "1. Enter username\n2. Enter password\n3. Click login",
                ExpectedResult = "User is logged in successfully"
            };

            // ACT — First run: add to cache
            string hash = cache.GenerateHash(testCase);
            cache.AddToCache(testCase.TestId, hash, "GOOD - Clear and complete", "", 150);

            // ACT — Second run: check cache
            bool cacheHit = cache.TryGetCached(hash, out CachedResult? cachedResult, 30);

            // ASSERT
            cacheHit.Should().BeTrue("because the result was cached on first run");
            cachedResult.Should().NotBeNull();
            cachedResult!.Quality.Should().Be("GOOD - Clear and complete");
            cachedResult.Tokens.Should().Be(150);

            // Verify zero API calls needed on second run
            int apiCallsNeeded = cacheHit ? 0 : 1;
            apiCallsNeeded.Should().Be(0, "because cache hit means no API call required");

            // Cleanup
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }

        [Fact]
        public void JsonExport_ProducesValidJsonFile_WithCorrectStructure()
        {
            // ARRANGE
            string outputDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestOutput");

            Directory.CreateDirectory(outputDir);

            string outputPath = Path.Combine(outputDir,
                $"integration_test_json_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

            var promptConfig = new PromptConfig
            {
                Model = "gpt-4o-mini",
                MaxTokens = 150,
                Temperature = 0.2,
                CostPerToken = 0.00000015,
                SystemMessage = "Test",
                UserTemplate = "Test"
            };

            var results = new List<(string TestId, string Result, int Tokens, string Coverage)>
    {
        ("TC-001", "GOOD - Clear steps", 150, ""),
        ("TC-002", "GOOD - Well defined", 145, ""),
        ("TC-003", "INCOMPLETE - Missing expected result", 160, ""),
        ("TC-004", "GOOD - Complete test", 148, ""),
        ("TC-005", "GOOD - All fields present", 152, "")
    };

            // ACT
            string jsonPath = JsonExporter.Export(
                results,
                AnalysisMode.QA,
                cacheHits: 2,
                apiCalls: 3,
                startTime: DateTime.Now.AddSeconds(-10),
                endTime: DateTime.Now,
                promptConfig: promptConfig,
                outputPath: outputPath);

            // ASSERT — file exists
            File.Exists(jsonPath).Should().BeTrue("JSON file should be created");
            jsonPath.Should().EndWith(".json", "output path should have .json extension");

            // ASSERT — JSON structure is valid
            string jsonContent = File.ReadAllText(jsonPath);
            jsonContent.Should().NotBeNullOrEmpty("JSON file should not be empty");

            using var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Verify metadata section
            root.TryGetProperty("metadata", out var metadata).Should().BeTrue(
                "JSON should contain metadata section");
            metadata.GetProperty("analysisMode").GetString().Should().Be("QA");
            metadata.GetProperty("totalTests").GetInt32().Should().Be(5);
            metadata.GetProperty("cacheHits").GetInt32().Should().Be(2);
            metadata.GetProperty("apiCalls").GetInt32().Should().Be(3);

            // Verify summary section
            root.TryGetProperty("summary", out var summary).Should().BeTrue(
                "JSON should contain summary section");
            summary.GetProperty("goodTests").GetInt32().Should().Be(4);
            summary.GetProperty("testsWithIssues").GetInt32().Should().Be(1);
            summary.GetProperty("qualityScorePct").GetDouble().Should().Be(80.0);

            // Verify results array
            root.TryGetProperty("results", out var resultsArray).Should().BeTrue(
                "JSON should contain results array");
            resultsArray.GetArrayLength().Should().Be(5,
                "results array should have one entry per test");

            // Verify first result structure
            var firstResult = resultsArray[0];
            firstResult.TryGetProperty("testId", out _).Should().BeTrue();
            firstResult.TryGetProperty("analysis", out _).Should().BeTrue();
            firstResult.TryGetProperty("coverage", out _).Should().BeTrue();
            firstResult.TryGetProperty("tokens", out _).Should().BeTrue();

            // Cleanup
            if (File.Exists(jsonPath))
                File.Delete(jsonPath);
        }

        [Fact]
        public async Task GenerateTestCasesAsync_WhenCalled_Returns5TestCases()
        {
            // ARRANGE
            var mockAnalyzer = new Mock<IAIAnalyzer>();

            var expectedTestCases = Enumerable.Range(1, 5)
                .Select(i => new GeneratedTestCase
                {
                    TestId = $"TC-GEN-00{i}",
                    Feature = "User Registration",
                    Scenario = $"Scenario {i}",
                    Priority = "High",
                    Steps = "Step 1. Do X\nStep 2. Do Y",
                    ExpectedResult = "Expected result"
                }).ToList();

            mockAnalyzer
                .Setup(a => a.GenerateTestCasesAsync(
                    It.IsAny<string>(),
                    It.IsAny<int>()))
                .ReturnsAsync((expectedTestCases, 500));

            // ACT
            var (testCases, tokens) = await mockAnalyzer.Object
                .GenerateTestCasesAsync("some requirements", 5);

            // ASSERT
            testCases.Should().HaveCount(5);
            testCases.First().TestId.Should().Be("TC-GEN-001");
            tokens.Should().Be(500);
        }

        [Fact]
        public async Task CritiqueTestCasesAsync_WhenCalled_ReturnsCorrectActions()
        {
            // ARRANGE
            var mockAnalyzer = new Mock<IAIAnalyzer>();

            var testCases = new List<GeneratedTestCase>
            {
                new GeneratedTestCase { TestId = "TC-GEN-001", Feature = "Login" },
                new GeneratedTestCase { TestId = "TC-GEN-002", Feature = "Login" },
                new GeneratedTestCase { TestId = "TC-GEN-003", Feature = "Login" }
            };

            var expectedCritiques = new List<CritiqueResult>
            {
                new CritiqueResult { TestId = "TC-GEN-001", Action = "KEEP",   Reason = "No issues" },
                new CritiqueResult { TestId = "TC-GEN-002", Action = "REVISE", Reason = "Missing precondition" },
                new CritiqueResult { TestId = "TC-GEN-003", Action = "DROP",   Reason = "Duplicate of TC-GEN-001" }
            };

            mockAnalyzer
                .Setup(a => a.CritiqueTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(),
                    It.IsAny<string>()))
                .ReturnsAsync((expectedCritiques, 300));

            // ACT
            var (critiques, tokens) = await mockAnalyzer.Object
                .CritiqueTestCasesAsync(testCases, "some requirements");

            // ASSERT
            critiques.Should().HaveCount(3);
            critiques[0].Action.Should().Be("KEEP");
            critiques[1].Action.Should().Be("REVISE");
            critiques[2].Action.Should().Be("DROP");
            tokens.Should().Be(300);
        }

        [Fact]
        public async Task RefineTestCasesAsync_WhenCalled_RemovesDroppedTestCases()
        {
            // ARRANGE
            var mockAnalyzer = new Mock<IAIAnalyzer>();

            var testCases = new List<GeneratedTestCase>
            {
                new GeneratedTestCase { TestId = "TC-GEN-001", Feature = "Login" },
                new GeneratedTestCase { TestId = "TC-GEN-002", Feature = "Login" },
                new GeneratedTestCase { TestId = "TC-GEN-003", Feature = "Login" }
            };

            var critiques = new List<CritiqueResult>
            {
                new CritiqueResult { TestId = "TC-GEN-001", Action = "KEEP",   Reason = "No issues" },
                new CritiqueResult { TestId = "TC-GEN-002", Action = "REVISE", Reason = "Missing precondition" },
                new CritiqueResult { TestId = "TC-GEN-003", Action = "DROP",   Reason = "Duplicate of TC-GEN-001" }
            };

            // Refined output: TC-GEN-003 dropped, TC-GEN-002 revised
            var refinedTestCases = new List<GeneratedTestCase>
            {
                new GeneratedTestCase { TestId = "TC-GEN-001", Feature = "Login" },
                new GeneratedTestCase { TestId = "TC-GEN-002", Feature = "Login", PassNumber = 2 }
            };

            mockAnalyzer
                .Setup(a => a.RefineTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(),
                    It.IsAny<List<CritiqueResult>>(),
                    It.IsAny<string>()))
                .ReturnsAsync((refinedTestCases, 400));

            // ACT
            var (refined, tokens) = await mockAnalyzer.Object
                .RefineTestCasesAsync(testCases, critiques, "some requirements");

            // ASSERT
            refined.Should().HaveCount(2);
            refined.Should().NotContain(t => t.TestId == "TC-GEN-003");
            refined.Should().Contain(t => t.TestId == "TC-GEN-002" && t.PassNumber == 2);
            tokens.Should().Be(400);
        }

        [Fact]
        public async Task GenMode_FullPipeline_ProducesExcelAndJsonOutput()
        {
            // ARRANGE
            string outputDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestOutput");
            Directory.CreateDirectory(outputDir);

            string cacheDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestCache_Gen");
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);

            var promptConfig = new PromptConfig
            {
                Model = "gpt-4o-mini",
                GenModel = "gpt-4.1-mini",
                MaxTokens = 150,
                Temperature = 0.2,
                CostPerToken = 0.00000015,
                SystemMessage = "Test",
                UserTemplate = "Test",
                GenSystemMessage = "Test",
                GenUserTemplate = "Test"
            };

            // Deterministic test cases returned by mock
            var fakeTestCases = Enumerable.Range(1, 3).Select(i => new GeneratedTestCase
            {
                TestId = $"TC-GEN-00{i}",
                Feature = "User Registration",
                Scenario = $"Scenario {i}",
                Priority = "High",
                Steps = "Step 1. Do X\nStep 2. Do Y",
                ExpectedResult = "Expected result",
                PassNumber = 1,
                QAScore = "GOOD"
            }).ToList();

            var fakeCritiques = fakeTestCases.Select(tc =>
                new CritiqueResult { TestId = tc.TestId, Action = "KEEP", Reason = "No issues" }
            ).ToList();

            var mockAnalyzer = new Mock<IAIAnalyzer>();

            mockAnalyzer
                .Setup(a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((fakeTestCases, 500));

            mockAnalyzer
                .Setup(a => a.CritiqueTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(), It.IsAny<string>()))
                .ReturnsAsync((fakeCritiques, 300));

            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()))
                .ReturnsAsync(("GOOD - Test passes standards", 150));

            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, promptConfig);

            // ACT
            var startTime = DateTime.Now;
            var result = await orchestrator.RunAsync("# Requirements\n- FR-001: Login", 3, 1);
            var elapsed = DateTime.Now - startTime;

            // Write Excel + JSON output
            var genExcelWriter = new GenModeExcelWriter(promptConfig);
            string outputPath = genExcelWriter.WriteOutput(result, elapsed);
            string jsonPath = JsonExporter.Export(result, promptConfig, elapsed, outputPath);

            // ASSERT — result
            result.Should().NotBeNull();
            result.TestCases.Should().HaveCount(3);
            result.TotalPasses.Should().Be(1);
            result.TotalTokens.Should().BeGreaterThan(0);

            // ASSERT — Excel file created with correct sheets
            File.Exists(outputPath).Should().BeTrue("Excel output file should be created");

            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                var sheetNames = package.Workbook.Worksheets
                    .Select(ws => ws.Name).ToList();

                sheetNames.Should().Contain("Generated Tests",
                    "because generated tests sheet should exist");
                sheetNames.Should().Contain("Gen Statistics Dashboard",
                    "because gen statistics dashboard should exist");
            }

            // ASSERT — JSON file created with correct structure
            File.Exists(jsonPath).Should().BeTrue("JSON output file should be created");

            string jsonContent = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            root.TryGetProperty("metadata", out _).Should().BeTrue(
                "JSON should contain metadata section");
            root.TryGetProperty("summary", out var summary).Should().BeTrue(
                "JSON should contain summary section");
            root.TryGetProperty("testCases", out var testCasesArray).Should().BeTrue(
                "JSON should contain testCases array");

            summary.GetProperty("testsGenerated").GetInt32().Should().Be(3);
            testCasesArray.GetArrayLength().Should().Be(3);

            // Cleanup
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }

        [Fact]
        public async Task GenMode_CritiqueLoop_DropsAndRevisesTests()
        {
            // ARRANGE
            string cacheDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestCache_CritiqueLoop");
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);

            var promptConfig = new PromptConfig
            {
                Model = "gpt-4o-mini",
                GenModel = "gpt-4.1-mini",
                MaxTokens = 150,
                Temperature = 0.2,
                CostPerToken = 0.00000015,
                SystemMessage = "Test",
                UserTemplate = "Test",
                GenSystemMessage = "Test",
                GenUserTemplate = "Test"
            };

            // Initial generated test cases — 3 tests
            var initialTestCases = new List<GeneratedTestCase>
            {
                new GeneratedTestCase
                {
                    TestId = "TC-GEN-001", Feature = "Login",
                    Scenario = "Valid login", Priority = "High",
                    Steps = "Step 1. Enter credentials", ExpectedResult = "Logged in",
                    PassNumber = 1
                },
                new GeneratedTestCase
                {
                    TestId = "TC-GEN-002", Feature = "Login",
                    Scenario = "Invalid password", Priority = "High",
                    Steps = "Step 1. Enter wrong password", ExpectedResult = "Error shown",
                    PassNumber = 1
                },
                new GeneratedTestCase
                {
                    TestId = "TC-GEN-003", Feature = "Login",
                    Scenario = "Duplicate of TC-GEN-001", Priority = "High",
                    Steps = "Step 1. Enter credentials", ExpectedResult = "Logged in",
                    PassNumber = 1
                }
            };

            // First critique: TC-GEN-002 REVISE, TC-GEN-003 DROP, TC-GEN-001 KEEP
            var firstCritiques = new List<CritiqueResult>
            {
                new CritiqueResult { TestId = "TC-GEN-001", Action = "KEEP",   Reason = "No issues" },
                new CritiqueResult { TestId = "TC-GEN-002", Action = "REVISE", Reason = "Missing precondition — add login page navigation step" },
                new CritiqueResult { TestId = "TC-GEN-003", Action = "DROP",   Reason = "Duplicate of TC-GEN-001" }
            };

            // Refined output after Pass 2 — TC-GEN-003 dropped, TC-GEN-002 revised
            var refinedTestCases = new List<GeneratedTestCase>
            {
                new GeneratedTestCase
                {
                    TestId = "TC-GEN-001", Feature = "Login",
                    Scenario = "Valid login", Priority = "High",
                    Steps = "Step 1. Enter credentials", ExpectedResult = "Logged in",
                    PassNumber = 1
                },
                new GeneratedTestCase
                {
                    TestId = "TC-GEN-002", Feature = "Login",
                    Scenario = "Invalid password", Priority = "High",
                    Steps = "Step 1. Navigate to login page\nStep 2. Enter wrong password",
                    ExpectedResult = "Error message displayed",
                    PassNumber = 2  // revised — PassNumber incremented
                }
            };

            var mockAnalyzer = new Mock<IAIAnalyzer>();

            // Generate returns 3 initial test cases
            mockAnalyzer
                .Setup(a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((initialTestCases, 500));

            // First critique returns 1 KEEP + 1 REVISE + 1 DROP
            mockAnalyzer
                .Setup(a => a.CritiqueTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(), It.IsAny<string>()))
                .ReturnsAsync((firstCritiques, 300));

            // Refine returns 2 test cases (TC-GEN-003 dropped, TC-GEN-002 revised)
            mockAnalyzer
                .Setup(a => a.RefineTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(),
                    It.IsAny<List<CritiqueResult>>(),
                    It.IsAny<string>()))
                .ReturnsAsync((refinedTestCases, 400));

            // QA scoring
            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()))
                .ReturnsAsync(("GOOD", 150));

            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, promptConfig)
            {
                MaxPasses = 2,
                TargetTestCount = 3
            };

            // ACT
            var result = await orchestrator.RunAsync(
                "# Requirements\n- FR-001: Login",
                targetCount: 3,
                maxPasses: 2);

            // ASSERT — correct number of final test cases
            result.TestCases.Should().HaveCount(2,
                "because TC-GEN-003 was dropped by the critique");

            // ASSERT — dropped test not in final output
            result.TestCases.Should().NotContain(
                t => t.TestId == "TC-GEN-003",
                "because TC-GEN-003 was marked DROP and should be removed");

            // ASSERT — revised test appears with PassNumber = 2
            result.TestCases.Should().Contain(
                t => t.TestId == "TC-GEN-002" && t.PassNumber == 2,
                "because TC-GEN-002 was revised and PassNumber should be incremented");

            // ASSERT — kept test unchanged
            result.TestCases.Should().Contain(
                t => t.TestId == "TC-GEN-001" && t.PassNumber == 1,
                "because TC-GEN-001 was kept and PassNumber should remain 1");

            // ASSERT — passes used
            result.TotalPasses.Should().Be(2,
                "because refinement ran for 2 passes");

            // ASSERT — tokens accumulated across all passes
            result.TotalTokens.Should().BeGreaterThan(0);

            // Cleanup
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }

        [Fact]
        public async Task GenMode_CacheHit_MakesZeroAICalls()
        {
            // ARRANGE
            string cacheDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestCache_CacheHit");
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);

            var promptConfig = new PromptConfig
            {
                Model = "gpt-4o-mini",
                GenModel = "gpt-4.1-mini",
                MaxTokens = 150,
                Temperature = 0.2,
                CostPerToken = 0.00000015,
                SystemMessage = "Test",
                UserTemplate = "Test",
                GenSystemMessage = "Test",
                GenUserTemplate = "Test"
            };

            var fakeTestCases = new List<GeneratedTestCase>
            {
                new GeneratedTestCase
                {
                    TestId = "TC-GEN-001", Feature = "Login",
                    Scenario = "Valid login", Priority = "High",
                    Steps = "Step 1. Enter credentials",
                    ExpectedResult = "Logged in", PassNumber = 1,
                    QAScore = "GOOD"
                }
            };

            var fakeCritiques = new List<CritiqueResult>
            {
                new CritiqueResult
                {
                    TestId = "TC-GEN-001", Action = "KEEP", Reason = "No issues"
                }
            };

            var mockAnalyzer = new Mock<IAIAnalyzer>();

            mockAnalyzer
                .Setup(a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((fakeTestCases, 500));

            mockAnalyzer
                .Setup(a => a.CritiqueTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(), It.IsAny<string>()))
                .ReturnsAsync((fakeCritiques, 300));

            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()))
                .ReturnsAsync(("GOOD", 150));

            const string requirements = "# Requirements\n- FR-001: Login feature";
            const int targetCount = 1;
            const int maxPasses = 1;

            var cache = new TestCaseCache(cacheDir);

            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, promptConfig)
            {
                MaxPasses = maxPasses,
                TargetTestCount = targetCount
            };

            // ACT — First run: cache miss, AI calls made
            var firstResult = await orchestrator.RunAsync(requirements, targetCount, maxPasses);

            // Verify first run made AI calls
            mockAnalyzer.Verify(
                a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Once(),
                "first run should call GenerateTestCasesAsync exactly once");

            // ACT — Second run: same inputs, should hit cache
            // Reset invocation tracking by creating a new mock with same setups
            var mockAnalyzer2 = new Mock<IAIAnalyzer>();
            mockAnalyzer2
                .Setup(a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((fakeTestCases, 500));
            mockAnalyzer2
                .Setup(a => a.CritiqueTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(), It.IsAny<string>()))
                .ReturnsAsync((fakeCritiques, 300));
            mockAnalyzer2
                .Setup(a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()))
                .ReturnsAsync(("GOOD", 150));

            var orchestrator2 = new GenModeOrchestrator(mockAnalyzer2.Object, cache, promptConfig)
            {
                MaxPasses = maxPasses,
                TargetTestCount = targetCount
            };

            var secondResult = await orchestrator2.RunAsync(requirements, targetCount, maxPasses);

            // ASSERT — second run returned same result
            secondResult.Should().NotBeNull();
            secondResult.TestCases.Should().HaveCount(firstResult.TestCases.Count,
                "cached result should have same test case count as first run");
            secondResult.TestCases[0].TestId.Should().Be(
                firstResult.TestCases[0].TestId,
                "cached result should have same test IDs");

            // ASSERT — second run made zero AI calls
            mockAnalyzer2.Verify(
                a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never(),
                "second run should make zero GenerateTestCasesAsync calls — cache hit");

            mockAnalyzer2.Verify(
                a => a.CritiqueTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(), It.IsAny<string>()),
                Times.Never(),
                "second run should make zero CritiqueTestCasesAsync calls — cache hit");

            mockAnalyzer2.Verify(
                a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()),
                Times.Never(),
                "second run should make zero AnalyzeTestQualityAsync calls — cache hit");

            // Cleanup
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }

        [Fact]
        public async Task GenMode_MissingRequirements_ThrowsArgumentException()
        {
            // ARRANGE
            string cacheDir = Path.Combine(
                Directory.GetCurrentDirectory(), "TestCache_MissingReqs");
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);

            var promptConfig = new PromptConfig
            {
                Model = "gpt-4o-mini",
                GenModel = "gpt-4.1-mini",
                MaxTokens = 150,
                Temperature = 0.2,
                CostPerToken = 0.00000015,
                SystemMessage = "Test",
                UserTemplate = "Test",
                GenSystemMessage = "Test",
                GenUserTemplate = "Test"
            };

            var mockAnalyzer = new Mock<IAIAnalyzer>();
            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, promptConfig);

            // ACT + ASSERT — null requirements
            await FluentActions
                .Invoking(() => orchestrator.RunAsync(null!, 5, 2))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*GEN Mode requires a requirements document*");

            // ACT + ASSERT — empty requirements
            await FluentActions
                .Invoking(() => orchestrator.RunAsync("", 5, 2))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*GEN Mode requires a requirements document*");

            // ACT + ASSERT — whitespace only
            await FluentActions
                .Invoking(() => orchestrator.RunAsync("   ", 5, 2))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*GEN Mode requires a requirements document*");

            // ASSERT — no AI calls were made
            mockAnalyzer.Verify(
                a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never(),
                "no AI calls should be made when requirements are missing");

            // Cleanup
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);
        }
    }
}
