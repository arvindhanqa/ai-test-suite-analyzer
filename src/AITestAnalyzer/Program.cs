using Microsoft.Extensions.Configuration;
using OfficeOpenXml;
using static AITestAnalyzer.FileSelector;

namespace AITestAnalyzer
{
    class Program
    {
        private const string Version = "1.0.0";
        private const string AppName = "AI Test Suite Analyzer";
        private const int CACHE_MAX_AGE_DAYS = Constants.CACHE_MAX_AGE_DAYS;

        private static TestCaseCache? _activeCache;
        private static RequirementCache? _activeReqCache;
        static async Task Main(string[] args)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Aravindhan Rajasekaran");

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // Prevent immediate kill
                Console.WriteLine();
                WriteWarning("Shutdown requested. Saving cache...");

                _activeCache?.SaveCache();
                _activeReqCache?.SaveCache();

                WriteSuccess("Cache saved. Exiting cleanly.");
                Environment.Exit(0);
            };
            // ============================================================
            // EARLY EXIT FLAGS — these don't need FileSelector at all
            // ============================================================
            if (args.Length > 0)
            {
                string firstArg = args[0].ToLower();

                // For requirement extraction testing
                if (firstArg == "--test-requirements")
                {
                    await TestRequirementExtractionAsync();
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
                    WriteInfo("Clearing all caches...");
                    var tempCache = new TestCaseCache();
                    tempCache.ClearCache();
                    var tempReqCache = new RequirementCache();
                    tempReqCache.ClearCache();
                    WriteSuccess("All caches cleared successfully!");
                    Console.WriteLine();
                    WriteInfo("Deleted: cache/test_analysis_cache.json");
                    WriteInfo("Deleted: cache/requirements/requirements_cache.json");
                    WriteInfo("Next run will re-analyze all tests and requirements using OpenAI API.");
                    return;
                }
            }

            // Check for --no-cache anywhere in args (still supported as CLI override)
            bool useCache = !args.Any(a => a.ToLower() == "--no-cache");

            bool resumeBatch = args.Any(a => a.ToLower() == "--resume");

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

            // ← NEW: Dry run check goes HERE, before routing
            await HandleDryRunOptionAsync(selection, useCache, promptConfig);  // if they chose D, this exits after preview

            // ============================================================
            // STEP 3: Route to batch or single based on selection
            // ============================================================
            if (selection.SelectedMode == FileSelector.SelectionResult.Mode.Batch)
            {
                await RunBatchModeAsync(appConfig, promptConfig, selection, resumeBatch);
            }
            else
            {
                await RunSingleModeAsync(appConfig, promptConfig, selection, useCache);
            }
        }


        // ============================================================
        // MI-4: DRY RUN PREVIEW — shows cost estimate before analysis
        // Single file only. Batch mode skips this.
        // ============================================================
        static async Task HandleDryRunOptionAsync(SelectionResult selection, bool useCache, PromptConfig promptConfig)
        {
            // Batch mode — dry run not supported, proceed normally
            if (selection.SelectedMode == SelectionResult.Mode.Batch)
                return;

            string analysisMode = selection.SelectedAnalysisMode == AnalysisMode.QA ? "QA" : "BA";
            int testLimit = selection.TestLimit;

            // Show ready-to-run summary
            Console.WriteLine();
            WriteHeader("─── Ready to run ───────────────────────────────────");
            Console.WriteLine($"   File:        {Path.GetFileName(selection.FilePath)}");
            Console.WriteLine($"   Mode:        {analysisMode}");
            Console.WriteLine($"   Sheet:       {selection.SheetIndex}");
            Console.WriteLine($"   Tests:       {(testLimit == 0 ? "ALL" : testLimit.ToString())}");
            Console.WriteLine($"   Cache:       {(useCache ? "Enabled" : "Disabled")}");
            WriteHeader("────────────────────────────────────────────────────");
            Console.WriteLine();

            Console.Write("  Press ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Enter");
            Console.ResetColor();
            Console.Write(" to analyze, ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("D");
            Console.ResetColor();
            Console.Write(" for dry-run preview, ");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("B");
            Console.ResetColor();
            Console.Write(" to go back: ");

            // Clear any buffered keystrokes from FileSelector navigation
            while (Console.KeyAvailable)
                Console.ReadKey(intercept: true);

            string? input = Console.ReadLine()?.Trim().ToUpper();

            if (input == "B")
            {
                WriteInfo("Exiting. Run again to restart.");
                Environment.Exit(0);
            }

            if (input != "D")
                return; // Enter or anything else → proceed normally

            // ── DRY RUN PREVIEW ──
            Console.WriteLine();
            WriteHeader("════════════════════════════════════════════════════");
            WriteHeader("🔍 DRY RUN MODE — No API calls will be made");
            WriteHeader("════════════════════════════════════════════════════");
            Console.WriteLine();

            try
            {
                var excelReader = new ExcelReader(selection.FilePath, selection.SheetIndex);
                var (isValid, validationMsg) = excelReader.ValidateExcelStructure();

                if (!isValid)
                {
                    WriteError($"Cannot preview: {validationMsg}");
                    Environment.Exit(1);
                }

                int totalRowsInExcel = excelReader.CountTestRows();
                int testsToCheck = (testLimit == 0 || testLimit > totalRowsInExcel)
                    ? totalRowsInExcel
                    : testLimit;

                WriteInfo($"Scanning {testsToCheck} tests for cache coverage...");
                Console.WriteLine();

                int cacheHits = 0;

                if (useCache)
                {
                    var cache = new TestCaseCache();
                    string cachePrefix = analysisMode == "QA" ? "" : "ba_";

                    for (int row = 2; row <= testsToCheck + 1; row++)
                    {
                        var testCase = excelReader.ReadTestCase(row);
                        if (testCase == null || string.IsNullOrEmpty(testCase.TestId))
                            continue;

                        string hash = cachePrefix + cache.GenerateHash(testCase);
                        if (cache.TryGetCached(hash, out _, CACHE_MAX_AGE_DAYS))
                            cacheHits++;
                    }
                }

                int apiCallsNeeded = testsToCheck - cacheHits;
                double estimatedTokens = apiCallsNeeded * promptConfig.MaxTokens;
                double estimatedCost = estimatedTokens * promptConfig.CostPerToken;
                double cachePercent = testsToCheck > 0 ? (cacheHits * 100.0 / testsToCheck) : 0;

                WriteHeader("📊 Analysis Preview:");
                Console.WriteLine($"   File:              {Path.GetFileName(selection.FilePath)}");
                Console.WriteLine($"   Mode:              {analysisMode}");
                Console.WriteLine($"   Total tests:       {testsToCheck}");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   Cache hits:        {cacheHits} ({cachePercent:F1}%) — free & instant");
                Console.ResetColor();

                Console.ForegroundColor = apiCallsNeeded > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
                Console.WriteLine($"   API calls needed:  {apiCallsNeeded}");
                Console.ResetColor();

                Console.WriteLine($"   Estimated tokens:  {estimatedTokens:N0}");

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"   Estimated cost:    ${estimatedCost:F6}");
                Console.ResetColor();

                Console.WriteLine();
                if (apiCallsNeeded == 0)
                    WriteSuccess("All tests cached — this run will be FREE!");
                else
                    WriteInfo($"To proceed, run again and press Enter at the prompt.");
            }
            catch (Exception ex)
            {
                WriteError($"Dry run failed: {ex.Message}");
            }

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(0);
        }


        // ============================================================
        // TEST REQUIREMENT EXTRACTION (Day 15 Feature)
        // ============================================================
        static async Task TestRequirementExtractionAsync()
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
                MaxTokens = 4000,
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

            var connectionResult = await validator.ValidateOpenAIConnectionAsync();
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
            var requirements = await extractor.ExtractRequirementsAsync(requirementFile, cache, maxAgeDays: 30);

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
        static async Task RunSingleModeAsync(Configuration appConfig, PromptConfig promptConfig, SelectionResult selection, bool useCache)
        {
            string excelPath = selection.FilePath;
            int worksheetIndex = selection.SheetIndex;
            int testLimit = selection.TestLimit; // 0 = all
            string analysisMode = selection.SelectedAnalysisMode == AnalysisMode.QA ? "QA" : "BA"; 

            var aiAnalyzer = new AIAnalyzer(appConfig, promptConfig);
            Console.WriteLine();

            // ============================================================
            // MODE-SPECIFIC SETUP
            // ============================================================
            List<ExtractedRequirement> requirements = new List<ExtractedRequirement>();

            if (analysisMode == "BA")
            {
                WriteInfo("BA MODE: Loading requirements for coverage analysis...");

                var reqCache = new RequirementCache();
                _activeReqCache = reqCache;
                var reqExtractor = new RequirementExtractor(appConfig, promptConfig);

                // Auto-detect requirement file
                string testFileName = Path.GetFileNameWithoutExtension(excelPath);
                string reqFileName = testFileName.Replace("test_cases_", "requirements_") + ".md";
                string dataFolder = Path.GetDirectoryName(excelPath) ?? ".";
                string reqPath = Path.Combine(dataFolder, reqFileName);

                if (!File.Exists(reqPath))
                {
                    Console.WriteLine($"⚠️  Auto-detection failed. Could not find: {reqFileName}");
                    Console.Write("📁 Enter requirement file path: ");
                    string? userInput = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(userInput))
                    {
                        WriteError("BA Mode requires a requirement file. Exiting...");
                        WriteInfo("Press any key to exit...");
                        Console.ReadKey();
                        return;
                    }

                    reqPath = userInput;
                }
                else
                {
                    Console.WriteLine($"✅ Auto-detected requirement file: {Path.GetFileName(reqPath)}");
                }

                try
                {
                    requirements = await reqExtractor.ExtractRequirementsAsync(reqPath, reqCache);
                    WriteSuccess($"Loaded {requirements.Count} requirements");
                }
                catch (Exception ex)
                {
                    WriteError($"Failed to load requirements: {ex.Message}");
                    WriteInfo("Press any key to exit...");
                    Console.ReadKey();
                    return;
                }
            }
            else
            {
                WriteInfo("QA MODE: Quality analysis only (no requirements needed)");
            }

            Console.WriteLine();

            bool configValid = await ValidateConfigurationAsync(appConfig, promptConfig, excelPath, worksheetIndex);
            if (!configValid)
            {
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            // Prepare output file
            WriteInfo("Preparing output file...");
            string outputDir = ExcelWriter.CreateOutputFolder();
            string outputPath = ExcelWriter.PrepareOutputFile(excelPath, outputDir);

            var excelWriter = new ExcelWriter(outputPath, promptConfig, worksheetIndex);
            excelWriter.RenameOriginalSheet();
            excelWriter.AddAnalysisColumnHeader(analysisMode); 
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
                _activeCache = cache;
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
            var results = new List<(string TestId, string Result, int Tokens, string Coverage)>();
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

                string quality = "";
                string coverage = "";
                int tokens = 0;

                // QA MODE: Quality analysis only
                if (analysisMode == "QA")
                {
                    if (useCache && cache != null)
                    {
                        string hash = cache.GenerateHash(testCase);
                        if (cache.TryGetCached(hash, out CachedResult? cachedResult, CACHE_MAX_AGE_DAYS))
                        {
                            quality = cachedResult!.Quality;
                            tokens = 0;
                            cacheHits++;
                        }
                        else
                        {
                            (quality, tokens) = await aiAnalyzer.AnalyzeTestQualityAsync(testCase);
                            cache.AddToCache(testCase.TestId, hash, quality, "", tokens);
                            apiCalls++;
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        (quality, tokens) = await aiAnalyzer.AnalyzeTestQualityAsync(testCase);
                        apiCalls++;
                        await Task.Delay(1000);
                    }
                    coverage = "";
                }
                // BA MODE: Coverage + requirement feedback
                else
                {
                    if (useCache && cache != null)
                    {
                        string baseHash = cache.GenerateHash(testCase);
                        string baHash = "ba_" + baseHash;  // Separate namespace from QA cache

                        if (cache.TryGetCached(baHash, out CachedResult? cachedResult, CACHE_MAX_AGE_DAYS))
                        {
                            quality = cachedResult!.Quality;
                            coverage = cachedResult!.Coverage;
                            tokens = 0;
                            cacheHits++;
                        }
                        else
                        {
                            var (reqFeedback, coverageIds, tokensUsed) = await aiAnalyzer.AnalyzeCoverageAndFeedbackAsync(testCase, requirements);
                            quality = reqFeedback;
                            coverage = string.Join(", ", coverageIds);
                            tokens = tokensUsed;
                            cache.AddToCache(testCase.TestId, baHash, quality, coverage, tokens);
                            apiCalls++;
                            await Task.Delay(1000);
                        }
                    }
                    else
                    {
                        var (reqFeedback, coverageIds, tokensUsed) = await aiAnalyzer.AnalyzeCoverageAndFeedbackAsync(testCase, requirements);
                        quality = reqFeedback;
                        coverage = string.Join(", ", coverageIds);
                        tokens = tokensUsed;
                        apiCalls++;
                        await Task.Delay(1000);
                    }
                }

                results.Add((testCase.TestId, quality, tokens, coverage));
                excelWriter.WriteAnalysis(row, quality, coverage, analysisMode);  
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

            // Create summary sheets (only for QA mode)
            if (analysisMode == "QA")
            {
                Console.WriteLine();
                WriteInfo("Creating Quality Issues Summary...");
                excelWriter.CreateQualityIssuesSheet(results);

                WriteInfo("Creating Statistics Dashboard...");
                excelWriter.CreateStatisticsDashboard(results, startTime, endTime);
            }

            // BA Mode: Coverage Gap Analysis sheet
            if (analysisMode == "BA")
            {
                Console.WriteLine();
                WriteInfo("Creating Coverage Gap Analysis...");
                
                excelWriter.CreateCoverageGapSheet(results, requirements);
                WriteInfo("Creating BA Statistics Dashboard...");
                excelWriter.CreateBAStatisticsDashboard(
                    results,
                    requirements,
                    results.Sum(r => r.Tokens),
                    cacheHits,
                    endTime - startTime
                );
            }

            // Display summary
            Console.WriteLine();
            SummaryDisplay.Display(results, startTime, endTime, outputPath, cacheHits, apiCalls, useCache, promptConfig, analysisMode);

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // BATCH MODE — receives everything from FileSelector.
        // No arg parsing here. BatchProcessor gets what it needs directly.
        // ============================================================
        static async Task RunBatchModeAsync(Configuration appConfig, PromptConfig promptConfig, SelectionResult selection, bool resume = false)
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

            var promptConfigResult = validator.ValidatePromptConfig();
            if (!promptConfigResult.IsValid)
            {
                WriteError($"PromptConfig Error: {promptConfigResult.ErrorMessage}");
                return;
            }

            var apiKeyResult = validator.ValidateApiKey();
            if (!apiKeyResult.IsValid)
            {
                WriteError($"API Key Error: {apiKeyResult.ErrorMessage}");
                return;
            }

            // Test OpenAI connection
            WriteInfo("Testing OpenAI API connection...");
            var connectionResult = await validator.ValidateOpenAIConnectionAsync();
            if (!connectionResult.IsValid)
            {
                WriteError($"OpenAI Connection Error: {connectionResult.ErrorMessage}");
                return;
            }
            WriteSuccess("OpenAI API connection successful");
            Console.WriteLine();

            // Run batch — pass testLimit as nullable (null = no limit)
            var batchProcessor = new BatchProcessor(appConfig, promptConfig);
            var sharedCache = new TestCaseCache();
            _activeCache = sharedCache;
            _activeReqCache = new RequirementCache();

            try
            {
                int? limitParam = (testLimit == 0) ? null : (int?)testLimit;

                string batchMode = selection.SelectedAnalysisMode == AnalysisMode.QA ? "QA" : "BA";

                var results = await batchProcessor.ProcessBatchAsync(
                    folderPath,
                    limitParam,
                    worksheetIndex,
                    useCache,
                    batchMode,
                    sharedCache,
                    resume);

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
                UserTemplate = configBuilder["UserTemplate"] ?? "Analyze: {Scenario}",
                CostPerToken = double.Parse(configBuilder["CostPerToken"] ?? "0.00000015")
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
        static async Task<bool> ValidateConfigurationAsync(Configuration appConfig, PromptConfig promptConfig, string excelPath, int worksheetIndex)
        {
            WriteInfo("Validating configuration...");
            Console.WriteLine();

            var validator = new ConfigurationValidator(appConfig, promptConfig);

            var (isValid, errorMessage) = await validator.ValidateAllAsync(excelPath, worksheetIndex);

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
            Console.WriteLine("  dotnet run -- --resume            # Resume interrupted batch run");
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
