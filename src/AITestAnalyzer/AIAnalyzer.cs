using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AITestAnalyzer
{
    // ============================================================
    // METHOD 3: Analyze Test Case with AI
    // FIXED: Now uses promptConfig.Model instead of hardcoded
    // OPTIMIZED: Only sends Feature, Scenario, Steps, Expected Result
    // ============================================================
    public class AIAnalyzer
    {
        private readonly Configuration _config;
        private readonly PromptConfig _promptConfig;
        private readonly OpenAIService _openAiService;

        public AIAnalyzer(Configuration config, PromptConfig promptConfig)
        {
            _config = config;
            _promptConfig = promptConfig;
            _openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = config.ApiKey
            });
        }

        /// <summary>
        /// Analyzes a test case using OpenAI GPT-4o-mini and returns quality feedback with token usage
        /// </summary>
        /// <param name="testCase">Test case to analyze (must have Feature, Scenario, Steps, ExpectedResult populated)</param>
        /// <returns>
        /// Tuple containing:
        /// - result: AI analysis feedback ("GOOD" or "Issue: [specific problem]")
        /// - tokens: Number of tokens used (0 if error occurred)
        /// </returns>
        /// <remarks>
        /// Implements automatic retry logic with exponential backoff (1s, 2s, 4s delays).
        /// Uses GPT-4o-mini model at temperature 0.2 for consistent results.
        /// Skips Priority, Status, and TestId fields to minimize token usage (84% reduction vs verbose mode).
        /// </remarks>
        /// <exception cref="Exception">Returns error message in result string after 3 failed retry attempts</exception>
        public async Task<(string quality, string coverage, int tokens)> AnalyzeTestCase(TestCase testCase, List<ExtractedRequirement> requirements)
        {
            int maxRetries = 3;
            int retryDelayMs = 1000; // Start with 1 second

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    // Build user prompt - only include relevant fields
                    string userPrompt = _promptConfig.UserTemplate
                        .Replace("{Requirements}", FormatRequirements(requirements))
                        .Replace("{Feature}", testCase.Feature)
                        .Replace("{Scenario}", testCase.Scenario)
                        .Replace("{Steps}", testCase.Steps)
                        .Replace("{ExpectedResult}", testCase.ExpectedResult);

                    var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest
                        {
                            Messages = new List<ChatMessage>
                            {
                        ChatMessage.FromSystem(_promptConfig.SystemMessage),
                        ChatMessage.FromUser(userPrompt)
                            },
                            Model = _promptConfig.Model,
                            MaxTokens = _promptConfig.MaxTokens,
                            Temperature = (float)_promptConfig.Temperature
                        });

                    if (completionResult.Successful)
                    {
                        string analysis = completionResult!.Choices.First().Message.Content!.Trim();
                        int tokens = completionResult.Usage!.TotalTokens;

                        // Parse Quality and Coverage
                        string quality = "Unknown";
                        string coverage = "None";

                        var lines = analysis.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase))
                            {
                                quality = line.Substring(8).Trim();
                            }
                            else if (line.StartsWith("Coverage:", StringComparison.OrdinalIgnoreCase))
                            {
                                coverage = line.Substring(9).Trim();
                            }
                        }

                        return (quality, coverage, tokens);
                    }
                    else
                    {
                        // API returned error
                        string errorMsg = completionResult.Error?.Message ?? "Unknown API error";

                        if (attempt < maxRetries)
                        {
                            // Retry with exponential backoff
                            Console.WriteLine($"      ⚠️  API error (attempt {attempt}/{maxRetries}): {errorMsg}");
                            Console.WriteLine($"      ⏳ Retrying in {retryDelayMs / 1000} seconds...");
                            await Task.Delay(retryDelayMs);
                            retryDelayMs *= 2; // Exponential backoff: 1s, 2s, 4s
                            continue;
                        }
                        else
                        {
                            // Max retries exceeded
                            return ("ERROR: Unexpected retry loop exit", "None", 0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Network error, timeout, etc.
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"      ⚠️  Exception (attempt {attempt}/{maxRetries}): {ex.Message}");
                        Console.WriteLine($"      ⏳ Retrying in {retryDelayMs / 1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2; // Exponential backoff
                        continue;
                    }
                    else
                    {
                        return ("ERROR: Unexpected retry loop exit", "None", 0);
                    }
                }
            }

            // Should never reach here, but just in case
            return ("ERROR: Unexpected retry loop exit", "None", 0);
        }

        /// <summary>
        /// QA MODE: Analyzes test quality without requirements
        /// </summary>
        public async Task<(string quality, int tokens)> AnalyzeTestQuality(TestCase testCase)
        {
            int maxRetries = 3;
            int retryDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    string systemMessage = "You are an expert QA analyst. Assess test case quality. Be concise and actionable.";

                    string userPrompt = $@"TEST CASE:
Feature: {testCase.Feature}
Scenario: {testCase.Scenario}
Steps: {testCase.Steps}
Expected Result: {testCase.ExpectedResult}

Provide BRIEF feedback:
Quality: [ONE sentence - either 'GOOD' or specific issue]

Be direct and actionable. Focus on: clarity, completeness, testability.";

                    var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest
                        {
                            Messages = new List<ChatMessage>
                            {
                        ChatMessage.FromSystem(systemMessage),
                        ChatMessage.FromUser(userPrompt)
                            },
                            Model = _promptConfig.Model,
                            MaxTokens = 250,
                            Temperature = (float)_promptConfig.Temperature
                        });

                    if (completionResult.Successful)
                    {
                        string analysis = completionResult.Choices.First().Message.Content!.Trim();
                        int tokens = completionResult.Usage!.TotalTokens;

                        // Extract quality feedback
                        string quality = analysis;
                        if (quality.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase))
                        {
                            quality = quality.Substring(8).Trim();
                        }

                        return (quality, tokens);
                    }
                    else
                    {
                        string errorMsg = completionResult.Error?.Message ?? "Unknown API error";
                        if (attempt < maxRetries)
                        {
                            Console.WriteLine($"      ⚠️  API error (attempt {attempt}/{maxRetries}): {errorMsg}");
                            Console.WriteLine($"      ⏳ Retrying in {retryDelayMs / 1000} seconds...");
                            await Task.Delay(retryDelayMs);
                            retryDelayMs *= 2;
                            continue;
                        }
                        else
                        {
                            return ("ERROR: API call failed after retries", 0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"      ⚠️  Exception (attempt {attempt}/{maxRetries}): {ex.Message}");
                        Console.WriteLine($"      ⏳ Retrying in {retryDelayMs / 1000} seconds...");
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2;
                        continue;
                    }
                    else
                    {
                        return ($"ERROR: {ex.Message}", 0);
                    }
                }
            }

            return ("ERROR: Unexpected retry loop exit", 0);
        }
        private string FormatRequirements(List<ExtractedRequirement> requirements)
        {
            var formatted = new StringBuilder();
            formatted.AppendLine("REQUIREMENTS TO VALIDATE:");
            formatted.AppendLine();

            foreach (var req in requirements)
            {
                formatted.AppendLine($"- {req.Topic} → {req.Subtopic}: {req.ExpectedAction}");
            }

            return formatted.ToString();
        }
    }
}
