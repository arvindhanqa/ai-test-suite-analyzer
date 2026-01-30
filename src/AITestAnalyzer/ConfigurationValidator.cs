using System;
using System.IO;
using System.Threading.Tasks;
using OfficeOpenXml;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AITestAnalyzer
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public string DetailedInfo { get; set; }

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
        public async Task<(bool IsValid, string ErrorMessage)> ValidateAll()
        {
            // 1. Validate API Key
            var apiKeyResult = ValidateApiKey();
            if (!apiKeyResult.IsValid)
            {
                return (false, apiKeyResult.ErrorMessage);
            }

            // 2. Validate Excel File
            var excelResult = ValidateExcelFile();
            if (!excelResult.IsValid)
            {
                return (false, excelResult.ErrorMessage);
            }

            // 3. Validate Worksheet Index
            var worksheetResult = ValidateWorksheetIndex();
            if (!worksheetResult.IsValid)
            {
                return (false, worksheetResult.ErrorMessage);
            }

            // 4. Validate OpenAI Connection (optional but recommended)
            var connectionResult = await ValidateOpenAIConnection();
            if (!connectionResult.IsValid)
            {
                return (false, connectionResult.ErrorMessage);
            }

            return (true, "All validations passed");
        }

        // Validate API Key format
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

        // Validate Excel file exists and is accessible
        public ValidationResult ValidateExcelFile()
        {
            if (string.IsNullOrWhiteSpace(_config.ExcelPath))
            {
                return ValidationResult.Failure("Excel file path is not configured in appsettings.json");
            }

            if (!File.Exists(_config.ExcelPath))
            {
                return ValidationResult.Failure($"Excel file not found at: {_config.ExcelPath}\n" +
                    $"   Please verify the path in appsettings.json or create the file");
            }

            // Try to open the file to ensure it's accessible
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_config.ExcelPath)))
                {
                    if (package.Workbook.Worksheets.Count == 0)
                    {
                        return ValidationResult.Failure($"Excel file has no worksheets: {_config.ExcelPath}");
                    }
                }
            }
            catch (IOException ex)
            {
                return ValidationResult.Failure($"Excel file is locked or cannot be accessed: {ex.Message}\n" +
                    $"   Please close the file in Excel and try again");
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Excel file is corrupted or invalid: {ex.Message}");
            }

            return ValidationResult.Success($"Excel file exists and is accessible");
        }

        // Validate worksheet index
        public ValidationResult ValidateWorksheetIndex()
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_config.ExcelPath)))
                {
                    int worksheetCount = package.Workbook.Worksheets.Count;

                    if (_config.WorksheetIndex < 0)
                    {
                        return ValidationResult.Failure($"Worksheet index cannot be negative (configured: {_config.WorksheetIndex})");
                    }

                    if (_config.WorksheetIndex >= worksheetCount)
                    {
                        return ValidationResult.Failure($"Worksheet index {_config.WorksheetIndex} is out of range.\n" +
                            $"   Excel file has {worksheetCount} worksheet(s) (valid indexes: 0-{worksheetCount - 1})");
                    }

                    var worksheet = package.Workbook.Worksheets[_config.WorksheetIndex];
                    string worksheetName = worksheet.Name;

                    return ValidationResult.Success($"Worksheet index valid (Sheet: '{worksheetName}')");
                }
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Error validating worksheet index: {ex.Message}");
            }
        }

        // Validate OpenAI API connection
        public async Task<ValidationResult> ValidateOpenAIConnection()
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
                return ValidationResult.Failure($"Failed to connect to OpenAI API: {ex.Message}\n" +
                    $"   Please check your internet connection and API key");
            }
        }

        // Get detailed configuration info for troubleshooting
        public string GetConfigurationSummary()
        {
            return $"Configuration Summary:\n" +
                   $"  Model: {_promptConfig.Model}\n" +
                   $"  Max Tokens: {_promptConfig.MaxTokens}\n" +
                   $"  Temperature: {_promptConfig.Temperature}\n" +
                   $"  Excel Path: {_config.ExcelPath}\n" +
                   $"  Worksheet Index: {_config.WorksheetIndex}\n" +
                   $"  API Key: {(_config.ApiKey?.Length > 10 ? _config.ApiKey.Substring(0, 7) + "..." : "Not set")}";
        }
    }
}