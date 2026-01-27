using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        // Analyze test case with AI
        public async Task<(string result, int tokens)> AnalyzeTestCase(TestCase testCase)
        {
            try
            {
                // Build user prompt - only include relevant fields
                string userPrompt = _promptConfig.UserTemplate
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
                    string analysis = completionResult.Choices.First().Message.Content.Trim();
                    int tokens = completionResult.Usage.TotalTokens;
                    return (analysis, tokens);
                }
                else
                {
                    return ($"ERROR: {completionResult.Error?.Message}", 0);
                }
            }
            catch (Exception ex)
            {
                return ($"ERROR: {ex.Message}", 0);
            }
        }
    }
}