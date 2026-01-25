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

            Console.WriteLine("AI Test Suite Analyzer - Week 1");
            Console.WriteLine("===============================================");
            Console.WriteLine();

            // STEP 1: Load configurations
            var (appConfig, promptConfig) = LoadConfiguration();
            if (appConfig == null || promptConfig == null) return;

            // STEP 2: Prepare output file
            Console.WriteLine("📁 Preparing output file...");
            string outputDir = CreateOutputFolder();
            string outputPath = PrepareOutputFile(appConfig.ExcelPath, outputDir);
            AddAnalysisColumnHeader(outputPath);
            Console.WriteLine();

            // STEP 3: Process test cases
            int startRow = 2;  // First data row (row 1 is header)
            int totalTests = 56; // Start with 5 for testing
            Console.WriteLine($"📊 Analyzing {totalTests} test cases...");
            Console.WriteLine();

            var startTime = DateTime.Now;
            var results = new List<(string TestId, string Result, int Tokens)>();

            for (int row = startRow; row <= totalTests+1; row++)  // Loops for each test case to the totalTests in the Excel
            {
                TestCase testCase = ReadTestCaseFromExcel(appConfig.ExcelPath, rowNumber: row);
                if (testCase == null)
                {
                    Console.WriteLine($"   ⚠️  Row {row}: Skipped (empty or invalid)");
                    continue;
                }

                Console.Write($"   Processing {testCase.TestId}...");

                var (result, tokens) = await AnalyzeTestCaseWithAI(testCase, appConfig, promptConfig);
                results.Add((testCase.TestId, result, tokens));

                // Write to Excel immediately
                WriteAnalysisToExcel(outputPath, row, result);

                Console.WriteLine(" ✅");

                await Task.Delay(1000);  // Rate limiting
            }

            var endTime = DateTime.Now;

            // STEP 4: Display summary
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
                Temperature = double.Parse(configBuilder["Temperature"] ?? "0.3"),
                SystemMessage = configBuilder["SystemMessage"] ?? "You are an expert QA analyzer.",
                UserTemplate = configBuilder["UserTemplate"] ?? "Analyze: {Scenario}"
            };

            Console.WriteLine($"   ✅ Model: {promptConfig.Model}");
            Console.WriteLine($"   ✅ Max Tokens: {promptConfig.MaxTokens}");
            Console.WriteLine();

            return (appConfig, promptConfig);
        }

        // ============================================================
        // METHOD 2: Read Test Case from Excel
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
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2

                    // Check if row is empty
                    if (worksheet.Cells[rowNumber, 1].Value == null ||
                        string.IsNullOrWhiteSpace(worksheet.Cells[rowNumber, 1].Value.ToString()))
                    {
                        return null;
                    }

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
        // METHOD 4: Create Output Folder
        // ============================================================
        static string CreateOutputFolder()
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"   ✅ Created output directory");
            }

            return outputDir;
        }

        // ============================================================
        // METHOD 5: Prepare Output File (Copy Input + Timestamp)
        // ============================================================
        static string PrepareOutputFile(string inputPath, string outputDir)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputFileName = $"analysis_results_{timestamp}.xlsx";
            string outputPath = Path.Combine(outputDir, outputFileName);

            // Copy input file to output location
            File.Copy(inputPath, outputPath, overwrite: true);

            Console.WriteLine($"   ✅ Output file: {outputFileName}");

            return outputPath;
        }

        // ============================================================
        // METHOD 6: Add AI Analysis Column Header
        // ============================================================
        static void AddAnalysisColumnHeader(string outputPath)
        {
            using (var package = new ExcelPackage(new FileInfo(outputPath)))
            {
                var worksheet = package.Workbook.Worksheets[1]; // Sheet2

                // Add header in column 8 (H)
                worksheet.Cells[1, 8].Value = "AI Analysis";
                worksheet.Cells[1, 8].Style.Font.Bold = true;

                package.Save();
            }
        }

        // ============================================================
        // METHOD 7: Write Analysis to Excel with Color Coding
        // ============================================================
        static void WriteAnalysisToExcel(string outputPath, int rowNumber, string analysis)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(outputPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2

                    // Write to column 8 (AI Analysis)
                    worksheet.Cells[rowNumber, 8].Value = analysis;

                    // Color coding
                    if (analysis == "GOOD")
                    {
                        worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    }
                    else if (analysis.StartsWith("Issue:"))
                    {
                        worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                    }
                    else if (analysis.StartsWith("ERROR:"))
                    {
                        worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    }

                    package.Save();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not write to Excel row {rowNumber}: {ex.Message}");
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