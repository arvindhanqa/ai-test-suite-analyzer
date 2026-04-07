using System;
using System.IO;
using System.Threading.Tasks;
using AITestAnalyzer.Models;
using OfficeOpenXml;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AITestAnalyzer
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
        public string DetailedInfo { get; set; } = "";

        public static ValidationResult Success(string info = "")
        {
            return new ValidationResult { IsValid = true, DetailedInfo = info };
        }

        public static ValidationResult Failure(string errorMessage)
        {
            return new ValidationResult { IsValid = false, ErrorMessage = errorMessage };
        }
    }

    public class ConfigurationValidator
    {
        private readonly Configuration _config;
        private readonly PromptConfig _promptConfig;

        public ConfigurationValidator(Configuration config, PromptConfig promptConfig)
        {
            _config = config;
            _promptConfig = promptConfig;
        }

        // Master validation method - runs all checks
        // excelPath and worksheetIndex come from FileSelector, not from config
        /// <summary>
        /// Validates all configuration requirements before starting test analysis
        /// </summary>
        /// <param name="excelPath">Path to Excel file selected by user via FileSelector</param>
        /// <param name="worksheetIndex">Zero-based worksheet index to read from (0=Sheet1, 1=Sheet2, etc.)</param>
        /// <returns>
        /// Tuple containing:
        /// - IsValid: true if all validations pass, false if any validation fails
        /// - ErrorMessage: Descriptive error message if IsValid=false, "All validations passed" if IsValid=true
        /// </returns>
        /// <remarks>
        /// Performs three validation checks in order:
        /// 1. API key format validation (starts with "sk-", minimum length)
        /// 2. Worksheet index validation (exists in selected Excel file)
        /// 3. OpenAI API connection test (makes test API request to verify key works)
        /// 
        /// This is the first validation step in the application lifecycle, called before
        /// any test processing begins. Designed to fail fast with clear error messages.
        /// 
        /// NOTE: This method never throws exceptions. All validation failures are caught
        /// internally and returned as (false, errorMessage) tuples for graceful error handling.
        /// </remarks>
        public async Task<(bool IsValid, string ErrorMessage)> ValidateAllAsync(string excelPath, int worksheetIndex)
        {
            // 2. Validate Promptconfig file
            var promptconfigResult = ValidatePromptConfig();
            if (!promptconfigResult.IsValid)
            {
                return (false, promptconfigResult.ErrorMessage);
            }

            // 1. Validate API Key
            var apiKeyResult = ValidateApiKey();
            if (!apiKeyResult.IsValid)
            {
                return (false, apiKeyResult.ErrorMessage);
            }

            // 3. Validate Worksheet Index against the file FileSelector picked
            var worksheetResult = ValidateWorksheetIndex(excelPath, worksheetIndex);
            if (!worksheetResult.IsValid)
            {
                return (false, worksheetResult.ErrorMessage);
            }

            // 4. Validate OpenAI Connection (optional but recommended)
            var connectionResult = await ValidateOpenAIConnectionAsync();
            if (!connectionResult.IsValid)
            {
                return (false, connectionResult.ErrorMessage);
            }

            return (true, "All validations passed");
        }

        /// <summary>
        /// Validates OpenAI API key format and configuration
        /// </summary>
        /// <returns>
        /// ValidationResult with IsValid=true if key passes all checks, IsValid=false with error message if any check fails
        /// </returns>
        /// <remarks>
        /// VALIDATION CHECKS:
        /// 1. Key exists and not empty
        /// 2. Not placeholder value ("YOUR-ACTUAL-API-KEY-HERE")
        /// 3. Starts with "sk-" (OpenAI secret key prefix)
        /// 4. Minimum length 40 characters (typical OpenAI key length: 48-51)
        /// 
        /// Does NOT validate key authenticity (doesn't call API).
        /// Use ValidateOpenAIConnection() to test actual API access.
        /// </remarks>
        public ValidationResult ValidateApiKey()
        {
            if (string.IsNullOrWhiteSpace(_config.ApiKey))
            {
                return ValidationResult.Failure("API key is missing. Please add your OpenAI API key to appsettings.json");
            }

            if (_config.ApiKey == "YOUR-ACTUAL-API-KEY-HERE" ||
                _config.ApiKey == "YOUR-API-KEY-HERE")
            {
                return ValidationResult.Failure("API key is not configured. Please replace the placeholder in appsettings.json with your actual OpenAI API key");
            }

            // OpenAI API keys start with "sk-" (for secret keys)
            if (!_config.ApiKey.StartsWith("sk-"))
            {
                return ValidationResult.Failure("API key format is invalid. OpenAI API keys should start with 'sk-'");
            }

            // OpenAI keys are typically 48-51 characters
            if (_config.ApiKey.Length < 40)
            {
                return ValidationResult.Failure("API key seems too short. Please verify you copied the complete key from OpenAI");
            }

            return ValidationResult.Success("API key format valid");
        }

        /// <summary>
        /// Validates that the specified worksheet index exists in the Excel file
        /// </summary>
        /// <param name="excelPath">Path to Excel file selected by user via FileSelector</param>
        /// <param name="worksheetIndex">Zero-based worksheet index to validate (0=Sheet1, 1=Sheet2, etc.)</param>
        /// <returns>
        /// ValidationResult containing:
        /// - IsValid=true with sheet name in DetailedInfo if index is valid
        /// - IsValid=false with error message if index is negative or exceeds worksheet count
        /// </returns>
        /// <remarks>
        /// VALIDATION CHECKS:
        /// - Opens Excel file using EPPlus
        /// - Checks if worksheetIndex is negative (invalid)
        /// - Checks if worksheetIndex >= total worksheet count (out of range)
        /// - Returns worksheet name if valid (e.g., "Sheet: 'Sheet2'")
        /// 
        /// EXAMPLE ERROR MESSAGES:
        /// - worksheetIndex=-1 → "Worksheet index cannot be negative (selected: -1)"
        /// - worksheetIndex=5, file has 2 sheets → "Worksheet index 5 is out of range. 'test.xlsx' has 2 worksheet(s) (valid indexes: 0-1)"
        /// 
        /// Called during startup validation before any test processing begins.
        /// </remarks>
        public ValidationResult ValidateWorksheetIndex(string excelPath, int worksheetIndex)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(excelPath)))
                {
                    int worksheetCount = package.Workbook.Worksheets.Count;

                    if (worksheetIndex < 0)
                    {
                        return ValidationResult.Failure($"Worksheet index cannot be negative (selected: {worksheetIndex})");
                    }

                    if (worksheetIndex >= worksheetCount)
                    {
                        return ValidationResult.Failure($"Worksheet index {worksheetIndex} is out of range.\n" +
                            $"   '{Path.GetFileName(excelPath)}' has {worksheetCount} worksheet(s) (valid indexes: 0-{worksheetCount - 1})");
                    }

                    var worksheet = package.Workbook.Worksheets[worksheetIndex];
                    string worksheetName = worksheet.Name;

                    return ValidationResult.Success($"Worksheet index valid (Sheet: '{worksheetName}')");
                }
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure(
                    $"Error reading '{Path.GetFileName(excelPath)}' to validate worksheet index: {ex.Message}. " +
                    $"Ensure the file is not open in another program.");
            }
        }

        // Validate OpenAI API connection
        public async Task<ValidationResult> ValidateOpenAIConnectionAsync()
        {
            try
            {
                var openAiService = new OpenAIService(new OpenAI.OpenAiOptions()
                {
                    ApiKey = _config.ApiKey
                });

                // Make a minimal test request (1 token)
                var testRequest = new ChatCompletionCreateRequest
                {
                    Messages = new[]
                    {
                        ChatMessage.FromSystem("Test"),
                        ChatMessage.FromUser("Hi")
                    },
                    Model = _promptConfig.Model,
                    MaxTokens = 5
                };

                var response = await openAiService.ChatCompletion.CreateCompletion(testRequest);

                if (response.Successful)
                {
                    return ValidationResult.Success("OpenAI API connection successful");
                }
                else
                {
                    return ValidationResult.Failure($"OpenAI API error: {response.Error?.Message ?? "Unknown error"}\n" +
                        $"   Code: {response.Error?.Code}");
                }
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure(
                    $"Failed to connect to OpenAI API: {ex.Message}. " +
                    $"Check your internet connection and verify your API key in appsettings.json.");
            }
        }

        // Get detailed configuration info for troubleshooting
        // excelPath and worksheetIndex passed in since they come from FileSelector now
        public string GetConfigurationSummary(string excelPath, int worksheetIndex)
        {
            return $"Configuration Summary:\n" +
                   $"  Model: {_promptConfig.Model}\n" +
                   $"  Max Tokens: {_promptConfig.MaxTokens}\n" +
                   $"  Temperature: {_promptConfig.Temperature}\n" +
                   $"  Excel File: {Path.GetFileName(excelPath)}\n" +
                   $"  Worksheet Index: {worksheetIndex}\n" +
                   $"  API Key: {(_config.ApiKey?.Length > 10 ? _config.ApiKey.Substring(0, 7) + "..." : "Not set")}";
        }

        public ValidationResult ValidatePromptConfig()
        {
            // Check file exists
            if (!File.Exists("PromptConfig.json"))
            {
                return ValidationResult.Failure(
                    "PromptConfig.json not found. Please ensure the file exists in the application directory.");
            }

            // Check MaxTokens is reasonable
            if (_promptConfig.MaxTokens <= 0 || _promptConfig.MaxTokens > 10000)
            {
                return ValidationResult.Failure(
                    $"Invalid MaxTokens: {_promptConfig.MaxTokens}. Must be between 1 and 10000.");
            }

            // Check Temperature is in valid range
            if (_promptConfig.Temperature < 0 || _promptConfig.Temperature > 2.0)
            {
                return ValidationResult.Failure(
                    $"Invalid Temperature: {_promptConfig.Temperature}. Must be between 0.0 and 2.0.");
            }

            // Check Model is not empty
            if (string.IsNullOrWhiteSpace(_promptConfig.Model))
            {
                return ValidationResult.Failure("Model name is required in PromptConfig.json");
            }

            return ValidationResult.Success("PromptConfig is valid");
        }

    }
}
