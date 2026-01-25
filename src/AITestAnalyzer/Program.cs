using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AITestAnalyzer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Aravindhan Rajasekaran");

            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║    AI Test Suite Analyzer - Day 6         ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
            Console.WriteLine();

            // STEP 1: Load configurations
            var (appConfig, promptConfig) = LoadConfiguration();
            if (appConfig == null || promptConfig == null) return;

            // STEP 2 & 3: Process multiple test cases
            Console.WriteLine("📊 Analyzing test cases...");
            Console.WriteLine();

            var startTime = DateTime.Now;
            var results = new List<(string TestId, string Result, int Tokens)>();

            for (int row = 2; row <= 6; row++)  // First 5 tests
            {
                TestCase testCase = ReadTestCaseFromExcel(appConfig.ExcelPath, rowNumber: row);
                if (testCase == null) continue;

                var (result, tokens) = await AnalyzeTestCaseWithAI(testCase, appConfig, promptConfig);
                results.Add((testCase.TestId, result, tokens));

                await Task.Delay(1000);  // Rate limiting
            }

            var endTime = DateTime.Now;

            // Display clean results
            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║          TEST QUALITY ANALYSIS             ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
            Console.WriteLine();

            foreach (var (testId, result, _) in results)
            {
                Console.WriteLine($"{testId}: {result}");
            }

            // Display summary statistics
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("📊 ANALYSIS SUMMARY");
            Console.WriteLine("═══════════════════════════════════════════════");

            int totalTests = results.Count;
            int goodTests = results.Count(r => r.Result == "GOOD");
            int issueTests = totalTests - goodTests;
            int totalTokens = results.Sum(r => r.Tokens);
            double totalCost = totalTokens * 0.00000015;
            int avgTokens = totalTokens / totalTests;
            int oldAvgTokens = 750;
            double percentSaved = ((oldAvgTokens - avgTokens) / (double)oldAvgTokens) * 100;
            var timeTaken = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Tests analyzed: {totalTests}");
            Console.WriteLine($"✅ Good tests: {goodTests} ({(goodTests * 100.0 / totalTests):F0}%)");
            Console.WriteLine($"⚠️  Tests with issues: {issueTests} ({(issueTests * 100.0 / totalTests):F0}%)");
            Console.WriteLine();
            Console.WriteLine($"Total tokens used: {totalTokens:N0}");
            Console.WriteLine($"Total cost: ${totalCost:F6}");
            Console.WriteLine($"Average tokens per test: {avgTokens}");
            Console.WriteLine($"💰 Average savings: {percentSaved:F0}% vs verbose mode");
            Console.WriteLine();
            Console.WriteLine($"⏱️  Time taken: {timeTaken:F1} seconds");
            Console.WriteLine("═══════════════════════════════════════════════");

            Console.WriteLine();
            Console.WriteLine("🎉 DAY 6 COMPLETE - ANALYSIS FINISHED!");
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        // ============================================================
        // METHOD 1: Load Configuration
        // USES: Configuration.cs class
        // ============================================================
        static (Configuration appConfig, PromptConfig promptConfig) LoadConfiguration()
        {
            Console.WriteLine("📋 Step 1: Loading configuration...");

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("PromptConfig.json", optional: false, reloadOnChange: true)
                .Build();

            // Load app configuration
            string apiKey = configBuilder["OpenAI:ApiKey"];
            string model = configBuilder["OpenAI:Model"] ?? "gpt-4o-mini";
            string excelPath = configBuilder["Excel:FilePath"];

            // Validate API key
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR-ACTUAL-API-KEY-HERE")
            {
                Console.WriteLine("❌ ERROR: OpenAI API key not configured!");
                Console.WriteLine("Please update appsettings.json with your actual API key.");
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return (null, null);
            }

            var appConfig = new Configuration
            {
                ApiKey = apiKey,
                Model = model,
                ExcelPath = excelPath
            };

            // Load prompt configuration
            var promptConfig = new PromptConfig
            {
                MaxTokens = int.Parse(configBuilder["MaxTokens"] ?? "150"),
                Model = configBuilder["Model"] ?? "gpt-4o-mini",
                Temperature = double.Parse(configBuilder["Temperature"] ?? "0.3"),
                SystemMessage = configBuilder["SystemMessage"] ?? "You are an expert QA analyzer.",
                UserTemplate = configBuilder["UserTemplate"] ?? "Analyze: {TestId}"
            };

            Console.WriteLine($"✅ Configuration loaded!");
            Console.WriteLine($"   API Key: {apiKey.Substring(0, 7)}...{apiKey.Substring(apiKey.Length - 4)}");
            Console.WriteLine($"   Model: {promptConfig.Model}");
            Console.WriteLine($"   Max Tokens: {promptConfig.MaxTokens}");
            Console.WriteLine($"   Temperature: {promptConfig.Temperature}");
            Console.WriteLine();

            return (appConfig, promptConfig);
        }

        // ============================================================
        // METHOD 2: Read Test Case from Excel
        // USES: TestCase.cs class
        // ============================================================
        static TestCase ReadTestCaseFromExcel(string excelPath, int rowNumber = 2)
        {
            if (!File.Exists(excelPath))
            {
                Console.WriteLine($"❌ ERROR: Excel file not found at: {excelPath}");
                return null;
            }

            try
            {
                using (var package = new ExcelPackage(new FileInfo(excelPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1];

                    var testCase = new TestCase
                    {
                        TestId = worksheet.Cells[rowNumber, 1].Value?.ToString() ?? "",
                        Feature = worksheet.Cells[rowNumber, 2].Value?.ToString() ?? "",
                        Scenario = worksheet.Cells[rowNumber, 3].Value?.ToString() ?? "",
                        Priority = worksheet.Cells[rowNumber, 4].Value?.ToString() ?? "",
                        Steps = worksheet.Cells[rowNumber, 5].Value?.ToString() ?? "",
                        ExpectedResult = worksheet.Cells[rowNumber, 6].Value?.ToString() ?? "",
                        Status = worksheet.Cells[rowNumber, 7].Value?.ToString() ?? ""
                    };

                    return testCase;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Excel Error: {ex.Message}");
                return null;
            }
        }

        // ============================================================
        // METHOD 3: Analyze Test Case with AI
        // OPTIMIZED: Tweet-style feedback (GOOD vs Issue: X)
        // ============================================================
        static async Task<(string result, int tokens)> AnalyzeTestCaseWithAI(TestCase testCase, Configuration config, PromptConfig promptConfig)
        {
            try
            {
                var openAiService = new OpenAIService(new OpenAiOptions()
                {
                    ApiKey = config.ApiKey
                });

                // Build user prompt from template
                string userPrompt = promptConfig.UserTemplate
                    .Replace("{TestId}", testCase.TestId)
                    .Replace("{Feature}", testCase.Feature)
                    .Replace("{Scenario}", testCase.Scenario)
                    .Replace("{Priority}", testCase.Priority)
                    .Replace("{Steps}", testCase.Steps)
                    .Replace("{ExpectedResult}", testCase.ExpectedResult);

                var completionResult = await openAiService.ChatCompletion.CreateCompletion(
                    new ChatCompletionCreateRequest
                    {
                        Messages = new List<ChatMessage>
                        {
                    ChatMessage.FromSystem(promptConfig.SystemMessage),
                    ChatMessage.FromUser(userPrompt)
                        },
                        Model = Models.Gpt_4o_mini,
                        MaxTokens = promptConfig.MaxTokens,
                        Temperature = (float)promptConfig.Temperature
                    });

                if (completionResult.Successful)
                {
                    string analysis = completionResult.Choices.First().Message.Content.Trim();
                    int tokens = completionResult.Usage.TotalTokens;

                    return (analysis, tokens);  // Return result and tokens
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