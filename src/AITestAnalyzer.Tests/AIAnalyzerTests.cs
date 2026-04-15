using System.Buffers.Text;
using AITestAnalyzer;
using AITestAnalyzer.Models;
using AITestAnalyzer.Services;
using FluentAssertions;
using Moq;
using Xunit;

namespace AITestAnalyzer.Tests
{
    public class AIAnalyzerTests
    {
        [Fact]
        public async Task AnalyzeTestQualityAsync_WhenCalled_ReturnsQualityAndTokens()
        {
            // ARRANGE
            var mockAnalyzer = new Mock<IAIAnalyzer>();

            var testCase = new TestCase
            {
                TestId = "TC-001",
                Feature = "Login",
                Scenario = "Valid credentials",
                Steps = "1. Enter username\n2. Enter password\n3. Click login",
                ExpectedResult = "User is logged in successfully"
            };

            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(testCase))
                .ReturnsAsync(("GOOD - Clear steps and expected result", 150));

            // ACT
            var (quality, tokens) = await mockAnalyzer.Object
                .AnalyzeTestQualityAsync(testCase);

            // ASSERT
            quality.Should().StartWith("GOOD");
            tokens.Should().Be(150);
        }

        [Fact]
        public async Task AnalyzeTestQualityAsync_WhenCalled_InvokedExactlyOnce()
        {
            // ARRANGE
            var mockAnalyzer = new Mock<IAIAnalyzer>();

            var testCase = new TestCase
            {
                TestId = "TC-001",
                Feature = "Login",
                Scenario = "Valid credentials",
                Steps = "1. Enter username",
                ExpectedResult = "User logged in"
            };

            mockAnalyzer
                .Setup(a => a.AnalyzeTestQualityAsync(testCase))
                .ReturnsAsync(("GOOD", 120));

            // ACT
            await mockAnalyzer.Object.AnalyzeTestQualityAsync(testCase);

            // ASSERT
            mockAnalyzer.Verify(
                a => a.AnalyzeTestQualityAsync(testCase),
                Times.Once(),
                "analyzer should be called exactly once per test case");
        }

        [Fact]
        public async Task AnalyzeCoverageAndFeedbackAsync_WhenCalled_ReturnsCoverageIds()
        {
            // ARRANGE
            var mockAnalyzer = new Mock<IAIAnalyzer>();

            var testCase = new TestCase
            {
                TestId = "TC-001",
                Feature = "Auth",
                Scenario = "User registration",
                Steps = "1. Fill form\n2. Submit",
                ExpectedResult = "Account created"
            };

            var requirements = new List<ExtractedRequirement>();

            mockAnalyzer
                .Setup(a => a.AnalyzeCoverageAndFeedbackAsync(testCase, requirements))
                .ReturnsAsync(("", new List<string> { "FR-AUTH-001", "FR-AUTH-002" }, 800));

            // ACT
            var (feedback, coverageIds, tokens) = await mockAnalyzer.Object
                .AnalyzeCoverageAndFeedbackAsync(testCase, requirements);

            // ASSERT
            coverageIds.Should().HaveCount(2);
            coverageIds.Should().Contain("FR-AUTH-001");
            tokens.Should().Be(800);
        }
    }
}
