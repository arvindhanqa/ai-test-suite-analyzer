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
            RenameOriginalSheet(outputPath);  // ← ADD THIS LINE
            AddAnalysisColumnHeader(outputPath);
            Console.WriteLine();

            // STEP 3: Process test cases
            int startRow = 2;  // First data row (row 1 is header)
            int totalTests = 56; // Process all tests
            Console.WriteLine($"📊 Analyzing {totalTests} test cases...");
            Console.WriteLine();

            var startTime = DateTime.Now;
            var results = new List<(string TestId, string Result, int Tokens)>();
            int processedCount = 0;

            for (int row = startRow; row <= totalTests + 1; row++)
            {
                TestCase testCase = ReadTestCaseFromExcel(appConfig.ExcelPath, rowNumber: row);
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
                WriteAnalysisToExcel(outputPath, row, result);

                await Task.Delay(1000);  // Rate limiting
            }

            var endTime = DateTime.Now;
            Console.WriteLine(); // New line after progress bar
            Console.WriteLine("   ✅ Analysis complete!");
            Console.WriteLine();

            // STEP 4: Create Quality Issues Sheet
            Console.WriteLine();
            Console.WriteLine("📋 Creating Quality Issues Summary...");
            CreateQualityIssuesSheet(outputPath, results);

            // STEP 5: Create Statistics Dashboard
            Console.WriteLine("📊 Creating Statistics Dashboard...");
            CreateStatisticsDashboard(outputPath, results, startTime, endTime);

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

                    worksheet.Cells[rowNumber, 8].Style.WrapText = true;
                    worksheet.Cells[rowNumber, 8].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                    worksheet.Cells[rowNumber, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Column(8).Width = 50;  // Set AI Analysis column to 50 characters wide

                    // In AddAnalysisColumnHeader method, add:
                    worksheet.Cells[1, 8].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    worksheet.Cells[1, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);

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

        // ============================================================
        // METHOD 9: Create Quality Issues Summary Sheet
        // ============================================================
        static void CreateQualityIssuesSheet(string outputPath, List<(string TestId, string Result, int Tokens)> results)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(outputPath)))
                {
                    // Create new worksheet
                    var issuesSheet = package.Workbook.Worksheets.Add("Quality Issues Summary");

                    // HEADERS
                    issuesSheet.Cells[1, 1].Value = "Test ID";
                    issuesSheet.Cells[1, 2].Value = "Issue Found";
                    issuesSheet.Cells[1, 3].Value = "Status";

                    // Format headers
                    using (var headerRange = issuesSheet.Cells[1, 1, 1, 3])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        headerRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);
                    }

                    // DATA ROWS - only tests with issues
                    int currentRow = 2;
                    foreach (var (testId, result, tokens) in results)
                    {
                        if (result != "GOOD" && !result.StartsWith("ERROR:"))
                        {
                            issuesSheet.Cells[currentRow, 1].Value = testId;
                            issuesSheet.Cells[currentRow, 2].Value = result.Replace("Issue: ", "");
                            issuesSheet.Cells[currentRow, 3].Value = "Needs Review";

                            // Format issue row
                            issuesSheet.Cells[currentRow, 2].Style.WrapText = true;
                            issuesSheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.Orange);

                            currentRow++;
                        }
                    }

                    // Summary at the bottom
                    currentRow++; // Empty row
                    issuesSheet.Cells[currentRow, 1].Value = "TOTAL ISSUES:";
                    issuesSheet.Cells[currentRow, 2].Value = currentRow - 3; // Subtract header + empty row
                    issuesSheet.Cells[currentRow, 1].Style.Font.Bold = true;

                    // Auto-fit columns
                    issuesSheet.Column(1).Width = 15;
                    issuesSheet.Column(2).Width = 60;
                    issuesSheet.Column(3).Width = 15;

                    package.Save();
                    Console.WriteLine("   ✅ Created 'Quality Issues Summary' sheet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not create issues sheet: {ex.Message}");
            }
        }

        // ============================================================
        // METHOD 10: Create Statistics Dashboard Sheet
        // ============================================================
        static void CreateStatisticsDashboard(string outputPath, List<(string TestId, string Result, int Tokens)> results, DateTime startTime, DateTime endTime)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(outputPath)))
                {
                    var statsSheet = package.Workbook.Worksheets.Add("Statistics Dashboard");

                    // Calculate metrics
                    int totalTests = results.Count;
                    int goodTests = results.Count(r => r.Result == "GOOD");
                    int issueTests = results.Count(r => r.Result != "GOOD" && !r.Result.StartsWith("ERROR:"));
                    int errorTests = results.Count(r => r.Result.StartsWith("ERROR:"));
                    int totalTokens = results.Sum(r => r.Tokens);
                    double totalCost = totalTokens * 0.00000015;
                    int avgTokens = totalTests > 0 ? totalTokens / totalTests : 0;
                    double timeTaken = (endTime - startTime).TotalSeconds;
                    double qualityScore = totalTests > 0 ? (goodTests * 100.0 / totalTests) : 0;

                    // TITLE
                    statsSheet.Cells[1, 1].Value = "AI TEST SUITE ANALYZER - STATISTICS DASHBOARD";
                    statsSheet.Cells[1, 1, 1, 4].Merge = true;
                    statsSheet.Cells[1, 1].Style.Font.Size = 16;
                    statsSheet.Cells[1, 1].Style.Font.Bold = true;
                    statsSheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    statsSheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkBlue);
                    statsSheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);

                    int row = 3;

                    // SECTION 1: QUALITY OVERVIEW
                    statsSheet.Cells[row, 1].Value = "QUALITY OVERVIEW";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Overall Quality Score:";
                    statsSheet.Cells[row, 2].Value = $"{qualityScore:F1}%";
                    statsSheet.Cells[row, 2].Style.Font.Bold = true;
                    statsSheet.Cells[row, 2].Style.Font.Size = 12;
                    if (qualityScore >= 80)
                        statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    else if (qualityScore >= 50)
                        statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                    else
                        statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Total Tests Analyzed:";
                    statsSheet.Cells[row, 2].Value = totalTests;
                    row++;

                    row++; // Empty row

                    // SECTION 2: TEST BREAKDOWN
                    statsSheet.Cells[row, 1].Value = "TEST BREAKDOWN";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    // Table headers
                    statsSheet.Cells[row, 1].Value = "Category";
                    statsSheet.Cells[row, 2].Value = "Count";
                    statsSheet.Cells[row, 3].Value = "Percentage";
                    statsSheet.Cells[row, 1, row, 3].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1, row, 3].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1, row, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    row++;

                    // Good tests
                    statsSheet.Cells[row, 1].Value = "✅ Good Quality Tests";
                    statsSheet.Cells[row, 2].Value = goodTests;
                    statsSheet.Cells[row, 3].Value = $"{(totalTests > 0 ? goodTests * 100.0 / totalTests : 0):F1}%";
                    statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    row++;

                    // Tests with issues
                    statsSheet.Cells[row, 1].Value = "⚠️ Tests with Issues";
                    statsSheet.Cells[row, 2].Value = issueTests;
                    statsSheet.Cells[row, 3].Value = $"{(totalTests > 0 ? issueTests * 100.0 / totalTests : 0):F1}%";
                    statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                    row++;

                    // Errors
                    if (errorTests > 0)
                    {
                        statsSheet.Cells[row, 1].Value = "❌ Analysis Errors";
                        statsSheet.Cells[row, 2].Value = errorTests;
                        statsSheet.Cells[row, 3].Value = $"{(totalTests > 0 ? errorTests * 100.0 / totalTests : 0):F1}%";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                        row++;
                    }

                    row++; // Empty row

                    // SECTION 3: COST & PERFORMANCE
                    statsSheet.Cells[row, 1].Value = "COST & PERFORMANCE METRICS";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Total Tokens Used:";
                    statsSheet.Cells[row, 2].Value = $"{totalTokens:N0}";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Average Tokens/Test:";
                    statsSheet.Cells[row, 2].Value = avgTokens;
                    row++;

                    statsSheet.Cells[row, 1].Value = "Total Cost:";
                    statsSheet.Cells[row, 2].Value = $"${totalCost:F6}";
                    statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Average Cost/Test:";
                    statsSheet.Cells[row, 2].Value = $"${(totalTests > 0 ? totalCost / totalTests : 0):F6}";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Analysis Time:";
                    statsSheet.Cells[row, 2].Value = $"{timeTaken:F1} seconds";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Average Time/Test:";
                    statsSheet.Cells[row, 2].Value = $"{(totalTests > 0 ? timeTaken / totalTests : 0):F2} seconds";
                    row++;

                    row++; // Empty row

                    // SECTION 4: RECOMMENDATIONS
                    statsSheet.Cells[row, 1].Value = "RECOMMENDATIONS";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    if (qualityScore < 50)
                    {
                        statsSheet.Cells[row, 1].Value = "❌ CRITICAL: Less than 50% of tests meet quality standards.";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                        row++;
                        statsSheet.Cells[row, 1].Value = "   Action: Review 'Quality Issues Summary' sheet and prioritize fixes.";
                        row++;
                    }
                    else if (qualityScore < 80)
                    {
                        statsSheet.Cells[row, 1].Value = "⚠️ MODERATE: Test suite quality needs improvement.";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                        row++;
                        statsSheet.Cells[row, 1].Value = "   Action: Address issues in 'Quality Issues Summary' sheet.";
                        row++;
                    }
                    else
                    {
                        statsSheet.Cells[row, 1].Value = "✅ EXCELLENT: Test suite quality is high!";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                        row++;
                        statsSheet.Cells[row, 1].Value = "   Action: Maintain quality standards and address remaining issues.";
                        row++;
                    }

                    row++;

                    // Budget projection
                    statsSheet.Cells[row, 1].Value = "💰 BUDGET PROJECTION";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    int testsPerDollar = totalCost > 0 ? (int)(1.0 / (totalCost / totalTests)) : 0;
                    statsSheet.Cells[row, 1].Value = "Tests you can analyze with $10:";
                    statsSheet.Cells[row, 2].Value = $"{testsPerDollar * 10:N0}";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Cost to analyze 500 tests:";
                    statsSheet.Cells[row, 2].Value = $"${(totalTests > 0 ? (totalCost / totalTests) * 500 : 0):F4}";

                    // Column widths
                    statsSheet.Column(1).Width = 40;
                    statsSheet.Column(2).Width = 20;
                    statsSheet.Column(3).Width = 15;

                    // Add borders to all used cells
                    var usedRange = statsSheet.Cells[1, 1, row, 3];
                    usedRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);

                    package.Save();
                    Console.WriteLine("   ✅ Created 'Statistics Dashboard' sheet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Warning: Could not create statistics sheet: {ex.Message}");
            }
        }

        // ============================================================
        // METHOD 11: Rename Original Sheet to "AI Detailed Analysis"
        // ============================================================
        static void RenameOriginalSheet(string outputPath)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(outputPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2 (index 1)
                    worksheet.Name = "AI Detailed Analysis";
                    package.Save();
                    Console.WriteLine("   ✅ Renamed sheet to 'AI Detailed Analysis'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not rename sheet: {ex.Message}");
            }
        }
    }
}