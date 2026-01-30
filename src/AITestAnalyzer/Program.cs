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

            WriteHeader("===============================================");
            WriteHeader("AI Test Suite Analyzer - Week 1");
            WriteHeader("===============================================");
            Console.WriteLine();

            // STEP 1: Load configurations
            var (appConfig, promptConfig) = LoadConfiguration();
            if (appConfig == null || promptConfig == null) return;

            // Create AI analyzer
            var aiAnalyzer = new AIAnalyzer(appConfig, promptConfig);

            // STEP 2: Prepare output file
            WriteInfo("Preparing output file...");
            string outputDir = ExcelWriter.CreateOutputFolder();
            string outputPath = ExcelWriter.PrepareOutputFile(appConfig.ExcelPath, outputDir);

            var excelWriter = new ExcelWriter(outputPath, appConfig.WorksheetIndex);// Need to use outputPath here
            excelWriter.RenameOriginalSheet();  
            excelWriter.AddAnalysisColumnHeader();
            Console.WriteLine();

            // STEP 3: Validate and process test cases
            int startRow = 2;  // First data row (row 1 is header)
            int totalTests;

            // Create Excel reader and validate structure
            var excelReader = new ExcelReader(appConfig.ExcelPath, appConfig.WorksheetIndex);

            WriteInfo("Validating Excel structure...");
            var (isValid, validationMessage) = excelReader.ValidateExcelStructure();

            if (!isValid)
            {
                WriteError($"VALIDATION ERROR: {validationMessage}");
                WriteError("Please check your Excel file and try again.");
                Console.WriteLine();
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            WriteSuccess($"{validationMessage}");
            Console.WriteLine();

            // Count actual rows in Excel
            int totalRowsInExcel = excelReader.CountTestRows();

            if (totalRowsInExcel == 0)
            {
                WriteError("ERROR: No test cases found in Excel file");
                return;
            }

            // Check if command-line argument provided
            if (args.Length > 0 && int.TryParse(args[0], out int argTests))
            {
                // Command-line argument provided
                if (argTests > totalRowsInExcel)
                {
                    WriteWarning($"WARNING: Requested {argTests} tests, but only {totalRowsInExcel} exist in Excel");
                    WriteInfo($"   Analyzing all {totalRowsInExcel} tests instead...");
                    totalTests = totalRowsInExcel;
                }
                else
                {
                    totalTests = argTests;
                    WriteInfo($"Analyzing {totalTests} of {totalRowsInExcel} test cases (from command line)...");
                }
            }
            else
            {
                // No command-line argument - ASK USER
                WriteSuccess($"Found {totalRowsInExcel} test cases in Excel.");
                Console.WriteLine($"   How many tests to analyze? (Enter number or press Enter for all): ");

                string userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    // User pressed Enter - process all
                    totalTests = totalRowsInExcel;
                    WriteInfo($"   → Analyzing all {totalTests} test cases...");
                }
                else if (int.TryParse(userInput, out int userTests) && userTests > 0)
                {
                    // User entered a number
                    if (userTests > totalRowsInExcel)
                    {
                        WriteWarning($"Requested {userTests} tests, but only {totalRowsInExcel} exist.");
                        WriteInfo($"  → Analyzing all {totalRowsInExcel} tests instead...");
                        totalTests = totalRowsInExcel;
                    }
                    else
                    {
                        totalTests = userTests;
                        WriteInfo($"   → Analyzing {totalTests} of {totalRowsInExcel} test cases...");
                    }
                }
                else
                {
                    WriteError("Invalid input. Please enter a positive number.");
                    return;
                }
            }

            // Validate
            if (totalTests < 1)
            {
                WriteError("ERROR: Test count must be at least 1");
                return;
            }

            Console.WriteLine();

            var startTime = DateTime.Now;
            var results = new List<(string TestId, string Result, int Tokens)>();
            int processedCount = 0;

            var progressTracker = new ProgressTracker(totalTests, startTime);

            for (int row = startRow; row < startRow + totalTests; row++)
            {
                TestCase testCase = excelReader.ReadTestCase(rowNumber: row);
                if (testCase == null)
                {
                    continue; // Silently skip empty rows
                }

                processedCount++;
                progressTracker.DisplayProgress(processedCount, testCase.TestId);

                var (result, tokens) = await aiAnalyzer.AnalyzeTestCase(testCase);
                results.Add((testCase.TestId, result, tokens));

                // Write to Excel immediately
                excelWriter.WriteAnalysis( row, result);

                await Task.Delay(1000);  // Rate limiting
            }

            var endTime = DateTime.Now;
            progressTracker.Complete();

            // STEP 4: Create Quality Issues Sheet
            Console.WriteLine();
            WriteInfo("Creating Quality Issues Summary...");
            excelWriter.CreateQualityIssuesSheet(results);

            // STEP 5: Create Statistics Dashboard
            WriteInfo("Creating Statistics Dashboard...");
            excelWriter.CreateStatisticsDashboard(results, startTime, endTime);

            // STEP 6: Display summary
            Console.WriteLine();
            SummaryDisplay.Display(results, startTime, endTime, outputPath);

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // METHOD 1: Load Configuration
        // ============================================================
        static (Configuration appConfig, PromptConfig promptConfig) LoadConfiguration()
        {
            WriteInfo("Loading configuration...");

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
                WriteError("ERROR: OpenAI API key not configured!");
                WriteError("Please update appsettings.json with your actual API key.");
                return (null, null);
            }

            var appConfig = new Configuration
            {
                ApiKey = apiKey,
                Model = model,
                ExcelPath = excelPath,
                WorksheetIndex = int.Parse(configBuilder["Excel:WorksheetIndex"] ?? "0")
            };

            var promptConfig = new PromptConfig
            {
                MaxTokens = int.Parse(configBuilder["MaxTokens"] ?? "150"),
                Model = configBuilder["Model"] ?? "gpt-4o-mini",
                Temperature = double.Parse(configBuilder["Temperature"] ?? "0.2"),
                SystemMessage = configBuilder["SystemMessage"] ?? "You are an expert QA analyzer.",
                UserTemplate = configBuilder["UserTemplate"] ?? "Analyze: {Scenario}"
            };

            WriteSuccess($"Model: {promptConfig.Model}");
            WriteSuccess($"Max Tokens: {promptConfig.MaxTokens}");
            Console.WriteLine();

            return (appConfig, promptConfig);
        }

        // ============================================================
        // COLOR HELPER METHODS
        // ============================================================
        static void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✅ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("⚠️  ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("❌ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void WriteInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("📊 ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void WriteHeader(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ResetColor();
        }

    }
}