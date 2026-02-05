using Xunit;
using FluentAssertions;
using AITestAnalyzer;
using OfficeOpenXml;

namespace AITestAnalyzer.Tests
{
    public class ConfigurationValidatorTests
    {
        // Static constructor runs ONCE before any tests
        static ConfigurationValidatorTests()
        {
            ExcelPackage.License.SetNonCommercialPersonal("Aravindhan Rajasekaran");
        }

        // Helper method to create Configuration objects for testing
        private Configuration CreateConfiguration(string apiKey)
        {
            return new Configuration
            {
                ApiKey = apiKey
            };
        }

        // Helper method to create PromptConfig for testing
        private PromptConfig CreatePromptConfig()
        {
            return new PromptConfig
            {
                Model = "gpt-4o-mini",
                MaxTokens = 500,
                Temperature = 0.2,
                SystemMessage = "Test",
                UserTemplate = "Test"
            };
        }

        [Fact]
        public void ValidateApiKey_WithEmptyKey_ReturnsFailure()
        {
            // ARRANGE
            var config = CreateConfiguration("");  // Empty key
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);

            // ACT
            ValidationResult result = validator.ValidateApiKey();

            // ASSERT
            result.IsValid.Should().BeFalse("because empty API key is invalid");
            result.ErrorMessage.Should().Contain("API key is missing");
        }

        [Fact]
        public void ValidateApiKey_WithNullKey_ReturnsFailure()
        {
            // ARRANGE
            var config = CreateConfiguration(null!);  // Null key
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);

            // ACT
            ValidationResult result = validator.ValidateApiKey();

            // ASSERT
            result.IsValid.Should().BeFalse("because null API key is invalid");
            result.ErrorMessage.Should().Contain("API key is missing");
        }

        [Fact]
        public void ValidateApiKey_WithWhitespaceKey_ReturnsFailure()
        {
            // ARRANGE
            var config = CreateConfiguration("   ");  // Whitespace only
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);

            // ACT
            ValidationResult result = validator.ValidateApiKey();

            // ASSERT
            result.IsValid.Should().BeFalse("because whitespace-only API key is invalid");
            result.ErrorMessage.Should().Contain("API key is missing");
        }

        [Fact]
        public void ValidateApiKey_WithPlaceholderKey_ReturnsFailure()
        {
            // ARRANGE
            var config = CreateConfiguration("YOUR-ACTUAL-API-KEY-HERE");
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);

            // ACT
            ValidationResult result = validator.ValidateApiKey();

            // ASSERT
            result.IsValid.Should().BeFalse("because placeholder key is not configured");
            result.ErrorMessage.Should().Contain("not configured");
        }

        [Fact]
        public void ValidateApiKey_WithoutSkPrefix_ReturnsFailure()
        {
            // ARRANGE
            var config = CreateConfiguration("invalid-key-without-sk-prefix-1234567890");
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);

            // ACT
            ValidationResult result = validator.ValidateApiKey();

            // ASSERT
            result.IsValid.Should().BeFalse("because OpenAI keys must start with 'sk-'");
            result.ErrorMessage.Should().Contain("should start with 'sk-'");
        }

        [Fact]
        public void ValidateApiKey_WithShortKey_ReturnsFailure()
        {
            // ARRANGE
            var config = CreateConfiguration("sk-short");  // Too short (< 40 chars)
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);

            // ACT
            ValidationResult result = validator.ValidateApiKey();

            // ASSERT
            result.IsValid.Should().BeFalse("because key is shorter than 40 characters");
            result.ErrorMessage.Should().Contain("too short");
        }

        [Fact]
        public void ValidateApiKey_WithValidKey_ReturnsSuccess()
        {
            // ARRANGE
            // Create a realistic 48-character key starting with "sk-"
            var config = CreateConfiguration("sk-1234567890abcdefghijklmnopqrstuvwxyz123456");
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);

            // ACT
            ValidationResult result = validator.ValidateApiKey();

            // ASSERT
            result.IsValid.Should().BeTrue("because key meets all validation criteria");
            result.DetailedInfo.Should().Contain("valid");
        }

        [Theory]
        [InlineData(-1)]   // Negative index
        [InlineData(-100)] // Large negative
        public void ValidateWorksheetIndex_WithNegativeIndex_ReturnsFailure(int worksheetIndex)
        {
            // ARRANGE
            var config = CreateConfiguration("sk-1234567890abcdefghijklmnopqrstuvwxyz123456");
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);
            string testExcelPath = @"C:\Projects\ai-test-analyzer\ai-test-suite-analyzer\data\test_cases_shopease.xlsx";

            // ACT
            ValidationResult result = validator.ValidateWorksheetIndex(testExcelPath, worksheetIndex);

            // ASSERT
            result.IsValid.Should().BeFalse("because worksheet index cannot be negative");
            result.ErrorMessage.Should().Contain("cannot be negative");
        }

        [Fact]
        public void ValidateWorksheetIndex_WithValidIndex_ReturnsSuccess()
        {
            // ARRANGE
            var config = CreateConfiguration("sk-1234567890abcdefghijklmnopqrstuvwxyz123456");
            var promptConfig = CreatePromptConfig();
            var validator = new ConfigurationValidator(config, promptConfig);
            string testExcelPath = @"C:\Projects\ai-test-analyzer\ai-test-suite-analyzer\data\test_cases_shopease.xlsx";

            // ACT
            ValidationResult result = validator.ValidateWorksheetIndex(testExcelPath, 0);  // Sheet 1 (index 0)

            // ASSERT
            result.IsValid.Should().BeTrue("because index 0 should exist in the test file");
            result.DetailedInfo.Should().Contain("Sheet");
        }
    }
}
