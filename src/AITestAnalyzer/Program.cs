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

        private const int CACHE_MAX_AGE_DAYS = 30;

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

            // STEP 1B: Validate configuration before proceeding
            bool configIsValid = await ValidateConfiguration(appConfig, promptConfig);
            if (!configIsValid)
            {
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Create AI analyzer
            var aiAnalyzer = new AIAnalyzer(appConfig, promptConfig);

            // STEP 2: Prepare output file
            WriteInfo("Preparing output file...");
            string outputDir = ExcelWriter.CreateOutputFolder();
            string outputPath = ExcelWriter.PrepareOutputFile(appConfig.ExcelPath, outputDir);

            var excelWriter = new ExcelWriter(outputPath, appConfig.WorksheetIndex);
            excelWriter.RenameOriginalSheet();
            excelWriter.AddAnalysisColumnHeader();
            Console.WriteLine();

            // STEP 3: Validate and process test cases
            int startRow = 2;  // First data row (row 1 is header)

            // Create Excel reader and validate structure
            var excelReader = new ExcelReader(appConfig.ExcelPath, appConfig.WorksheetIndex);

            WriteInfo("Validating Excel structure...");
            var (excelIsValid, validationMessage) = excelReader.ValidateExcelStructure();

            if (!excelIsValid)
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
            int totalTests = 0;
            bool useCache = true;  // Default: cache enabled

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

                // Handle clear cache command
                if (arg == "--clear-cache")
                {
                    WriteInfo("Clearing cache...");
                    var tempCache = new TestCaseCache();
                    tempCache.ClearCache();
                    WriteSuccess("Cache cleared successfully!");
                    Console.WriteLine();
                    WriteInfo("All cached analysis results have been deleted.");
                    WriteInfo("Next run will re-analyze all tests using OpenAI API.");
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

                // Check for --no-cache flag (can be anywhere in args)
                if (args.Any(a => a.ToLower() == "--no-cache"))
                {
                    useCache = false;
                    WriteWarning("Cache disabled - all tests will use OpenAI API");
                    Console.WriteLine();
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

            // NOW Initialize cache system (AFTER argument parsing)
            TestCaseCache cache = null;
            if (useCache)
            {
                WriteInfo("Initializing cache system...");
                cache = new TestCaseCache();
                int cacheSize = cache.GetCacheSize();
                int expiredCount = cache.GetExpiredCount(CACHE_MAX_AGE_DAYS);

                if (cacheSize > 0)
                {
                    WriteSuccess($"Loaded cache with {cacheSize} entries");
                    if (expiredCount > 0)
                    {
                        WriteWarning($"Found {expiredCount} expired entries (older than {CACHE_MAX_AGE_DAYS} days)");
                        WriteInfo("Expired entries will be automatically cleaned up");
                    }
                }
                else
                {
                    WriteInfo("Cache is empty (first run)");
                }
                Console.WriteLine();
            }
            else
            {
                WriteWarning("Cache is disabled for this run");
                Console.WriteLine();
            }

            Console.WriteLine();

            var startTime = DateTime.Now;
            var results = new List<(string TestId, string Result, int Tokens)>();
            int processedCount = 0;

            var progressTracker = new ProgressTracker(totalTests, startTime);

            // Track cache statistics
            int cacheHits = 0;
            int apiCalls = 0;

            for (int row = startRow; row < startRow + totalTests; row++)
            {
                TestCase testCase = excelReader.ReadTestCase(rowNumber: row);
                if (testCase == null)
                {
                    continue;
                }

                processedCount++;
                progressTracker.DisplayProgress(processedCount, testCase.TestId);

                string result;
                int tokens;

                // Check cache if enabled
                if (useCache && cache != null)
                {
                    // Generate hash for this test case
                    string hash = cache.GenerateHash(testCase);

                    // Try to get from cache
                    if (cache.TryGetCached(hash, out CachedResult cachedResult, CACHE_MAX_AGE_DAYS))
                    {
                        // CACHE HIT! Use cached result
                        result = cachedResult.AnalysisResult;
                        tokens = 0; // No tokens used (cached)
                        cacheHits++;
                    }
                    else
                    {
                        // CACHE MISS - Call OpenAI API
                        (result, tokens) = await aiAnalyzer.AnalyzeTestCase(testCase);

                        // Save to cache for next time
                        cache.AddToCache(testCase.TestId, hash, result, tokens);
                        apiCalls++;

                        // Rate limiting only for API calls
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    // Cache disabled - always call API
                    (result, tokens) = await aiAnalyzer.AnalyzeTestCase(testCase);
                    apiCalls++;
                    await Task.Delay(1000);
                }

                results.Add((testCase.TestId, result, tokens));
                excelWriter.WriteAnalysis(row, result);
            }

            var endTime = DateTime.Now;
            progressTracker.Complete();

            // Save cache to disk (if enabled)
            if (useCache && cache != null)
            {
                Console.WriteLine();
                WriteInfo("Saving cache...");

                // Clean expired entries before saving
                int cleaned = cache.CleanExpiredEntries(CACHE_MAX_AGE_DAYS);
                if (cleaned > 0)
                {
                    WriteInfo($"Cleaned {cleaned} expired cache entries");
                }

                cache.SaveCache();
                WriteSuccess("Cache saved successfully");
            }

            // STEP 4: Create Quality Issues Sheet
            Console.WriteLine();
            WriteInfo("Creating Quality Issues Summary...");
            excelWriter.CreateQualityIssuesSheet(results);

            // STEP 5: Create Statistics Dashboard
            WriteInfo("Creating Statistics Dashboard...");
            excelWriter.CreateStatisticsDashboard(results, startTime, endTime);

            // STEP 6: Display summary
            Console.WriteLine();
            SummaryDisplay.Display(results, startTime, endTime, outputPath, cacheHits, apiCalls, useCache);

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
        // METHOD 1B: Validate Configuration
        // ============================================================
        static async Task<bool> ValidateConfiguration(Configuration appConfig, PromptConfig promptConfig)
        {
            WriteInfo("Validating configuration...");
            Console.WriteLine();

            var validator = new ConfigurationValidator(appConfig, promptConfig);

            // Run all validations
            var (isValid, errorMessage) = await validator.ValidateAll();

            if (!isValid)
            {
                Console.WriteLine();
                WriteError("CONFIGURATION ERROR:");
                WriteError(errorMessage);
                Console.WriteLine();
                WriteInfo("Please fix the configuration and try again.");
                Console.WriteLine();
                WriteInfo("Need help? Run: dotnet run -- --help");
                return false;
            }

            // All checks passed - show success messages
            WriteSuccess("API key format valid");
            WriteSuccess("Excel file exists and is accessible");

            // Get worksheet name for display
            var worksheetResult = validator.ValidateWorksheetIndex();
            if (worksheetResult.IsValid && !string.IsNullOrEmpty(worksheetResult.DetailedInfo))
            {
                WriteSuccess(worksheetResult.DetailedInfo);
            }

            WriteSuccess("OpenAI API connection successful");
            Console.WriteLine();

            return true;
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
            Console.WriteLine("  --no-cache                    Disable cache (force fresh analysis)");
            Console.WriteLine("  --clear-cache                 Clear all cached results");
            Console.WriteLine();

            WriteInfo("EXAMPLES:");
            Console.WriteLine("  dotnet run -- 5               # Analyze first 5 test cases");
            Console.WriteLine("  dotnet run -- --all           # Analyze all test cases");
            Console.WriteLine("  dotnet run                    # Interactive: prompts for test count");
            Console.WriteLine("  dotnet run -- 10 --no-cache   # Analyze 10 tests without cache");
            Console.WriteLine("  dotnet run -- --clear-cache   # Clear cache and exit");
            Console.WriteLine();

            WriteInfo("CACHE SYSTEM:");
            Console.WriteLine("  - Analysis results are cached to save API costs");
            Console.WriteLine("  - Cache expires after 30 days automatically");
            Console.WriteLine("  - Unchanged tests use cached results (instant + free!)");
            Console.WriteLine("  - Changed tests are automatically re-analyzed");
            Console.WriteLine("  - Use --no-cache to force fresh analysis");
            Console.WriteLine("  - Use --clear-cache to reset all cached data");
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