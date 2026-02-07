using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using OpenAI;
using OpenAI.Managers;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using static AITestAnalyzer.FileSelector;
using static OpenAI.ObjectModels.StaticValues.ImageStatics;

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

            // ============================================================
            // EARLY EXIT FLAGS — these don't need FileSelector at all
            // ============================================================
            if (args.Length > 0)
            {
                string firstArg = args[0].ToLower();

                // For requirement extraction testing
                if (firstArg == "--test-requirements")
                {
                    await TestRequirementExtraction();
                    return;
                }

                if (firstArg == "--help" || firstArg == "-h")
                {
                    DisplayHelp();
                    return;
                }
                if (firstArg == "--version" || firstArg == "-v")
                {
                    DisplayVersion();
                    return;
                }
                if (firstArg == "--clear-cache")
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
            }

            // Check for --no-cache anywhere in args (still supported as CLI override)
            bool useCache = !args.Any(a => a.ToLower() == "--no-cache");

            WriteHeader("===============================================");
            WriteHeader("AI Test Suite Analyzer - Week 1");
            WriteHeader("===============================================");
            Console.WriteLine();

            // ============================================================
            // STEP 1: Load config (API key + prompt settings only)
            // ============================================================
            var (appConfig, promptConfig) = LoadConfiguration();
            if (appConfig == null || promptConfig == null) return;

            // ============================================================
            // STEP 2: FileSelector — user picks file, mode, sheet, limit
            // ============================================================
            var selection = FileSelector.ShowMainMenu();

            // User quit out of the menu
            if (selection == null || selection.SelectedMode == FileSelector.SelectionResult.Mode.Exit)
            {
                WriteInfo("Exited.");
                return;
            }

            // ============================================================
            // STEP 3: Route to batch or single based on selection
            // ============================================================
            if (selection.SelectedMode == FileSelector.SelectionResult.Mode.Batch)
            {
                await RunBatchMode(appConfig, promptConfig, selection);
            }
            else
            {
                await RunSingleMode(appConfig, promptConfig, selection, useCache);
            }
        }

        // ============================================================
        // TEST REQUIREMENT EXTRACTION (Day 15 Feature)
        // ============================================================
        static async Task TestRequirementExtraction()
        {
            Console.Clear();
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            WriteHeader("   🧪 REQUIREMENT EXTRACTION TEST - DAY 15");
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Load configuration inline
            WriteInfo("Loading configuration...");

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("PromptConfig.json", optional: false, reloadOnChange: true)
                .Build();

            string? apiKey = configBuilder["OpenAI:ApiKey"];
            string model = configBuilder["OpenAI:Model"] ?? "gpt-4o-mini";

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR-ACTUAL-API-KEY-HERE")
            {
                WriteError("ERROR: OpenAI API key not configured!");
                WriteError("Please update appsettings.json with your actual API key.");
                Console.WriteLine();
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            var appConfig = new Configuration { ApiKey = apiKey, Model = model };
            var promptConfig = new PromptConfig
            {
                MaxTokens = 2000,
                Model = model,
                Temperature = 0,
                SystemMessage = "You are a requirement analysis expert.",
                UserTemplate = ""
            };

            WriteSuccess($"Model: {model}");
            WriteSuccess($"Max Tokens: 2000 (requirement extraction)");
            WriteSuccess($"Temperature: 0 (deterministic)");
            Console.WriteLine();

            // Validate API
            WriteInfo("Validating API connection...");
            var validator = new ConfigurationValidator(appConfig, promptConfig);

            var apiKeyResult = validator.ValidateApiKey();
            if (!apiKeyResult.IsValid)
            {
                WriteError($"API Key Error: {apiKeyResult.ErrorMessage}");
                Console.WriteLine();
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            var connectionResult = await validator.ValidateOpenAIConnection();
            if (!connectionResult.IsValid)
            {
                WriteError($"OpenAI Connection Error: {connectionResult.ErrorMessage}");
                Console.WriteLine();
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            WriteSuccess("API connection validated");
            Console.WriteLine();

            // Initialize extractor AND cache
            var extractor = new RequirementExtractor(appConfig, promptConfig);
            var cache = new RequirementCache();

            // Show cache stats
            WriteInfo("Checking cache...");
            var (cacheCount, totalReqs, totalTokens) = cache.GetStats();
            if (cacheCount > 0)
            {
                WriteSuccess($"Cache: {cacheCount} documents, {totalReqs} requirements, {totalTokens} tokens saved");
            }
            else
            {
                WriteInfo("Cache: Empty (first run)");
            }
            Console.WriteLine();

            // File path
            string requirementFile = @"C:\Projects\ai-test-analyzer\ai-test-suite-analyzer\data\requirements_taskflow.md";

            WriteInfo($"Looking for: {requirementFile}");

            if (!File.Exists(requirementFile))
            {
                WriteError($"File not found: {requirementFile}");
                Console.WriteLine();
                WriteWarning("Please update the path in Program.cs TestRequirementExtraction() method");
                Console.WriteLine();
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Show file info
            var fileInfo = new FileInfo(requirementFile);
            WriteSuccess($"File: {fileInfo.Name}");
            WriteSuccess($"Size: {fileInfo.Length:N0} bytes");
            WriteSuccess($"Modified: {fileInfo.LastWriteTime}");
            Console.WriteLine();

            // Extract with caching!
            WriteInfo("Extracting requirements (checking cache first)...");
            var requirements = await extractor.ExtractRequirements(requirementFile, cache, maxAgeDays: 30);

            // Display results
            Console.WriteLine();
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            WriteHeader("   EXTRACTED REQUIREMENTS");
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            if (requirements.Count == 0)
            {
                WriteWarning("No requirements extracted. Check AI response format.");
                Console.WriteLine();
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Group and display
            var groupedByTopic = requirements.GroupBy(r => r.Topic).OrderBy(g => g.Key);
            int count = 1;

            foreach (var topicGroup in groupedByTopic)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"📁 {topicGroup.Key}");
                Console.ResetColor();

                foreach (var req in topicGroup)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"   {count}. ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(req.Subtopic);
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"      → {req.ExpectedAction}");
                    Console.ResetColor();

                    count++;
                }
                Console.WriteLine();
            }

            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            WriteSuccess($"Total Requirements Extracted: {requirements.Count}");

            Console.WriteLine();
            WriteInfo("Distribution by Topic:");
            foreach (var topicGroup in groupedByTopic)
            {
                Console.WriteLine($"   • {topicGroup.Key}: {topicGroup.Count()} requirements");
            }

            // Show final cache stats
            Console.WriteLine();
            var (finalCount, finalReqs, finalTokens) = cache.GetStats();
            WriteInfo($"Cache updated: {finalCount} documents, {finalReqs} requirements total");

            Console.WriteLine();
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }
        // ============================================================
        // SINGLE FILE MODE
        // ============================================================
        static async Task RunSingleMode(Configuration appConfig, PromptConfig promptConfig, SelectionResult selection, bool useCache)
        {
            string excelPath = selection.FilePath;
            int worksheetIndex = selection.SheetIndex;
            int testLimit = selection.TestLimit; // 0 = all

            // Validate (API key + worksheet index against the chosen file)
            bool configIsValid = await ValidateConfiguration(appConfig, promptConfig, excelPath, worksheetIndex);
            if (!configIsValid)
            {
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            var aiAnalyzer = new AIAnalyzer(appConfig, promptConfig);
            // After creating AIAnalyzer, add this:
            Console.WriteLine();
            WriteInfo("Loading requirements for context-aware analysis...");

            // Initialize requirement cache and extractor
            var reqCache = new RequirementCache();
            var reqExtractor = new RequirementExtractor(appConfig, promptConfig);

            // Auto-detect requirement file based on test file name
            string testFileName = Path.GetFileNameWithoutExtension(excelPath);
            string reqFileName = testFileName.Replace("test_cases_", "requirements_") + ".md";
            string dataFolder = Path.GetDirectoryName(excelPath) ?? ".";
            string reqPath = Path.Combine(dataFolder, reqFileName);

            if (!File.Exists(reqPath))
            {
                Console.WriteLine($"⚠️  Auto-detection failed. Could not find: {reqFileName}");
                Console.Write("📁 Enter requirement file path (or press Enter to skip): ");
                string? userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    Console.WriteLine("⚠️  No requirements provided. Analysis will be quality-only (no coverage tracking).");
                    reqPath = ""; // Empty path = skip requirements
                }
                else
                {
                    reqPath = userInput;
                }
            }
            else
            {
                Console.WriteLine($"✅ Auto-detected requirement file: {Path.GetFileName(reqPath)}");
            }

            string reqFile = reqPath; // Keep variable name consistent with existing code

            List<ExtractedRequirement> requirements;
            try
            {
                requirements = await reqExtractor.ExtractRequirements(reqFile, reqCache);
                WriteSuccess($"Loaded {requirements.Count} requirements as context");
            }
            catch (Exception ex)
            {
                WriteError($"Failed to load requirements: {ex.Message}");
                requirements = new List<ExtractedRequirement>(); // Continue with empty list
            }

            Console.WriteLine();

            // Prepare output file
            WriteInfo("Preparing output file...");
            string outputDir = ExcelWriter.CreateOutputFolder();
            string outputPath = ExcelWriter.PrepareOutputFile(excelPath, outputDir);

            var excelWriter = new ExcelWriter(outputPath, worksheetIndex);
            excelWriter.RenameOriginalSheet();
            excelWriter.AddAnalysisColumnHeader();
            Console.WriteLine();

            // Validate Excel structure
            var excelReader = new ExcelReader(excelPath, worksheetIndex);

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

            // Count rows and resolve test limit
            int totalRowsInExcel = excelReader.CountTestRows();

            if (totalRowsInExcel == 0)
            {
                WriteError("ERROR: No test cases found in Excel file");
                return;
            }

            // testLimit == 0 means "all" from FileSelector
            int totalTests = (testLimit == 0 || testLimit > totalRowsInExcel)
                ? totalRowsInExcel
                : testLimit;

            WriteInfo($"Analyzing {totalTests} of {totalRowsInExcel} tests");
            Console.WriteLine();

            // Initialize cache
            TestCaseCache? cache = null;
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
                WriteWarning("Cache is disabled for this run (--no-cache)");
                Console.WriteLine();
            }

            // Process tests
            var startTime = DateTime.Now;
            var results = new List<(string TestId, string Result, int Tokens)>();
            int processedCount = 0;
            int cacheHits = 0;
            int apiCalls = 0;

            var progressTracker = new ProgressTracker(totalTests, startTime);
            int startRow = 2;

            for (int row = startRow; row < startRow + totalTests; row++)
            {
                TestCase? testCase = excelReader.ReadTestCase(rowNumber: row);
                if (testCase == null)
                    continue;

                processedCount++;
                progressTracker.DisplayProgress(processedCount, testCase.TestId);

                string result;
                string quality;     // Declare here at method level
                string coverage;    // Declare here at method level
                int tokens;         // Declare here at method level

                if (useCache && cache != null)
                {
                    string hash = cache.GenerateHash(testCase);

                    if (cache.TryGetCached(hash, out CachedResult? cachedResult, CACHE_MAX_AGE_DAYS))
                    {
                        // Cache hit - use cached result
                        result = cachedResult!.AnalysisResult;
                        quality = result;  // For now, treat cached result as quality
                        coverage = "None"; // No coverage in cache yet
                        tokens = 0;
                        cacheHits++;
                    }
                    else
                    {
                        // Cache miss - call AI
                        (quality, coverage, tokens) = await aiAnalyzer.AnalyzeTestCase(testCase, requirements);
                        result = quality;  // Store quality as result for now
                        cache.AddToCache(testCase.TestId, hash, result, tokens);
                        apiCalls++;
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    // No cache - call AI directly
                    (quality, coverage, tokens) = await aiAnalyzer.AnalyzeTestCase(testCase, requirements);
                    result = quality;  // Store quality as result for now
                    apiCalls++;
                    await Task.Delay(1000);
                }

                results.Add((testCase.TestId, result, tokens));
                excelWriter.WriteAnalysis(row, result);
            }

            var endTime = DateTime.Now;
            progressTracker.Complete();

            // Save cache
            if (useCache && cache != null)
            {
                Console.WriteLine();
                WriteInfo("Saving cache...");
                int cleaned = cache.CleanExpiredEntries(CACHE_MAX_AGE_DAYS);
                if (cleaned > 0) WriteInfo($"Cleaned {cleaned} expired cache entries");
                cache.SaveCache();
                WriteSuccess("Cache saved successfully");
            }

            // Create summary sheets
            Console.WriteLine();
            WriteInfo("Creating Quality Issues Summary...");
            excelWriter.CreateQualityIssuesSheet(results);

            WriteInfo("Creating Statistics Dashboard...");
            excelWriter.CreateStatisticsDashboard(results, startTime, endTime);

            // Display summary
            Console.WriteLine();
            SummaryDisplay.Display(results, startTime, endTime, outputPath, cacheHits, apiCalls, useCache);

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // BATCH MODE — receives everything from FileSelector.
        // No arg parsing here. BatchProcessor gets what it needs directly.
        // ============================================================
        static async Task RunBatchMode(Configuration appConfig, PromptConfig promptConfig, SelectionResult selection)
        {
            string folderPath = selection.FolderPath;
            int worksheetIndex = selection.SheetIndex;
            int testLimit = selection.TestLimit;
            bool useCache = true;

            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            WriteHeader("   AI TEST SUITE ANALYZER - BATCH MODE");
            WriteHeader("═══════════════════════════════════════════════════════════════════════\n");

            // Validate API key only (individual files validated inside BatchProcessor)
            var validator = new ConfigurationValidator(appConfig, promptConfig);
            var apiKeyResult = validator.ValidateApiKey();
            if (!apiKeyResult.IsValid)
            {
                WriteError($"API Key Error: {apiKeyResult.ErrorMessage}");
                return;
            }

            // Test OpenAI connection
            WriteInfo("Testing OpenAI API connection...");
            var connectionResult = await validator.ValidateOpenAIConnection();
            if (!connectionResult.IsValid)
            {
                WriteError($"OpenAI Connection Error: {connectionResult.ErrorMessage}");
                return;
            }
            WriteSuccess("OpenAI API connection successful");
            Console.WriteLine();

            // Run batch — pass testLimit as nullable (null = no limit)
            var batchProcessor = new BatchProcessor(appConfig, promptConfig);

            try
            {
                int? limitParam = (testLimit == 0) ? null : (int?)testLimit;

                var results = await batchProcessor.ProcessBatchAsync(
                    folderPath,
                    limitParam,
                    worksheetIndex,
                    useCache);

                if (results.Count == 0)
                {
                    WriteWarning("No files were processed.");
                }
            }
            catch (DirectoryNotFoundException ex)
            {
                WriteError($"Folder not found: {ex.Message}");
            }
            catch (Exception ex)
            {
                WriteError($"Batch processing failed: {ex.Message}");
            }

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // Load Configuration — API key + prompt settings only.
        // No ExcelPath. No WorksheetIndex. FileSelector provides those.
        // ============================================================
        static (Configuration? appConfig, PromptConfig? promptConfig) LoadConfiguration()
        {
            WriteInfo("Loading configuration...");

            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("PromptConfig.json", optional: false, reloadOnChange: true)
                .Build();

            string? apiKey = configBuilder["OpenAI:ApiKey"];
            string model = configBuilder["OpenAI:Model"] ?? "gpt-4o-mini";

            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR-ACTUAL-API-KEY-HERE")
            {
                WriteError("ERROR: OpenAI API key not configured!");
                WriteError("Please update appsettings.json with your actual API key.");
                return (null, null);
            }

            var appConfig = new Configuration
            {
                ApiKey = apiKey,
                Model = model
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
        // Validate Configuration
        // excelPath and worksheetIndex come from FileSelector now
        // ============================================================
        static async Task<bool> ValidateConfiguration(Configuration appConfig, PromptConfig promptConfig, string excelPath, int worksheetIndex)
        {
            WriteInfo("Validating configuration...");
            Console.WriteLine();

            var validator = new ConfigurationValidator(appConfig, promptConfig);

            var (isValid, errorMessage) = await validator.ValidateAll(excelPath, worksheetIndex);

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

            WriteSuccess("API key format valid");
            WriteSuccess("Excel file exists and is accessible");

            var worksheetResult = validator.ValidateWorksheetIndex(excelPath, worksheetIndex);
            if (worksheetResult.IsValid && !string.IsNullOrEmpty(worksheetResult.DetailedInfo))
            {
                WriteSuccess(worksheetResult.DetailedInfo);
            }

            WriteSuccess("OpenAI API connection successful");
            Console.WriteLine();

            return true;
        }

        // ============================================================
        // Display Help
        // ============================================================
        static void DisplayHelp()
        {
            WriteHeader($"{AppName} v{Version}");
            Console.WriteLine();
            WriteInfo("USAGE:");
            Console.WriteLine("  dotnet run                        # Launch interactive menu");
            Console.WriteLine("  dotnet run -- --help              # Show this help message");
            Console.WriteLine("  dotnet run -- --version           # Show version information");
            Console.WriteLine("  dotnet run -- --clear-cache       # Clear all cached results");
            Console.WriteLine("  dotnet run -- --no-cache          # Disable cache for this run");
            Console.WriteLine("  dotnet run -- --test-requirements # 🆕 Test requirement extraction");
            Console.WriteLine();
            WriteInfo("The interactive menu lets you:");
            Console.WriteLine("  - Pick single file or batch mode");
            Console.WriteLine("  - Select which Excel file to analyze");
            Console.WriteLine("  - Choose worksheet index");
            Console.WriteLine("  - Set how many tests to run");
            Console.WriteLine();
            WriteInfo("OPTIONS:");
            Console.WriteLine("  --help, -h                        Show this help message");
            Console.WriteLine("  --version, -v                     Show version information");
            Console.WriteLine("  --clear-cache                     Clear all cached results");
            Console.WriteLine("  --no-cache                        Disable cache (force fresh analysis)");
            Console.WriteLine("  --test-requirements               🆕 Test AI requirement extraction (Day 15)");
            Console.WriteLine();
            WriteInfo("CACHE SYSTEM:");
            Console.WriteLine("  - Analysis results are cached to save API costs");
            Console.WriteLine("  - Cache expires after 30 days automatically");
            Console.WriteLine("  - Unchanged tests use cached results (instant + free!)");
            Console.WriteLine("  - Changed tests are automatically re-analyzed");
            Console.WriteLine("  - Use --clear-cache to reset all cached data");
        }

        // ============================================================
        // Display Version
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
