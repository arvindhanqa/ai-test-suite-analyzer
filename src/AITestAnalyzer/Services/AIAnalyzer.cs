using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AITestAnalyzer.Config;
using AITestAnalyzer.Infrastructure;
using AITestAnalyzer.Models;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;

namespace AITestAnalyzer.Services
{
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

        /// <summary>
        /// Formats a list of ExtractedRequirement objects into a plain-text string
        /// for inclusion in BA Mode prompts.
        /// </summary>
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

        /// <summary>
        /// GEN MODE: Generates test cases from requirements markdown.
        /// Calls OpenAI using GenModel and GenSystemMessage/GenUserTemplate from PromptConfig.
        /// Parses pipe-delimited response into List of GeneratedTestCase.
        /// </summary>
        public async Task<(List<GeneratedTestCase> TestCases, int Tokens)> GenerateTestCasesAsync(
            string requirementsMarkdown,
            int targetCount)
        {
            string systemMessage = _promptConfig.GenSystemMessage;
            string userPrompt = _promptConfig.GenUserTemplate
                .Replace("{targetCount}", targetCount.ToString())
                .Replace("{requirementsMarkdown}", requirementsMarkdown);

            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                            ChatMessage.FromSystem(systemMessage),
                            ChatMessage.FromUser(userPrompt)
                        },
                        Model = _promptConfig.GenModel,
                        MaxTokens = Constants.TOKENS_GEN_MODE,
                        Temperature = (float)_promptConfig.Temperature
                    }),
                isSuccess: r => r.Successful,
                getErrorMessage: r => r.Error?.Message ?? "Unknown API error"
            );

            if (result == null)
                return (new List<GeneratedTestCase>(), 0);

            string response = result.Choices.First().Message.Content!.Trim();
            int tokens = result.Usage!.TotalTokens;
            var testCases = ParseGeneratedTestCases(response);

            return (testCases, tokens);
        }

        /// <summary>
        /// Parses pipe-delimited AI response into a list of GeneratedTestCase objects.
        /// Format: TC-GEN-001|Feature|Scenario|Priority|Steps|ExpectedResult
        /// Skips malformed rows silently — logs a warning per skipped line.
        /// </summary>
        internal List<GeneratedTestCase> ParseGeneratedTestCases(string response)
        {
            var results = new List<GeneratedTestCase>();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                var parts = trimmed.Split('|');
                if (parts.Length < 6)
                {
                    Console.WriteLine($"⚠️  ParseGeneratedTestCases: skipping malformed line — {trimmed}");
                    continue;
                }

                results.Add(new GeneratedTestCase
                {
                    TestId = parts[0].Trim(),
                    Feature = parts[1].Trim(),
                    Scenario = parts[2].Trim(),
                    Priority = parts[3].Trim(),
                    Steps = parts[4].Trim().Replace("\\n", "\n"),
                    ExpectedResult = parts[5].Trim(),
                    PassNumber = 1,
                    GeneratedAt = DateTime.UtcNow
                });
            }

            return results;
        }

        /// <summary>
        /// GEN MODE: Critiques generated test cases against original requirements.
        /// Calls OpenAI using GenModel and CritiqueSystemMessage/CritiqueUserTemplate.
        /// Parses pipe-delimited response into List of CritiqueResult.
        /// </summary>
        public async Task<(List<CritiqueResult> Critiques, int Tokens)> CritiqueTestCasesAsync(
            List<GeneratedTestCase> testCases,
            string requirementsMarkdown)
        {
            string testCasesFormatted = FormatGeneratedTestCasesForPrompt(testCases);

            string systemMessage = _promptConfig.CritiqueSystemMessage;
            string userPrompt = _promptConfig.CritiqueUserTemplate
                .Replace("{requirementsMarkdown}", requirementsMarkdown)
                .Replace("{testCases}", testCasesFormatted);

            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                            ChatMessage.FromSystem(systemMessage),
                            ChatMessage.FromUser(userPrompt)
                        },
                        Model = _promptConfig.GenModel,
                        MaxTokens = Constants.TOKENS_CRITIQUE_MODE,
                        Temperature = (float)_promptConfig.Temperature
                    }),
                isSuccess: r => r.Successful,
                getErrorMessage: r => r.Error?.Message ?? "Unknown API error"
            );

            if (result == null)
                return (new List<CritiqueResult>(), 0);

            string response = result.Choices.First().Message.Content!.Trim();
            int tokens = result.Usage!.TotalTokens;
            var critiques = ParseCritiqueResults(response);

            return (critiques, tokens);
        }

        /// <summary>
        /// Parses pipe-delimited AI critique response into a list of CritiqueResult objects.
        /// Format: TC-GEN-001|KEEP|No issues
        /// Unknown action values default to KEEP. Malformed lines are skipped with a warning.
        /// </summary>
        internal List<CritiqueResult> ParseCritiqueResults(string response)
        {
            var results = new List<CritiqueResult>();
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                var parts = trimmed.Split('|');
                if (parts.Length < 3)
                {
                    Console.WriteLine($"⚠️  ParseCritiqueResults: skipping malformed line — {trimmed}");
                    continue;
                }

                string action = parts[1].Trim().ToUpper();
                if (action != Constants.CRITIQUE_KEEP &&
                    action != Constants.CRITIQUE_REVISE &&
                    action != Constants.CRITIQUE_DROP)
                {
                    Console.WriteLine($"⚠️  ParseCritiqueResults: unknown action '{action}' — defaulting to KEEP");
                    action = Constants.CRITIQUE_KEEP;
                }

                results.Add(new CritiqueResult
                {
                    TestId = parts[0].Trim(),
                    Action = action,
                    Reason = parts[2].Trim()
                });
            }

            return results;
        }

        /// <summary>
        /// Formats a list of GeneratedTestCase objects into pipe-delimited string
        /// for inclusion in Critique and Refine prompts.
        /// </summary>
        private static string FormatGeneratedTestCasesForPrompt(List<GeneratedTestCase> testCases)
        {
            return string.Join("\n", testCases.Select(tc =>
                $"{tc.TestId}|{tc.Feature}|{tc.Scenario}|{tc.Priority}|{tc.Steps.Replace("\n", "\\n")}|{tc.ExpectedResult}"));
        }

        /// <summary>
        /// GEN MODE: Refines generated test cases by applying critique feedback.
        /// Calls OpenAI using GenModel and RefineSystemMessage/RefineUserTemplate.
        /// KEEP items returned unchanged. REVISE items improved. DROP items removed.
        /// </summary>
        public async Task<(List<GeneratedTestCase> Refined, int Tokens)> RefineTestCasesAsync(
            List<GeneratedTestCase> testCases,
            List<CritiqueResult> critiques,
            string requirementsMarkdown)
        {
            // Short-circuit: if nothing to revise or drop, return unchanged
            bool hasChanges = critiques.Any(c =>
                            c.Action == Constants.CRITIQUE_REVISE || c.Action == Constants.CRITIQUE_DROP);
            if (!hasChanges)
            {
                Console.WriteLine("      ✅ All critiques are KEEP — skipping refinement pass.");
                return (testCases, 0);
            }

            string testCasesFormatted = FormatGeneratedTestCasesForPrompt(testCases);
            string critiqueFormatted = FormatCritiqueResultsForPrompt(critiques);

            string systemMessage = _promptConfig.RefineSystemMessage;
            string userPrompt = _promptConfig.RefineUserTemplate
                .Replace("{testCases}", testCasesFormatted)
                .Replace("{critiqueResults}", critiqueFormatted);

            var result = await RetryHelper.ExecuteWithRetryAsync(
                operation: () => _openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                            ChatMessage.FromSystem(systemMessage),
                            ChatMessage.FromUser(userPrompt)
                        },
                        Model = _promptConfig.GenModel,
                        MaxTokens = Constants.TOKENS_REFINE_MODE,
                        Temperature = (float)_promptConfig.Temperature
                    }),
                isSuccess: r => r.Successful,
                getErrorMessage: r => r.Error?.Message ?? "Unknown API error"
            );

            if (result == null)
            {
                Console.WriteLine("⚠️  RefineTestCasesAsync: API call failed — returning original test cases.");
                return (testCases, 0);
            }

            string response = result.Choices.First().Message.Content!.Trim();
            int tokens = result.Usage!.TotalTokens;

            // Parse refined output — increment PassNumber for revised items
            var refined = ParseGeneratedTestCases(response);
            var revisedIds = critiques
                .Where(c => c.Action == Constants.CRITIQUE_REVISE)
                .Select(c => c.TestId)
                .ToHashSet();

            foreach (var tc in refined)
            {
                if (revisedIds.Contains(tc.TestId))
                    tc.PassNumber = testCases
                        .FirstOrDefault(t => t.TestId == tc.TestId)?.PassNumber + 1 ?? 2;
            }

            return (refined, tokens);
        }

        /// <summary>
        /// Formats a list of CritiqueResult objects into pipe-delimited string
        /// for inclusion in the Refine prompt.
        /// </summary>
        private static string FormatCritiqueResultsForPrompt(List<CritiqueResult> critiques)
        {
            return string.Join("\n", critiques.Select(c =>
                $"{c.TestId}|{c.Action}|{c.Reason}"));
        }
    }
}
