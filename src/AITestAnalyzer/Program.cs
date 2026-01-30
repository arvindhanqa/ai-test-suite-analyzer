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
        private const string Version = "1.0.0";
        private const string AppName = "AI Test Suite Analyzer";
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

            // STEP 3B: Parse command-line arguments
            if (args.Length > 0)
            {
                string arg = args[0].ToLower();

                // Handle help flag
                if (arg == "--help" || arg == "-h")
                {
                    DisplayHelp();
                    return;
                }

                // Handle version flag
                if (arg == "--version" || arg == "-v")
                {
                    DisplayVersion();
                    return;
                }

                // Handle --all flag
                if (arg == "--all" || arg == "-a")
                {
                    totalTests = totalRowsInExcel;
                    WriteInfo($"Analyzing all {totalTests} tests (--all flag)");
                }
                // Handle numeric argument
                else if (int.TryParse(arg, out int requestedCount))
                {
                    if (requestedCount <= 0)
                    {
                        WriteError("Test count must be greater than 0");
                        return;
                    }

                    if (requestedCount > totalRowsInExcel)
                    {
                        WriteWarning($"Requested {requestedCount} tests but only {totalRowsInExcel} available");
                        totalTests = totalRowsInExcel;
                        WriteInfo($"Analyzing all {totalTests} tests instead");
                    }
                    else
                    {
                        totalTests = requestedCount;
                        WriteInfo($"Analyzing {totalTests} tests (from command-line)");
                    }
                }
                // Handle unknown argument
                else
                {
                    WriteError($"Unknown argument: {arg}");
                    Console.WriteLine();
                    WriteInfo("Use --help to see available options");
                    return;
                }
            }
            else
            {
                // Interactive mode (no arguments provided)
                WriteSuccess($"Found {totalRowsInExcel} test cases in Excel.");
                Console.Write("   How many tests to analyze? (Enter number or press Enter for all): ");

                string userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    totalTests = totalRowsInExcel;
                    WriteInfo($"Analyzing all {totalTests} tests");
                }
                else if (int.TryParse(userInput, out int userCount))
                {
                    if (userCount <= 0)
                    {
                        WriteError("Invalid input. Must be greater than 0");
                        return;
                    }

                    if (userCount > totalRowsInExcel)
                    {
                        WriteWarning($"Requested {userCount} tests but only {totalRowsInExcel} available");
                        totalTests = totalRowsInExcel;
                    }
                    else
                    {
                        totalTests = userCount;
                    }

                    WriteInfo($"Analyzing {totalTests} tests");
                }
                else
                {
                    WriteError("Invalid input. Please enter a number");
                    return;
                }
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
        // METHOD 2: Display Help Text
        // ============================================================
        static void DisplayHelp()
        {
            WriteHeader($"{AppName} v{Version}");
            Console.WriteLine();
            WriteInfo("USAGE:");
            Console.WriteLine("  dotnet run                    # Interactive mode (prompts for test count)");
            Console.WriteLine("  dotnet run -- <number>        # Analyze specific number of tests");
            Console.WriteLine("  dotnet run -- --all           # Analyze all tests without prompting");
            Console.WriteLine("  dotnet run -- --help          # Show this help message");
            Console.WriteLine("  dotnet run -- --version       # Show version information");
            Console.WriteLine();
            WriteInfo("OPTIONS:");
            Console.WriteLine("  --help, -h                    Show this help message");
            Console.WriteLine("  --version, -v                 Show version information");
            Console.WriteLine("  --all, -a                     Analyze all tests without prompting");
            Console.WriteLine();
            WriteInfo("EXAMPLES:");
            Console.WriteLine("  dotnet run -- 5               # Analyze first 5 test cases");
            Console.WriteLine("  dotnet run -- --all           # Analyze all test cases");
            Console.WriteLine("  dotnet run                    # Interactive: prompts for test count");
            Console.WriteLine();
            WriteInfo("CONFIGURATION:");
            Console.WriteLine("  Edit appsettings.json to configure:");
            Console.WriteLine("    - OpenAI API key");
            Console.WriteLine("    - Excel file path");
            Console.WriteLine("    - Worksheet index");
            Console.WriteLine();
        }

        // ============================================================
        // METHOD 3: Display Version
        // ============================================================
        static void DisplayVersion()
        {
            WriteSuccess($"{AppName} v{Version}");
            Console.WriteLine("Copyright (c) 2026 Aravindhan Rajasekaran");
            Console.WriteLine("Licensed under MIT License");
            Console.WriteLine();
            WriteInfo("GitHub: https://github.com/arvindhanqa/ai-test-suite-analyzer");
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