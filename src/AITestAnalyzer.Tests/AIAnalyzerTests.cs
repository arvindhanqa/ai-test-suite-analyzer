using System.Text;
using AITestAnalyzer.Models;
using AITestAnalyzer.Services;
using FluentAssertions;
using Moq;
using Xunit;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AITestAnalyzer.Tests
{
    public class AIAnalyzerTests
    {
        // ============================================================
        // MOCK-BASED TESTS — interface contract tests
        // ============================================================

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

        // ============================================================
        // REAL PARSING TESTS — test actual ParseGeneratedTestCases logic
        // ============================================================

        private RequirementExtractor CreateRequirementExtractor() =>
    new RequirementExtractor(
        new AITestAnalyzer.Models.Configuration { ApiKey = "sk-test-key-not-used" },
        new AITestAnalyzer.Models.PromptConfig
        {
            Model = "gpt-4o-mini",
            MaxTokens = 250,
            Temperature = 0.2
        });

        private AIAnalyzer CreateAnalyzer() => new AIAnalyzer(
            new AITestAnalyzer.Models.Configuration { ApiKey = "sk-test-key-not-used" },
            new AITestAnalyzer.Models.PromptConfig
            {
                Model = "gpt-4o-mini",
                GenModel = "gpt-4.1-mini",
                MaxTokens = 250,
                Temperature = 0.2
            });

        [Fact]
        public void Parse_ValidPipeResponse_Returns5TestCases()
        {
            // ARRANGE
            var analyzer = CreateAnalyzer();
            var response = @"TC-GEN-001|User Registration|Register with valid email|High|Step 1. Navigate\nStep 2. Submit|Account created
TC-GEN-002|User Registration|Register with duplicate email|High|Step 1. Navigate\nStep 2. Submit|Error shown
TC-GEN-003|User Registration|Register with invalid password|High|Step 1. Navigate\nStep 2. Submit|Error shown
TC-GEN-004|User Registration|Register with empty email|High|Step 1. Navigate\nStep 2. Submit|Error shown
TC-GEN-005|User Registration|Register with short password|High|Step 1. Navigate\nStep 2. Submit|Error shown";

            // ACT
            var result = analyzer.ParseGeneratedTestCases(response);

            // ASSERT
            result.Should().HaveCount(5);
            result[0].TestId.Should().Be("TC-GEN-001");
            result[0].Feature.Should().Be("User Registration");
            result[0].Priority.Should().Be("High");
            result[0].Steps.Should().Contain("\n");
        }

        [Fact]
        public void Parse_MissingField_SkipsRow()
        {
            // ARRANGE
            var analyzer = CreateAnalyzer();
            var response = @"TC-GEN-001|User Registration|Valid scenario|High|Step 1. Do X|Expected result
TC-GEN-002|User Registration|Missing fields only two pipes|High
TC-GEN-003|User Registration|Another valid row|High|Step 1. Do X|Expected result";

            // ACT
            var result = analyzer.ParseGeneratedTestCases(response);

            // ASSERT
            result.Should().HaveCount(2);
            result.Should().NotContain(t => t.TestId == "TC-GEN-002");
        }

        [Fact]
        public void Parse_EmptyResponse_ReturnsEmptyList()
        {
            // ARRANGE
            var analyzer = CreateAnalyzer();

            // ACT
            var result = analyzer.ParseGeneratedTestCases(string.Empty);

            // ASSERT
            result.Should().BeEmpty();
        }

        [Fact]
        public void Parse_MalformedRow_DoesNotThrow()
        {
            // ARRANGE
            var analyzer = CreateAnalyzer();
            var response = @"this is not pipe delimited at all
only|two|pipes
TC-GEN-001|User Registration|Valid row|High|Step 1. Do X|Expected result";

            // ACT
            Action act = () => analyzer.ParseGeneratedTestCases(response);

            // ASSERT
            act.Should().NotThrow();
            var result = analyzer.ParseGeneratedTestCases(response);
            result.Should().HaveCount(1);
            result[0].TestId.Should().Be("TC-GEN-001");
        }

        // ============================================================
        // REAL PARSING TESTS — test actual ParseCritiqueResults logic
        // ============================================================

        [Fact]
        public void Parse_ValidCritique_ReturnsCorrectActions()
        {
            // ARRANGE
            var analyzer = CreateAnalyzer();
            var response = @"TC-GEN-001|KEEP|No issues
TC-GEN-002|REVISE|Missing precondition — add login step
TC-GEN-003|DROP|Duplicate of TC-GEN-001";

            // ACT
            var result = analyzer.ParseCritiqueResults(response);

            // ASSERT
            result.Should().HaveCount(3);
            result[0].TestId.Should().Be("TC-GEN-001");
            result[0].Action.Should().Be("KEEP");
            result[0].Reason.Should().Be("No issues");
            result[1].Action.Should().Be("REVISE");
            result[1].Reason.Should().Be("Missing precondition — add login step");
            result[2].Action.Should().Be("DROP");
            result[2].TestId.Should().Be("TC-GEN-003");
        }

        [Fact]
        public void Parse_UnknownAction_DefaultsToKeep()
        {
            // ARRANGE
            var analyzer = CreateAnalyzer();
            var response = @"TC-GEN-001|KEEP|No issues
TC-GEN-002|UNKNOWN|Some reason
TC-GEN-003|MAYBE|Not sure about this one";

            // ACT
            var result = analyzer.ParseCritiqueResults(response);

            // ASSERT
            result.Should().HaveCount(3);
            result[1].Action.Should().Be("KEEP");
            result[1].Reason.Should().Be("Some reason");
            result[2].Action.Should().Be("KEEP");
        }

        [Fact]
        public void Parse_EmptyCritique_ReturnsEmptyList()
        {
            // ARRANGE
            var analyzer = CreateAnalyzer();

            // ACT
            var result = analyzer.ParseCritiqueResults(string.Empty);

            // ASSERT
            result.Should().BeEmpty();
        }

        // ============================================================
        // REAL PARSING TESTS — test actual ParsePipeDelimitedResponse logic
        // ============================================================

        [Fact]
        public void ParsePipeDelimited_ValidResponse_ReturnsCorrectCount()
        {
            // ARRANGE
            var extractor = CreateRequirementExtractor();
            var response = @"FR-001|user.auth.login|users login with email and password session expires 30min|1
FR-002|user.auth.register|new users create account with email verification required|1
BR-001|user.auth.password|password must be 8-20 chars with uppercase lowercase number|1";

            // ACT
            var result = extractor.ParsePipeDelimitedResponse(response);

            // ASSERT
            result.Should().HaveCount(3);
            result[0].Id.Should().Be("FR-001");
            result[0].Key.Should().Be("user.auth.login");
        }

        [Fact]
        public void ParsePipeDelimited_EmptyResponse_ReturnsEmptyList()
        {
            // ARRANGE
            var extractor = CreateRequirementExtractor();

            // ACT
            var result = extractor.ParsePipeDelimitedResponse(string.Empty);

            // ASSERT
            result.Should().BeEmpty();
        }

        [Fact]
        public void ParsePipeDelimited_ResponseWithMarkdown_StripsMarkdownAndParses()
        {
            // ARRANGE
            var extractor = CreateRequirementExtractor();
            var response = @"```
FR-001|user.auth.login|users login with email and password|1
FR-002|user.auth.register|new users create account with email verification|1
```";

            // ACT
            var result = extractor.ParsePipeDelimitedResponse(response);

            // ASSERT
            result.Should().HaveCount(2);
            result[0].Id.Should().Be("FR-001");
        }

        [Fact]
        public void ParsePipeDelimited_MalformedRows_SkipsMalformedKeepsValid()
        {
            // ARRANGE
            var extractor = CreateRequirementExtractor();
            var response = @"FR-001|user.auth.login|users login with email and password|1
this line has no pipes at all
FR-002|user.auth.register|new users create account with email verification|1";

            // ACT
            var result = extractor.ParsePipeDelimitedResponse(response);

            // ASSERT
            result.Should().HaveCount(2);
            result.Should().NotContain(r => r.Description == "this line has no pipes at all");
        }

        [Fact]
        public void ParsePipeDelimited_DoesNotThrow_OnAnyInput()
        {
            // ARRANGE
            var extractor = CreateRequirementExtractor();
            var response = @"not valid at all !!!
|||||||
   
FR-001|user.auth|valid line|1";

            // ACT
            Action act = () => extractor.ParsePipeDelimitedResponse(response);

            // ASSERT
            act.Should().NotThrow();
        }
    }
}
