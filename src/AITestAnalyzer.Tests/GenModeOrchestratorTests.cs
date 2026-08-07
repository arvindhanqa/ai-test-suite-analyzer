using AITestAnalyzer.Models;
using AITestAnalyzer.Services;
using AITestAnalyzer.Infrastructure;
using FluentAssertions;
using Moq;
using Xunit;

namespace AITestAnalyzer.Tests
{
    public class GenModeOrchestratorTests
    {
        private PromptConfig CreatePromptConfig() => new PromptConfig
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

        private string CreateCacheDir() =>
            Path.Combine(Directory.GetCurrentDirectory(),
                $"TestCache_Orchestrator_{Guid.NewGuid():N}");

        private List<GeneratedTestCase> CreateFakeTestCases(int count) =>
            Enumerable.Range(1, count).Select(i => new GeneratedTestCase
            {
                TestId = $"TC-GEN-{i:D3}",
                Feature = "Login",
                Scenario = $"Scenario {i}",
                Priority = "High",
                Steps = "Step 1. Do X",
                ExpectedResult = "Expected result",
                PassNumber = 1
            }).ToList();

        // ============================================================
        // GUARD TESTS
        // ============================================================

        [Fact]
        public async Task RunAsync_NullRequirements_ThrowsArgumentException()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var mockAnalyzer = new Mock<IAIAnalyzer>();
            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, CreatePromptConfig());

            // ACT + ASSERT
            await FluentActions
                .Invoking(() => orchestrator.RunAsync(null!, 5, 2))
                .Should().ThrowAsync<ArgumentException>()
                .WithMessage("*GEN Mode requires a requirements document*");

            mockAnalyzer.Verify(
                a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()),
                Times.Never());

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        [Fact]
        public async Task RunAsync_EmptyRequirements_ThrowsArgumentException()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var mockAnalyzer = new Mock<IAIAnalyzer>();
            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, CreatePromptConfig());

            // ACT + ASSERT
            await FluentActions
                .Invoking(() => orchestrator.RunAsync("   ", 5, 2))
                .Should().ThrowAsync<ArgumentException>();

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        [Fact]
        public async Task RunAsync_ZeroParsedTestCases_ThrowsInvalidOperationException()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var mockAnalyzer = new Mock<IAIAnalyzer>();

            mockAnalyzer
                .Setup(a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((new List<GeneratedTestCase>(), 500));

            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, CreatePromptConfig());

            // ACT + ASSERT
            await FluentActions
                .Invoking(() => orchestrator.RunAsync("# Requirements\n- FR-001: Login", 5, 1))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*zero test cases*");

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        // ============================================================
        // EARLY EXIT TESTS
        // ============================================================

        [Fact]
        public async Task RunAsync_AllCritiquesKeep_StopsAfterPass1()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var fakeTestCases = CreateFakeTestCases(3);
            var allKeepCritiques = fakeTestCases.Select(tc =>
                new CritiqueResult { TestId = tc.TestId, Action = "KEEP", Reason = "No issues" }
            ).ToList();

            var mockAnalyzer = new Mock<IAIAnalyzer>();
            mockAnalyzer
                .Setup(a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((fakeTestCases, 500));
            mockAnalyzer
                .Setup(a => a.CritiqueTestCasesAsync(It.IsAny<List<GeneratedTestCase>>(), It.IsAny<string>()))
                .ReturnsAsync((allKeepCritiques, 300));
            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()))
                .ReturnsAsync(("GOOD", 150));

            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, CreatePromptConfig())
            {
                MaxPasses = 3
            };

            // ACT
            var result = await orchestrator.RunAsync("# Requirements\n- FR-001: Login", 3, 3);

            // ASSERT
            result.TotalPasses.Should().Be(1,
                "because all critiques are KEEP — should exit after pass 1");

            // RefineTestCasesAsync should never be called
            mockAnalyzer.Verify(
                a => a.RefineTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(),
                    It.IsAny<List<CritiqueResult>>(),
                    It.IsAny<string>()),
                Times.Never(),
                "refinement should be skipped when all critiques are KEEP");

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        // ============================================================
        // PASS LIMIT TESTS
        // ============================================================

        [Fact]
        public async Task RunAsync_MaxPassesReached_StopsAtLimit()
        {
            // ARRANGE
            string cacheDir = CreateCacheDir();
            var fakeTestCases = CreateFakeTestCases(3);
            var reviseCritiques = fakeTestCases.Select(tc =>
                new CritiqueResult { TestId = tc.TestId, Action = "REVISE", Reason = "Needs improvement" }
            ).ToList();

            var mockAnalyzer = new Mock<IAIAnalyzer>();
            mockAnalyzer
                .Setup(a => a.GenerateTestCasesAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync((fakeTestCases, 500));
            mockAnalyzer
                .Setup(a => a.CritiqueTestCasesAsync(It.IsAny<List<GeneratedTestCase>>(), It.IsAny<string>()))
                .ReturnsAsync((reviseCritiques, 300));
            mockAnalyzer
                .Setup(a => a.RefineTestCasesAsync(
                    It.IsAny<List<GeneratedTestCase>>(),
                    It.IsAny<List<CritiqueResult>>(),
                    It.IsAny<string>()))
                .ReturnsAsync((fakeTestCases, 400));
            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(It.IsAny<TestCase>()))
                .ReturnsAsync(("GOOD", 150));

            var cache = new TestCaseCache(cacheDir);
            var orchestrator = new GenModeOrchestrator(mockAnalyzer.Object, cache, CreatePromptConfig())
            {
                MaxPasses = 2
            };

            // ACT
            var result = await orchestrator.RunAsync("# Requirements\n- FR-001: Login", 3, 2);

            // ASSERT
            result.TotalPasses.Should().Be(2,
                "because MaxPasses is 2 — should stop at pass limit");

            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }

        // ============================================================
        // DEFAULT VALUES TESTS
        // ============================================================

        [Fact]
        public void GenModeOrchestrator_DefaultValues_AreCorrect()
        {
            // ARRANGE
            var mockAnalyzer = new Mock<IAIAnalyzer>();
            var mockCache = new Mock<ITestCaseCache>();
            var orchestrator = new GenModeOrchestrator(
                mockAnalyzer.Object, mockCache.Object, CreatePromptConfig());

            // ASSERT
            orchestrator.MaxPasses.Should().Be(3,
                "default MaxPasses should match GEN_MAX_PASSES constant");
            orchestrator.TargetTestCount.Should().Be(10,
                "default TargetTestCount should match GEN_DEFAULT_TEST_COUNT constant");
        }
    }
}
