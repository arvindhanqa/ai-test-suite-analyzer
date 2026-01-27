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

            Console.WriteLine("===============================================");
            Console.WriteLine("AI Test Suite Analyzer - Week 1");
            Console.WriteLine("===============================================");
            Console.WriteLine();

            // STEP 1: Load configurations
            var (appConfig, promptConfig) = LoadConfiguration();
            if (appConfig == null || promptConfig == null) return;

            // STEP 2: Prepare output file
            Console.WriteLine("📁 Preparing output file...");
            string outputDir = ExcelWriter.CreateOutputFolder();
            string outputPath = ExcelWriter.PrepareOutputFile(appConfig.ExcelPath, outputDir);

            var excelWriter = new ExcelWriter(outputPath);// Need to use outputPath here
            excelWriter.RenameOriginalSheet();  
            excelWriter.AddAnalysisColumnHeader();
            Console.WriteLine();

            // STEP 3: Process test cases
            int startRow = 2;  // First data row (row 1 is header)
            int totalTests;

            // Count actual rows in Excel
            var excelReader = new ExcelReader(appConfig.ExcelPath);
            int totalRowsInExcel = excelReader.CountTestRows();

            if (totalRowsInExcel == 0)
            {
                Console.WriteLine("❌ ERROR: No test cases found in Excel file");
                return;
            }

            // Check if command-line argument provided
            if (args.Length > 0 && int.TryParse(args[0], out int argTests))
            {
                // Command-line argument provided
                if (argTests > totalRowsInExcel)
                {
                    Console.WriteLine($"⚠️  WARNING: Requested {argTests} tests, but only {totalRowsInExcel} exist in Excel");
                    Console.WriteLine($"   Analyzing all {totalRowsInExcel} tests instead...");
                    totalTests = totalRowsInExcel;
                }
                else
                {
                    totalTests = argTests;
                    Console.WriteLine($"📊 Analyzing {totalTests} of {totalRowsInExcel} test cases (from command line)...");
                }
            }
            else
            {
                // No command-line argument - ASK USER
                Console.WriteLine($"📊 Found {totalRowsInExcel} test cases in Excel.");
                Console.Write($"   How many tests to analyze? (Enter number or press Enter for all): ");

                string userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    // User pressed Enter - process all
                    totalTests = totalRowsInExcel;
                    Console.WriteLine($"   → Analyzing all {totalTests} test cases...");
                }
                else if (int.TryParse(userInput, out int userTests) && userTests > 0)
                {
                    // User entered a number
                    if (userTests > totalRowsInExcel)
                    {
                        Console.WriteLine($"   ⚠️  Requested {userTests} tests, but only {totalRowsInExcel} exist.");
                        Console.WriteLine($"   → Analyzing all {totalRowsInExcel} tests instead...");
                        totalTests = totalRowsInExcel;
                    }
                    else
                    {
                        totalTests = userTests;
                        Console.WriteLine($"   → Analyzing {totalTests} of {totalRowsInExcel} test cases...");
                    }
                }
                else
                {
                    Console.WriteLine("   ❌ Invalid input. Please enter a positive number.");
                    return;
                }
            }

            // Validate
            if (totalTests < 1)
            {
                Console.WriteLine("❌ ERROR: Test count must be at least 1");
                return;
            }

            Console.WriteLine();

            var startTime = DateTime.Now;
            var results = new List<(string TestId, string Result, int Tokens)>();
            int processedCount = 0;

            for (int row = startRow; row < startRow + totalTests; row++)
            {
                TestCase testCase = excelReader.ReadTestCase(rowNumber: row);
                if (testCase == null)
                {
                    continue; // Silently skip empty rows
                }

                processedCount++;

                // Calculate progress
                double percentComplete = (processedCount * 100.0) / totalTests;

                // Estimate remaining time
                var elapsedTime = (DateTime.Now - startTime).TotalSeconds;
                double avgTimePerTest = processedCount > 0 ? elapsedTime / processedCount : 0;
                double estimatedRemaining = (totalTests - processedCount) * avgTimePerTest;

                // Build progress bar (20 characters wide)
                int barWidth = 20;
                int filledWidth = (int)(barWidth * percentComplete / 100);
                string progressBar = "[" + new string('=', filledWidth) + new string('.', barWidth - filledWidth) + "]";

                // Format time remaining
                TimeSpan remainingSpan = TimeSpan.FromSeconds(estimatedRemaining);
                string timeRemaining = remainingSpan.TotalMinutes >= 1
                    ? $"{(int)remainingSpan.TotalMinutes}m {remainingSpan.Seconds}s"
                    : $"{remainingSpan.Seconds}s";

                // Display progress on single line (overwrites previous line)
                Console.Write($"\r   {progressBar} {percentComplete:F1}% | {processedCount}/{totalTests} | {testCase.TestId} | ETA: {timeRemaining}   ");

                var (result, tokens) = await AnalyzeTestCaseWithAI(testCase, appConfig, promptConfig);
                results.Add((testCase.TestId, result, tokens));

                // Write to Excel immediately
                excelWriter.WriteAnalysis( row, result);

                await Task.Delay(1000);  // Rate limiting
            }

            var endTime = DateTime.Now;
            Console.WriteLine(); // New line after progress bar
            Console.WriteLine("   ✅ Analysis complete!");
            Console.WriteLine();

            // STEP 4: Create Quality Issues Sheet
            Console.WriteLine();
            Console.WriteLine("📋 Creating Quality Issues Summary...");
            excelWriter.CreateQualityIssuesSheet(results);

            // STEP 5: Create Statistics Dashboard
            Console.WriteLine("📊 Creating Statistics Dashboard...");
            excelWriter.CreateStatisticsDashboard(results, startTime, endTime);

            // STEP 6: Display summary
            Console.WriteLine();
            DisplaySummary(results, startTime, endTime, outputPath);

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // METHOD 1: Load Configuration
        // ============================================================
        static (Configuration appConfig, PromptConfig promptConfig) LoadConfiguration()
        {
            Console.WriteLine("📋 Loading configuration...");

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("PromptConfig.json", optional: false, reloadOnChange: true)
                .Build();

            string apiKey = configBuilder["OpenAI:ApiKey"];
            string model = configBuilder["OpenAI:Model"] ?? "gpt-4o-mini";
            string excelPath = configBuilder["Excel:FilePath"];

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR-ACTUAL-API-KEY-HERE")
            {
                Console.WriteLine("❌ ERROR: OpenAI API key not configured!");
                Console.WriteLine("Please update appsettings.json with your actual API key.");
                return (null, null);
            }

            var appConfig = new Configuration
            {
                ApiKey = apiKey,
                Model = model,
                ExcelPath = excelPath
            };

            var promptConfig = new PromptConfig
            {
                MaxTokens = int.Parse(configBuilder["MaxTokens"] ?? "150"),
                Model = configBuilder["Model"] ?? "gpt-4o-mini",
                Temperature = double.Parse(configBuilder["Temperature"] ?? "0.2"),
                SystemMessage = configBuilder["SystemMessage"] ?? "You are an expert QA analyzer.",
                UserTemplate = configBuilder["UserTemplate"] ?? "Analyze: {Scenario}"
            };

            Console.WriteLine($"   ✅ Model: {promptConfig.Model}");
            Console.WriteLine($"   ✅ Max Tokens: {promptConfig.MaxTokens}");
            Console.WriteLine();

            return (appConfig, promptConfig);
        }

        // ============================================================
        // METHOD 3: Analyze Test Case with AI
        // FIXED: Now uses promptConfig.Model instead of hardcoded
        // OPTIMIZED: Only sends Feature, Scenario, Steps, Expected Result
        // ============================================================
        static async Task<(string result, int tokens)> AnalyzeTestCaseWithAI(TestCase testCase, Configuration config, PromptConfig promptConfig)
        {
            try
            {
                var openAiService = new OpenAIService(new OpenAiOptions()
                {
                    ApiKey = config.ApiKey
                });

                // Build user prompt - only include relevant fields
                string userPrompt = promptConfig.UserTemplate
                    .Replace("{Feature}", testCase.Feature)
                    .Replace("{Scenario}", testCase.Scenario)
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
                        Model = promptConfig.Model,  // ← FIXED! Uses config now ✅
                        MaxTokens = promptConfig.MaxTokens,
                        Temperature = (float)promptConfig.Temperature
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

        // ============================================================
        // METHOD 8: Display Summary Statistics
        // ============================================================
        static void DisplaySummary(List<(string TestId, string Result, int Tokens)> results, DateTime start, DateTime end, string outputPath)
        {
            Console.WriteLine("===============================================");
            Console.WriteLine("📊 ANALYSIS SUMMARY");
            Console.WriteLine("===============================================");

            int totalTests = results.Count;
            int goodTests = results.Count(r => r.Result == "GOOD");
            int issueTests = totalTests - goodTests;
            int totalTokens = results.Sum(r => r.Tokens);
            double totalCost = totalTokens * 0.00000015;
            int avgTokens = totalTests > 0 ? totalTokens / totalTests : 0;
            var timeTaken = (end - start).TotalSeconds;

            Console.WriteLine($"Tests analyzed: {totalTests}");
            Console.WriteLine($"✅ Good tests: {goodTests} ({(totalTests > 0 ? goodTests * 100.0 / totalTests : 0):F0}%)");
            Console.WriteLine($"⚠️  Tests with issues: {issueTests} ({(totalTests > 0 ? issueTests * 100.0 / totalTests : 0):F0}%)");
            Console.WriteLine();
            Console.WriteLine($"Total tokens: {totalTokens:N0}");
            Console.WriteLine($"Total cost: ${totalCost:F6}");
            Console.WriteLine($"Avg tokens/test: {avgTokens}");
            Console.WriteLine($"⏱️  Time: {timeTaken:F1} seconds");
            Console.WriteLine();
            Console.WriteLine($"📁 Output: {Path.GetFileName(outputPath)}");
            Console.WriteLine($"   Location: {Path.GetDirectoryName(outputPath)}");
            Console.WriteLine("===============================================");
        }
    }
}