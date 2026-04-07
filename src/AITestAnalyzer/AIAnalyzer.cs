using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AITestAnalyzer.Models;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AITestAnalyzer
{
    // ============================================================
    // METHOD 3: Analyze Test Case with AI
    // Now uses promptConfig.Model instead of hardcoded
    // OPTIMIZED: Only sends Feature, Scenario, Steps, Expected Result
    // ============================================================
    public class AIAnalyzer : IAIAnalyzer
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
        public async Task<(string quality, int tokens)> AnalyzeTestQualityAsync(TestCase testCase)
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

            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                    ChatMessage.FromSystem(systemMessage),
                    ChatMessage.FromUser(userPrompt)
                        },
                        Model = _promptConfig.Model,
                        MaxTokens = Constants.TOKENS_QA_MODE,
                        Temperature = (float)_promptConfig.Temperature
                    }),
                isSuccess: r => r.Successful,
                getErrorMessage: r => r.Error?.Message ?? "Unknown API error"
            );

            if (result == null)
                return ($"ERROR: OpenAI API call failed after {Constants.MAX_RETRIES} retries " +
                            $"for test '{testCase.TestId}'. Check your API key and network connection.", 0);
            string analysis = result.Choices.First().Message.Content!.Trim();
            int tokens = result.Usage!.TotalTokens;

            string quality = analysis;
            if (quality.StartsWith("Quality:", StringComparison.OrdinalIgnoreCase))
                quality = quality.Substring(8).Trim();

            return (quality, tokens);
        }

        /// <summary>
        /// BA MODE: Analyzes test coverage against requirements and provides feedback on gaps
        /// </summary>
        /// <param name="testCase">Test case to analyze</param>
        /// <param name="requirements">List of requirements to validate against</param>
        /// <returns>Tuple containing (requirement feedback, coverage IDs, tokens)</returns>
        public async Task<(string reqFeedback, List<string> coverageIds, int tokens)> AnalyzeCoverageAndFeedbackAsync(
            TestCase testCase,
            List<ExtractedRequirement> requirements)
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

            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                    ChatMessage.FromSystem(systemMessage),
                    ChatMessage.FromUser(userPrompt)
                        },
                        Model = _promptConfig.Model,
                        MaxTokens = Constants.TOKENS_BA_MODE,
                        Temperature = (float)_promptConfig.Temperature
                    }),
                isSuccess: r => r.Successful,
                getErrorMessage: r => r.Error?.Message ?? "Unknown API error"
            );

            if (result == null)
                return ($"ERROR: OpenAI API call failed after {Constants.MAX_RETRIES} retries " +
                        $"for test '{testCase.TestId}'. Check your API key and network connection.",
                        new List<string>(), 0);
            string response = result.Choices.First().Message.Content!.Trim();
            int tokens = result.Usage!.TotalTokens;

            var (coverageIds, reqFeedback) = ParseCoverageResponse(response);
            return (reqFeedback, coverageIds, tokens);
        }

        /// <summary>
        /// Parse AI response to extract coverage IDs and requirement feedback.
        /// Handles varied AI formatting: markdown bold, inline/separate-line IDs,
        /// plain-text feedback, and inline COMPLETE on the FEEDBACK line.
        /// </summary>
        private (List<string> ids, string feedback) ParseCoverageResponse(string response)
        {
            try
            {
                var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(l => StripMarkdown(l.Trim()))
                                    .Where(l => !string.IsNullOrWhiteSpace(l))
                                    .ToList();

                var ids = new List<string>();
                var feedbackLines = new List<string>();

                var idsPattern = new Regex(@"^coverage[_\s-]*ids?\s*:(.*)", RegexOptions.IgnoreCase);
                var feedbackPattern = new Regex(@"^feedback\s*:(.*)", RegexOptions.IgnoreCase);

                bool lookingForIdsOnNextLine = false;
                bool inFeedback = false;

                foreach (var line in lines)
                {
                    // ── IDs section ─────────────────────────────────────────────
                    if (!ids.Any() && !inFeedback)
                    {
                        var idsMatch = idsPattern.Match(line);
                        if (idsMatch.Success)
                        {
                            var idsText = idsMatch.Groups[1].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(idsText))
                                ids = ParseIdList(idsText);
                            else
                                lookingForIdsOnNextLine = true;
                            continue;
                        }

                        if (lookingForIdsOnNextLine)
                        {
                            lookingForIdsOnNextLine = false;
                            ids = ParseIdList(line);
                            continue;
                        }
                    }

                    // ── Feedback section ─────────────────────────────────────────
                    var feedbackMatch = feedbackPattern.Match(line);
                    if (feedbackMatch.Success)
                    {
                        inFeedback = true;

                        // Handle "FEEDBACK: COMPLETE" or "FEEDBACK: some inline text"
                        var inlineText = feedbackMatch.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(inlineText))
                        {
                            if (!inlineText.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase))
                                feedbackLines.Add(inlineText);
                            else
                                break; // COMPLETE inline — we're done
                        }
                        continue;
                    }

                    if (inFeedback)
                    {
                        if (line.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase))
                            break;

                        feedbackLines.Add(line);
                    }
                }

                // Warn on partial parse so silent failures surface in output
                if (!ids.Any() || (feedbackLines.Count == 0 && !response.Contains("COMPLETE", StringComparison.OrdinalIgnoreCase)))
                {
                    string snippet = response.Length > 200 ? response[..200] : response;
                    Console.WriteLine($"      ⚠️  ParseCoverageResponse: partial parse — " +
                                      $"IDs found: {ids.Count}, Feedback lines: {feedbackLines.Count}");
                    Console.WriteLine($"      ⚠️  Response snippet: {snippet}");
                }

                return (ids, string.Join("\n", feedbackLines));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error parsing coverage response: {ex.Message}");
                return (new List<string>(), "ERROR: Could not parse AI response");
            }
        }

        /// <summary>Splits a comma/semicolon-separated ID string into a clean list.</summary>
        private static List<string> ParseIdList(string idsText) =>
            idsText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                   .Select(id => id.Trim())
                   .Where(id => !string.IsNullOrWhiteSpace(id))
                   .ToList();

        /// <summary>Strips markdown formatting characters so they don't break pattern matching.</summary>
        private static string StripMarkdown(string line) => Regex.Replace(line, @"[\*_`#>~]", string.Empty);

        private string FormatRequirements(List<ExtractedRequirement> requirements)
        {
            var formatted = new StringBuilder();
            formatted.AppendLine("REQUIREMENTS TO VALIDATE:");
            formatted.AppendLine();

            foreach (var req in requirements)
            {
                formatted.AppendLine($"- {req.Id}: {req.GetDisplayText()}: {req.Description}");
            }

            return formatted.ToString();
        }
    }
}
