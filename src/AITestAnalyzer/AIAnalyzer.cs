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

        /// <summary>
        /// BA MODE: Analyzes test coverage against requirements and provides feedback on gaps
        /// </summary>
        /// <param name="testCase">Test case to analyze</param>
        /// <param name="requirements">List of requirements to validate against</param>
        /// <returns>Tuple containing (requirement feedback, coverage IDs, tokens)</returns>
        public async Task<(string reqFeedback, List<string> coverageIds, int tokens)> AnalyzeCoverageAndFeedback(
            TestCase testCase,
            List<ExtractedRequirement> requirements)
        {
            int maxRetries = 3;
            int retryDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    string systemMessage = @"You are a BA analyzing test coverage against requirements. 
Provide CONCISE tweet-style feedback on gaps only. Be specific and actionable.";

                    string userPrompt = $@"REQUIREMENTS:
{FormatRequirements(requirements)}

TEST CASE:
Feature: {testCase.Feature}
Scenario: {testCase.Scenario}
Steps: {testCase.Steps}
Expected Result: {testCase.ExpectedResult}

Respond in this EXACT format:

COVERAGE_IDS: [comma-separated requirement IDs that this test touches]
FEEDBACK:
❌ [what's missing] - [how to fix]

RULES:
- COVERAGE_IDS: ALL requirement IDs this test touches (covered OR missing)
- FEEDBACK: ONLY list MISSING/INCOMPLETE validations (use ❌)
- If test covers all requirements completely, return ""FEEDBACK: COMPLETE""
- Keep each feedback line under 100 chars
- Be specific and actionable

Example:
COVERAGE_IDS: TM-01,TM-03,UA-01
FEEDBACK:
❌ Task priority validation missing (TM-03) - add step to verify priority field
❌ Due date validation missing (TM-05) - verify past dates rejected";

                    var completionResult = await _openAiService.ChatCompletion.CreateCompletion(
                        new ChatCompletionCreateRequest
                        {
                            Messages = new List<ChatMessage>
                            {
                                ChatMessage.FromSystem(systemMessage),
                                ChatMessage.FromUser(userPrompt)
                            },
                            Model = _promptConfig.Model,
                            MaxTokens = 1000,
                            Temperature = (float)_promptConfig.Temperature
                        });

                    if (completionResult.Successful)
                    {
                        string response = completionResult.Choices.First().Message.Content!.Trim();
                        int tokens = completionResult.Usage!.TotalTokens;

                        // Parse the response
                        var (coverageIds, reqFeedback) = ParseCoverageResponse(response);

                        return (reqFeedback, coverageIds, tokens);
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
                            return ("ERROR: API call failed after retries", new List<string>(), 0);
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
                        return ($"ERROR: {ex.Message}", new List<string>(), 0);
                    }
                }
            }

            return ("ERROR: Unexpected retry loop exit", new List<string>(), 0);
        }

        /// <summary>
        /// Parse AI response to extract coverage IDs and requirement feedback
        /// </summary>
        private (List<string> ids, string feedback) ParseCoverageResponse(string response)
        {
            try
            {
                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                   .Select(l => l.Trim())
                                   .ToList();

                // Extract COVERAGE_IDS line
                var idsLine = lines.FirstOrDefault(l => l.StartsWith("COVERAGE_IDS:", StringComparison.OrdinalIgnoreCase));
                var ids = new List<string>();

                if (idsLine != null)
                {
                    string idsText = idsLine.Substring("COVERAGE_IDS:".Length).Trim();
                    ids = idsText.Split(',')
                                .Select(id => id.Trim())
                                .Where(id => !string.IsNullOrWhiteSpace(id))
                                .ToList();
                }

                // Extract FEEDBACK section
                var feedbackIndex = lines.FindIndex(l => l.StartsWith("FEEDBACK:", StringComparison.OrdinalIgnoreCase));

                if (feedbackIndex < 0)
                {
                    return (ids, string.Empty);
                }

                // Check if next line is "COMPLETE"
                if (feedbackIndex + 1 < lines.Count &&
                    lines[feedbackIndex + 1].Equals("COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    return (ids, string.Empty); // No gaps = empty feedback
                }

                // Extract feedback lines (skip "FEEDBACK:" header)
                var feedbackLines = lines
                    .Skip(feedbackIndex + 1)
                    .Where(l => l.StartsWith("❌") || l.StartsWith("*") || l.StartsWith("-"))
                    .ToList();

                string feedback = string.Join("\n", feedbackLines);

                return (ids, feedback);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error parsing coverage response: {ex.Message}");
                return (new List<string>(), "ERROR: Could not parse AI response");
            }
        }
        private string FormatRequirements(List<ExtractedRequirement> requirements)
        {
            var formatted = new StringBuilder();
            formatted.AppendLine("REQUIREMENTS TO VALIDATE:");
            formatted.AppendLine();

            foreach (var req in requirements)
            {
                formatted.AppendLine($"- {req.Id}: {req.Topic} → {req.Subtopic}: {req.ExpectedAction}");
            }

            return formatted.ToString();
        }
    }
}
