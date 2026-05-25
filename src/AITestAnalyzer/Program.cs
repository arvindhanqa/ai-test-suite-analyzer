using AITestAnalyzer.Config;
using AITestAnalyzer.Infrastructure;
using AITestAnalyzer.Models;
using AITestAnalyzer.Services;
using AITestAnalyzer.UI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OfficeOpenXml;
using static AITestAnalyzer.UI.FileSelector;

namespace AITestAnalyzer
{
    internal class Program
    {
        private const string Version = "1.0.0";
        private const string AppName = "AI Test Suite Analyzer";
        private const int CACHE_MAX_AGE_DAYS = Constants.CACHE_MAX_AGE_DAYS;

        private static ITestCaseCache? _activeCache;
        private static RequirementCache? _activeReqCache;
        private static async Task Main(string[] args)
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

                // For GEN Mode orchestrator testing
                if (firstArg == "--test-gen")
                {
                    await TestGenModeOrchestratorAsync();
                    return;
                }

                // GEN Mode direct launch — skips main menu
                if (firstArg == "--gen-mode")
                {
                    var (genAppConfig, genPromptConfig) = LoadConfiguration();
                    if (genAppConfig == null || genPromptConfig == null)
                        return;

                    var genSelection = FileSelector.SelectGenModeDirect();
                    if (genSelection != null)
                        await RunGenModeAsync(genAppConfig, genPromptConfig, genSelection);
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

            bool exportJson = args.Any(a => a.ToLower() == "--format") &&
                  args.SkipWhile(a => a.ToLower() != "--format").Skip(1).FirstOrDefault()?.ToLower() == "json";

            WriteHeader("===============================================");
            WriteHeader($"{AppName} v{Version}");
            WriteHeader("===============================================");
            Console.WriteLine();

            // ============================================================
            // STEP 1: Load config (API key + prompt settings only)
            // ============================================================
            var (appConfig, promptConfig) = LoadConfiguration();
            if (appConfig == null || promptConfig == null) return;

            // Build DI container
            var serviceProvider = BuildServiceProvider(appConfig, promptConfig);
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
            // STEP 3: Route to batch, GEN, or single based on selection
            // ============================================================
            if (selection.SelectedMode == FileSelector.SelectionResult.Mode.Batch)
            {
                await RunBatchModeAsync(appConfig, promptConfig, selection, resumeBatch, serviceProvider);
            }
            else if (selection.SelectedMode == FileSelector.SelectionResult.Mode.Gen)
            {
                await RunGenModeAsync(appConfig, promptConfig, selection, exportJson, serviceProvider);
            }
            else
            {
                await RunSingleModeAsync(appConfig, promptConfig, selection, useCache, exportJson, serviceProvider);
            }
        }


        // ============================================================
        // MI-4: DRY RUN PREVIEW — shows cost estimate before analysis
        // Single file only. Batch mode skips this.
        // ============================================================
        private static async Task HandleDryRunOptionAsync(SelectionResult selection, bool useCache, PromptConfig promptConfig)
        {
            // Batch and GEN Mode — dry run not supported, proceed normally
            if (selection.SelectedMode == SelectionResult.Mode.Batch ||
                selection.SelectedMode == SelectionResult.Mode.Gen)
                return;

            AnalysisMode analysisMode = selection.SelectedAnalysisMode;

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
                    string cachePrefix = analysisMode == AnalysisMode.QA ? "" : "ba_";

                    var testCases = excelReader.ReadAllTestCases(testsToCheck);
                    foreach (var testCase in testCases)
                    {
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
        private static async Task TestRequirementExtractionAsync()
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

            var appConfig = new Configuration { ApiKey = apiKey};
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

            // File path — prompt user instead of hardcoding
            Console.Write("📁 Enter path to requirement file: ");
            string? requirementFile = Console.ReadLine()?.Trim().Trim('"').Trim('\'');

            if (string.IsNullOrWhiteSpace(requirementFile))
            {
                WriteError("No path entered.");
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            if (!File.Exists(requirementFile))
            {
                WriteError($"File not found: {requirementFile}");
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
        // TEST GEN MODE ORCHESTRATOR (Day 111)
        // ============================================================
        private static async Task TestGenModeOrchestratorAsync()
        {
            Console.Clear();
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            WriteHeader("   🧪 GEN MODE ORCHESTRATOR TEST");
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Load configuration
            WriteInfo("Loading configuration...");
            var (appConfig, promptConfig) = LoadConfiguration();
            if (appConfig == null || promptConfig == null)
                return;

            // Validate API connection
            WriteInfo("Validating API connection...");
            var validator = new ConfigurationValidator(appConfig, promptConfig);
            var connectionResult = await validator.ValidateOpenAIConnectionAsync();
            if (!connectionResult.IsValid)
            {
                WriteError($"OpenAI Connection Error: {connectionResult.ErrorMessage}");
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            WriteSuccess("API connection validated");
            Console.WriteLine();

            // Prompt for requirements file
            Console.Write("📁 Enter path to requirements file: ");
            string? reqPath = Console.ReadLine()?.Trim().Trim('"').Trim('\'');

            if (string.IsNullOrWhiteSpace(reqPath) || !File.Exists(reqPath))
            {
                WriteError($"Requirements file not found: {reqPath}");
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            string requirementsMarkdown = await File.ReadAllTextAsync(reqPath);
            WriteSuccess($"Loaded: {Path.GetFileName(reqPath)} ({requirementsMarkdown.Length:N0} chars)");
            Console.WriteLine();

            // Prompt for options
            Console.Write("   How many test cases to generate? [default: 5]: ");
            string? countInput = Console.ReadLine()?.Trim();
            int targetCount = int.TryParse(countInput, out int parsed) ? parsed : 5;

            Console.Write("   Maximum refinement passes? [1-3, default: 2]: ");
            string? passInput = Console.ReadLine()?.Trim();
            int maxPasses = int.TryParse(passInput, out int parsedPasses) ? parsedPasses : 2;

            Console.WriteLine();
            WriteHeader("════════════════════════════════════════════════════");
            WriteHeader($"   Generating {targetCount} test cases, up to {maxPasses} passes");
            WriteHeader("════════════════════════════════════════════════════");

            var startTime = DateTime.Now;

            try
            {
                var aiAnalyzer = new AIAnalyzer(appConfig, promptConfig);
                var cache = new TestCaseCache();
                var orchestrator = new GenModeOrchestrator(aiAnalyzer, cache, promptConfig)
                {
                    MaxPasses = maxPasses,
                    TargetTestCount = targetCount
                };

                var result = await orchestrator.RunAsync(
                    requirementsMarkdown,
                    targetCount,
                    maxPasses);

                var elapsed = DateTime.Now - startTime;

                // Display results
                Console.WriteLine();
                WriteHeader("═══════════════════════════════════════════════════════════════════════");
                WriteHeader("   GENERATED TEST CASES");
                WriteHeader("═══════════════════════════════════════════════════════════════════════");
                Console.WriteLine();

                foreach (var tc in result.TestCases)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"   {tc.TestId} ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"[Pass {tc.PassNumber}] ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(tc.Scenario);
                    Console.ResetColor();

                    if (!string.IsNullOrEmpty(tc.QAScore))
                    {
                        Console.ForegroundColor = tc.QAScore.StartsWith("GOOD",
                            StringComparison.OrdinalIgnoreCase)
                            ? ConsoleColor.Green : ConsoleColor.Yellow;
                        Console.WriteLine($"      QA: {tc.QAScore}");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine();
                WriteHeader("═══════════════════════════════════════════════════════════════════════");
                WriteSuccess($"Test cases generated: {result.TestCases.Count}");
                WriteSuccess($"Total passes:         {result.TotalPasses}");
                WriteSuccess($"Total tokens:         {result.TotalTokens:N0}");
                WriteSuccess($"Elapsed time:         {elapsed.TotalSeconds:F1}s");

                double cost = result.TotalTokens * promptConfig.CostPerToken;
                WriteSuccess($"Estimated cost:       ${cost:F6}");

                // ── EXCEL OUTPUT ──────────────────────────────────────
                Console.WriteLine();
                WriteInfo("Creating Excel output...");
                var genExcelWriter = new GenModeExcelWriter(promptConfig);
                string outputPath = genExcelWriter.WriteOutput(result, elapsed);

                // ── JSON OUTPUT ───────────────────────────────────────
                WriteInfo("Creating JSON export...");
                string jsonPath = JsonExporter.Export(result, promptConfig, elapsed, outputPath);
                WriteSuccess($"JSON export: {Path.GetFileName(jsonPath)}");

                Console.WriteLine();
                WriteHeader("═══════════════════════════════════════════════════════════════════════");
                WriteSuccess($"Output folder: {Path.GetDirectoryName(outputPath)}");
            }
            catch (ArgumentException ex)
            {
                WriteError(ex.Message);
            }
            catch (Exception ex)
            {
                WriteError($"GEN Mode test failed: {ex.Message}");
            }

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // GEN MODE — generates test cases from requirements document
        // ============================================================
        private static async Task RunGenModeAsync(
            Configuration appConfig,
            PromptConfig promptConfig,
            SelectionResult selection,
            bool exportJson = false,
            IServiceProvider? serviceProvider = null)
        {
            Console.WriteLine();
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            WriteHeader("   🤖 GEN MODE — GENERATING TEST CASES");
            WriteHeader("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Validate requirements file
            if (string.IsNullOrWhiteSpace(selection.RequirementsPath) ||
                !File.Exists(selection.RequirementsPath))
            {
                WriteError($"Requirements file not found: {selection.RequirementsPath}");
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Validate API connection
            WriteInfo("Validating API connection...");
            var validator = new ConfigurationValidator(appConfig, promptConfig);
            var connectionResult = await validator.ValidateOpenAIConnectionAsync();
            if (!connectionResult.IsValid)
            {
                WriteError($"OpenAI Connection Error: {connectionResult.ErrorMessage}");
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return;
            }
            WriteSuccess("API connection validated");
            Console.WriteLine();

            // Load requirements
            string requirementsMarkdown = await File.ReadAllTextAsync(selection.RequirementsPath);
            WriteSuccess($"Requirements: {Path.GetFileName(selection.RequirementsPath)} " +
                         $"({requirementsMarkdown.Length:N0} chars)");
            WriteInfo($"Target tests:  {selection.TargetTestCount}");
            WriteInfo($"Max passes:    {selection.MaxPasses}");
            Console.WriteLine();

            var startTime = DateTime.Now;

            try
            {
                // Build orchestrator from DI or direct
                var aiAnalyzer = serviceProvider?.GetRequiredService<IAIAnalyzer>()
                    ?? new AIAnalyzer(appConfig, promptConfig);
                var cache = new TestCaseCache();
                _activeCache = cache;

                var orchestrator = new GenModeOrchestrator(aiAnalyzer, cache, promptConfig)
                {
                    MaxPasses = selection.MaxPasses,
                    TargetTestCount = selection.TargetTestCount
                };

                // Run the Generate → Critique → Refine pipeline
                var result = await orchestrator.RunAsync(
                    requirementsMarkdown,
                    selection.TargetTestCount,
                    selection.MaxPasses);

                var elapsed = DateTime.Now - startTime;

                // Display console summary
                Console.WriteLine();
                WriteHeader("═══════════════════════════════════════════════════════════════════════");
                WriteHeader("   GENERATED TEST CASES");
                WriteHeader("═══════════════════════════════════════════════════════════════════════");
                Console.WriteLine();

                foreach (var tc in result.TestCases)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"   {tc.TestId} ");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"[Pass {tc.PassNumber}] ");
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(tc.Scenario);
                    Console.ResetColor();

                    if (!string.IsNullOrEmpty(tc.QAScore))
                    {
                        Console.ForegroundColor = tc.QAScore.StartsWith("GOOD",
                            StringComparison.OrdinalIgnoreCase)
                            ? ConsoleColor.Green : ConsoleColor.Yellow;
                        Console.WriteLine($"      QA: {tc.QAScore}");
                        Console.ResetColor();
                    }
                }

                Console.WriteLine();
                WriteSuccess($"Test cases generated: {result.TestCases.Count}");
                WriteSuccess($"Total passes:         {result.TotalPasses}");
                WriteSuccess($"Total tokens:         {result.TotalTokens:N0}");
                WriteSuccess($"Elapsed time:         {elapsed.TotalSeconds:F1}s");
                WriteSuccess($"Estimated cost:       ${result.TotalTokens * promptConfig.CostPerToken:F6}");

                // Excel output
                Console.WriteLine();
                WriteInfo("Creating Excel output...");
                var genExcelWriter = new GenModeExcelWriter(promptConfig);
                string outputPath = genExcelWriter.WriteOutput(result, elapsed);

                // JSON output
                if (exportJson)
                {
                    WriteInfo("Creating JSON export...");
                    string jsonPath = JsonExporter.Export(result, promptConfig, elapsed, outputPath);
                    WriteSuccess($"JSON export: {Path.GetFileName(jsonPath)}");
                }

                Console.WriteLine();
                WriteSuccess($"Output folder: {Path.GetDirectoryName(outputPath)}");
            }
            catch (ArgumentException ex)
            {
                WriteError(ex.Message);
            }
            catch (Exception ex)
            {
                WriteError($"GEN Mode failed: {ex.Message}");
            }

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }


        // ============================================================
        // SINGLE FILE MODE — orchestrator only, delegates to helpers
        // ============================================================
        private static async Task RunSingleModeAsync(Configuration appConfig, PromptConfig promptConfig, SelectionResult selection, bool useCache, bool exportJson = false, IServiceProvider? serviceProvider = null)
        {
            var aiAnalyzer = serviceProvider?.GetRequiredService<IAIAnalyzer>()
                ?? new AIAnalyzer(appConfig, promptConfig);
            Console.WriteLine();

            // Load requirements (BA mode only)
            List<ExtractedRequirement>? requirements = await LoadRequirementsAsync(appConfig, promptConfig, selection);
            if (requirements == null)
                return;

            // Validate configuration
            bool configValid = await ValidateConfigurationAsync(appConfig, promptConfig, selection.FilePath, selection.SheetIndex);
            if (!configValid)
            { WriteInfo("Press any key to exit..."); Console.ReadKey(); return; }

            // Prepare output file + validate Excel
            var prepared = PrepareOutputAsync(selection, promptConfig);
            if (prepared == null)
            { WriteInfo("Press any key to exit..."); Console.ReadKey(); return; }
            var (excelWriter, excelReader, totalTests, outputPath) = prepared.Value;

            // Initialize cache
            var cache = InitializeCache(useCache);

            // Process all tests
            var startTime = DateTime.Now;
            var (results, cacheHits, apiCalls) = await ProcessTestsAsync(
                excelReader, excelWriter, aiAnalyzer, cache,
                requirements, selection, useCache, totalTests);
            var endTime = DateTime.Now;

            // Save cache + create output sheets
            await SaveCacheAsync(cache, useCache);
            CreateOutputSheets(excelWriter, results, requirements, selection.SelectedAnalysisMode, startTime, endTime, cacheHits);
            if (exportJson)
            {
                string jsonPath = JsonExporter.Export(
                    results, selection.SelectedAnalysisMode,
                    cacheHits, apiCalls, startTime, endTime,
                    promptConfig, outputPath);
                WriteSuccess($"JSON export: {Path.GetFileName(jsonPath)}");
            }

            // Display summary
            Console.WriteLine();
            SummaryDisplay.Display(results, startTime, endTime, outputPath, cacheHits, apiCalls, useCache, promptConfig, selection.SelectedAnalysisMode);

            Console.WriteLine();
            WriteInfo("Press any key to exit...");
            Console.ReadKey();
        }

        // ============================================================
        // HELPER: Load requirements for BA mode
        // Returns empty list for QA mode, null if BA mode load failed
        // ============================================================
        private static async Task<List<ExtractedRequirement>?> LoadRequirementsAsync(Configuration appConfig, PromptConfig promptConfig, SelectionResult selection)
        {
            if (selection.SelectedAnalysisMode == AnalysisMode.QA)
            {
                WriteInfo("QA MODE: Quality analysis only (no requirements needed)");
                return new List<ExtractedRequirement>();
            }

            WriteInfo("BA MODE: Loading requirements for coverage analysis...");

            var reqCache = new RequirementCache();
            _activeReqCache = reqCache;
            var reqExtractor = new RequirementExtractor(appConfig, promptConfig);

            string testFileName = Path.GetFileNameWithoutExtension(selection.FilePath);
            string reqFileName = testFileName.Replace("test_cases_", "requirements_") + ".md";
            string dataFolder = Path.GetDirectoryName(selection.FilePath) ?? ".";
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
                    return null;
                }

                reqPath = userInput;
            }
            else
            {
                Console.WriteLine($"✅ Auto-detected requirement file: {Path.GetFileName(reqPath)}");
            }

            try
            {
                var requirements = await reqExtractor.ExtractRequirementsAsync(reqPath, reqCache);
                WriteSuccess($"Loaded {requirements.Count} requirements");
                Console.WriteLine();
                return requirements;
            }
            catch (Exception ex)
            {
                WriteError($"Failed to load requirements: {ex.Message}");
                WriteInfo("Press any key to exit...");
                Console.ReadKey();
                return null;
            }
        }

        // ============================================================
        // HELPER: Prepare output file, ExcelWriter, ExcelReader
        // Returns null if validation fails
        // ============================================================
        private static (ExcelWriter excelWriter, ExcelReader excelReader, int totalTests, string outputPath)? PrepareOutputAsync(SelectionResult selection, PromptConfig promptConfig)
        {
            WriteInfo("Preparing output file...");
            string outputDir = ExcelWriter.CreateOutputFolder();
            string outputPath = ExcelWriter.PrepareOutputFile(selection.FilePath, outputDir);

            var excelWriter = new ExcelWriter(outputPath, promptConfig, selection.SheetIndex);
            excelWriter.RenameOriginalSheet();
            excelWriter.AddAnalysisColumnHeader(selection.SelectedAnalysisMode);
            Console.WriteLine();

            var excelReader = new ExcelReader(selection.FilePath, selection.SheetIndex);

            WriteInfo("Validating Excel structure...");
            var (isValid, validationMessage) = excelReader.ValidateExcelStructure();

            if (!isValid)
            {
                WriteError($"VALIDATION ERROR: {validationMessage}");
                WriteError("Please check your Excel file and try again.");
                Console.WriteLine();
                return null;
            }

            WriteSuccess($"{validationMessage}");
            Console.WriteLine();

            int totalRowsInExcel = excelReader.CountTestRows();
            if (totalRowsInExcel == 0)
            {
                WriteError("ERROR: No test cases found in Excel file");
                return null;
            }

            int totalTests = (selection.TestLimit == 0 || selection.TestLimit > totalRowsInExcel)
                ? totalRowsInExcel
                : selection.TestLimit;

            WriteInfo($"Analyzing {totalTests} of {totalRowsInExcel} tests");
            Console.WriteLine();

            return (excelWriter, excelReader, totalTests, outputPath);
        }

        // ============================================================
        // HELPER: Initialize cache system
        // ============================================================
        private static ITestCaseCache? InitializeCache(bool useCache)
        {
            if (!useCache)
            {
                WriteWarning("Cache is disabled for this run (--no-cache)");
                Console.WriteLine();
                return null;
            }

            WriteInfo("Initializing cache system...");
            var cache = new TestCaseCache();
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
            return cache;
        }

        // ============================================================
        // HELPER: Process all test cases — QA and BA mode
        // ============================================================
        private static async Task<(List<(string TestId, string Result, int Tokens, string Coverage)> results, int cacheHits, int apiCalls)>
            ProcessTestsAsync(ExcelReader excelReader, ExcelWriter excelWriter, IAIAnalyzer aiAnalyzer,
                ITestCaseCache? cache, List<ExtractedRequirement> requirements,
                SelectionResult selection, bool useCache, int totalTests)
        {
            var results = new List<(string TestId, string Result, int Tokens, string Coverage)>();
            int cacheHits = 0;
            int apiCalls = 0;
            int processedCount = 0;

            var testCases = excelReader.ReadAllTestCases(totalTests);
            var progressTracker = new ProgressTracker(totalTests, DateTime.Now);
            AnalysisMode analysisMode = selection.SelectedAnalysisMode;

            for (int i = 0; i < testCases.Count; i++)
            {
                TestCase testCase = testCases[i];
                int row = i + 2;

                processedCount++;
                progressTracker.DisplayProgress(processedCount, testCase.TestId);

                string quality;
                string coverage;
                int tokens;

                if (analysisMode == AnalysisMode.QA)
                {
                    (quality, coverage, tokens, cacheHits, apiCalls) = await ProcessQATestAsync(
                        testCase, aiAnalyzer, cache, useCache, cacheHits, apiCalls);
                }
                else
                {
                    (quality, coverage, tokens, cacheHits, apiCalls) = await ProcessBATestAsync(
                        testCase, aiAnalyzer, cache, requirements, useCache, cacheHits, apiCalls);
                }

                results.Add((testCase.TestId, quality, tokens, coverage));
                excelWriter.WriteAnalysis(row, quality, coverage, analysisMode);
            }

            progressTracker.Complete();
            excelWriter.FlushAnalysis();

            return (results, cacheHits, apiCalls);
        }

        // ============================================================
        // HELPER: Process single test in QA mode
        // ============================================================
        private static async Task<(string quality, string coverage, int tokens, int cacheHits, int apiCalls)>
            ProcessQATestAsync(TestCase testCase, IAIAnalyzer aiAnalyzer, ITestCaseCache? cache,
                bool useCache, int cacheHits, int apiCalls)
        {
            string quality;
            int tokens;

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

            return (quality, "", tokens, cacheHits, apiCalls);
        }

        // ============================================================
        // HELPER: Process single test in BA mode
        // ============================================================
        private static async Task<(string quality, string coverage, int tokens, int cacheHits, int apiCalls)>
            ProcessBATestAsync(TestCase testCase, IAIAnalyzer aiAnalyzer, ITestCaseCache? cache,
                List<ExtractedRequirement> requirements, bool useCache, int cacheHits, int apiCalls)
        {
            string quality;
            string coverage;
            int tokens;

            if (useCache && cache != null)
            {
                string baseHash = cache.GenerateHash(testCase);
                string baHash = "ba_" + baseHash;

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

            return (quality, coverage, tokens, cacheHits, apiCalls);
        }

        // ============================================================
        // HELPER: Save cache after processing
        // ============================================================
        private static async Task SaveCacheAsync(ITestCaseCache? cache, bool useCache)
        {
            if (!useCache || cache == null)
                return;

            Console.WriteLine();
            WriteInfo("Saving cache...");
            int cleaned = cache.CleanExpiredEntries(CACHE_MAX_AGE_DAYS);
            if (cleaned > 0)
                WriteInfo($"Cleaned {cleaned} expired cache entries");
            await cache.SaveCacheAsync();
            WriteSuccess("Cache saved successfully");
        }

        // ============================================================
        // HELPER: Create output sheets (QA or BA)
        // ============================================================
        private static void CreateOutputSheets(ExcelWriter excelWriter,
            List<(string TestId, string Result, int Tokens, string Coverage)> results,
            List<ExtractedRequirement> requirements,
            AnalysisMode analysisMode, DateTime startTime, DateTime endTime, int cacheHits)
        {
            Console.WriteLine();

            if (analysisMode == AnalysisMode.QA)
            {
                WriteInfo("Creating Quality Issues Summary...");
                excelWriter.CreateQualityIssuesSheet(results);
                WriteInfo("Creating Statistics Dashboard...");
                excelWriter.CreateStatisticsDashboard(results, startTime, endTime);
            }
            else
            {
                WriteInfo("Creating Coverage Gap Analysis...");
                excelWriter.CreateCoverageGapSheet(results, requirements);
                WriteInfo("Creating BA Statistics Dashboard...");
                excelWriter.CreateBAStatisticsDashboard(
                    results,
                    requirements,
                    results.Sum(r => r.Tokens),
                    cacheHits,
                    endTime - startTime);
            }
        }
        // ============================================================
        // BATCH MODE — receives everything from FileSelector.
        // No arg parsing here. BatchProcessor gets what it needs directly.
        // ============================================================
        private static async Task RunBatchModeAsync(Configuration appConfig, PromptConfig promptConfig, SelectionResult selection, bool resume = false, IServiceProvider? serviceProvider = null)
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
            var batchProcessor = serviceProvider?.GetRequiredService<BatchProcessor>()
                ?? new BatchProcessor(appConfig, promptConfig);
            var sharedCache = new TestCaseCache();
            _activeCache = sharedCache;
            _activeReqCache = new RequirementCache();

            try
            {
                int? limitParam = (testLimit == 0) ? null : (int?)testLimit;

                AnalysisMode batchMode = selection.SelectedAnalysisMode;

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
        private static (Configuration? appConfig, PromptConfig? promptConfig) LoadConfiguration()
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
            };

            var promptConfig = new PromptConfig
            {
                MaxTokens = int.Parse(configBuilder["MaxTokens"] ?? "150"),
                Model = configBuilder["Model"] ?? "gpt-4o-mini",
                Temperature = double.Parse(configBuilder["Temperature"] ?? "0.2"),
                SystemMessage = configBuilder["SystemMessage"] ?? "You are an expert QA analyzer.",
                UserTemplate = configBuilder["UserTemplate"] ?? "Analyze: {Scenario}",
                CostPerToken = double.Parse(configBuilder["CostPerToken"] ?? "0.00000015"),

                // GEN Mode fields
                GenModel = configBuilder["GenModel"] ?? "gpt-4.1-mini",
                GenSystemMessage = configBuilder["GenSystemMessage"] ?? "",
                GenUserTemplate = configBuilder["GenUserTemplate"] ?? "",
                CritiqueSystemMessage = configBuilder["CritiqueSystemMessage"] ?? "",
                CritiqueUserTemplate = configBuilder["CritiqueUserTemplate"] ?? "",
                RefineSystemMessage = configBuilder["RefineSystemMessage"] ?? "",
                RefineUserTemplate = configBuilder["RefineUserTemplate"] ?? ""
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
        private static async Task<bool> ValidateConfigurationAsync(Configuration appConfig, PromptConfig promptConfig, string excelPath, int worksheetIndex)
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
        private static void DisplayHelp()
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
            Console.WriteLine("  dotnet run -- --test-gen          # Test GEN Mode orchestrator");
            Console.WriteLine("  dotnet run -- --gen-mode          # 🆕 Launch GEN Mode directly");
            Console.WriteLine("  dotnet run -- --resume            # Resume interrupted batch run");
            Console.WriteLine("  dotnet run -- --format json               # Export results as JSON");
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
        private static void DisplayVersion()
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
        private static void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✅ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private static void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("⚠️  ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("❌ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private static void WriteInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("📊 ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private static void WriteHeader(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ResetColor();
        }

        // ============================================================
        // HELPER: Build DI service container
        // ============================================================
        private static IServiceProvider BuildServiceProvider(
            Configuration appConfig, PromptConfig promptConfig)
        {
            var services = new ServiceCollection();

            // Register config as singletons
            services.AddSingleton(appConfig);
            services.AddSingleton(promptConfig);

            // Register core services against their interfaces
            services.AddSingleton<IAIAnalyzer>(
                _ => new AIAnalyzer(appConfig, promptConfig));
            services.AddSingleton<ITestCaseCache, TestCaseCache>();
            services.AddSingleton<IRequirementExtractor>(
                _ => new RequirementExtractor(appConfig, promptConfig));
            services.AddSingleton<BatchProcessor>(
                _ => new BatchProcessor(appConfig, promptConfig));
            services.AddSingleton<ConfigurationValidator>(
                _ => new ConfigurationValidator(appConfig, new PromptConfig()));
            services.AddSingleton<GenModeOrchestrator>(sp =>
                new GenModeOrchestrator(
                    sp.GetRequiredService<IAIAnalyzer>(),
                    sp.GetRequiredService<ITestCaseCache>(),
                    promptConfig));

            return services.BuildServiceProvider();
        }
    }
}
