using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static AITestAnalyzer.BatchProcessor;

namespace AITestAnalyzer
{
    /// <summary>
    /// Handles batch processing of multiple Excel files
    /// </summary>
    public class BatchProcessor
    {
        private readonly Configuration _config;
        private readonly PromptConfig _promptConfig;
        private const int CACHE_MAX_AGE_DAYS = Constants.CACHE_MAX_AGE_DAYS;

        public BatchProcessor(Configuration config, PromptConfig promptConfig)
        {
            _config = config;
            _promptConfig = promptConfig;
        }

        // ============================================================
        // Data class to hold results for each file
        // ============================================================
        public class FileResult
        {
            public string FileName { get; set; } = "";
            public string OutputPath { get; set; } = "";
            public int TotalTests { get; set; }
            public int GoodTests { get; set; }
            public int IssueTests { get; set; }
            public int ErrorTests { get; set; }
            public int TotalTokens { get; set; }
            public double TotalCost { get; set; }
            public double TimeTaken { get; set; }
            public int CacheHits { get; set; }
            public int ApiCalls { get; set; }
            public double QualityScore => TotalTests > 0 ? (GoodTests * 100.0 / TotalTests) : 0;
        }

        // ============================================================
        // METHOD 1: Get all Excel files from a folder
        // ============================================================
        public static List<string> GetExcelFiles(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            var excelFiles = Directory.GetFiles(folderPath, "*.xlsx")
                .Where(f => !Path.GetFileName(f).StartsWith("~$")) // Exclude temp files
                .Where(f => !Path.GetFileName(f).StartsWith("analysis_results_")) // Exclude previous outputs
                .Where(f => !Path.GetFileName(f).Contains("_analysis_")) // Exclude batch outputs
                .OrderBy(f => f)
                .ToList();

            return excelFiles;
        }

        // ============================================================
        // METHOD 2: Process a single file — orchestrator only
        // ============================================================
        public async Task<FileResult> ProcessSingleFileAsync(
            string inputPath,
            string outputDir,
            int? testLimit = null,
            int worksheetIndex = 0,
            bool useCache = true,
            TestCaseCache? sharedCache = null,
            AnalysisMode analysisMode = AnalysisMode.BA)
        {
            var result = new FileResult { FileName = Path.GetFileName(inputPath) };
            var fileStartTime = DateTime.Now;

            try
            {
                WriteInfo($"\n{'═',60}");
                WriteHeader($"📁 Processing: {result.FileName}");
                WriteInfo($"{'═',60}");

                // Prepare output file
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseName = Path.GetFileNameWithoutExtension(inputPath);
                string outputPath = Path.Combine(outputDir, $"{baseName}_analysis_{timestamp}.xlsx");
                File.Copy(inputPath, outputPath, overwrite: true);
                result.OutputPath = outputPath;
                WriteSuccess($"Output file: {Path.GetFileName(outputPath)}");

                // Initialize components
                var excelReader = new ExcelReader(inputPath, worksheetIndex);
                var excelWriter = new ExcelWriter(outputPath, _promptConfig, worksheetIndex);
                var aiAnalyzer = new AIAnalyzer(_config, _promptConfig);

                // Load requirements (BA mode only)
                var requirements = await LoadBatchRequirementsAsync(inputPath, analysisMode);

                // Validate Excel structure
                var (excelIsValid, validationMessage) = excelReader.ValidateExcelStructure();
                if (!excelIsValid)
                {
                    WriteError($"Validation failed: {validationMessage}");
                    return result;
                }

                // Resolve test count
                int totalRowsInExcel = excelReader.CountTestRows();
                int testsToAnalyze = Math.Min(testLimit ?? totalRowsInExcel, totalRowsInExcel);
                WriteInfo($"Tests in file: {totalRowsInExcel}, Analyzing: {testsToAnalyze}");

                // Prepare Excel output
                excelWriter.RenameOriginalSheet();
                excelWriter.AddAnalysisColumnHeader(analysisMode);

                // Initialize cache
                TestCaseCache? cache = sharedCache ?? (useCache ? new TestCaseCache() : null);

                // Process all tests
                var (results, cacheHits, apiCalls) = await ProcessBatchTestsAsync(
                    excelReader, excelWriter, aiAnalyzer, cache,
                    requirements, analysisMode, useCache, testsToAnalyze, fileStartTime);

                var endTime = DateTime.Now;

                // Create output sheets
                CreateBatchOutputSheets(excelWriter, results, requirements,
                    analysisMode, fileStartTime, endTime, cacheHits);

                // Calculate and return file result
                CalculateFileResult(result, results, analysisMode, cacheHits, apiCalls, fileStartTime, endTime);

                WriteSuccess($"Completed: {result.FileName}");
                WriteInfo($"   Quality: {result.QualityScore:F1}% | Tests: {result.TotalTests} | Cache: {cacheHits}/{result.TotalTests} | Cost: ${result.TotalCost:F6}");
            }
            catch (Exception ex)
            {
                WriteError($"Failed to process {result.FileName}: {ex.Message}");
            }

            return result;
        }

        // ============================================================
        // HELPER: Load requirements for BA mode batch processing
        // ============================================================
        private async Task<List<ExtractedRequirement>> LoadBatchRequirementsAsync(
            string inputPath, AnalysisMode analysisMode)
        {
            if (analysisMode == AnalysisMode.QA)
            {
                WriteInfo("QA MODE: Skipping requirements (not needed)");
                return new List<ExtractedRequirement>();
            }

            var reqCache = new RequirementCache();
            var reqExtractor = new RequirementExtractor(_config, _promptConfig);

            string testFileName = Path.GetFileNameWithoutExtension(inputPath);
            string reqFileName = testFileName.Replace("test_cases_", "requirements_") + ".md";
            string dataFolder = Path.GetDirectoryName(inputPath) ?? ".";
            string reqPath = Path.Combine(dataFolder, reqFileName);

            if (!File.Exists(reqPath))
            {
                Console.WriteLine($"⚠️  Auto-detection failed. Could not find: {reqFileName}");
                Console.Write("📁 Enter requirement file path (or press Enter to skip): ");
                string? userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    Console.WriteLine("⚠️  No requirements provided. Analysis will be quality-only.");
                    return new List<ExtractedRequirement>();
                }

                reqPath = userInput;
            }
            else
            {
                Console.WriteLine($"✅ Auto-detected requirement file: {Path.GetFileName(reqPath)}");
            }

            try
            {
                return await reqExtractor.ExtractRequirementsAsync(reqPath, reqCache);
            }
            catch
            {
                return new List<ExtractedRequirement>();
            }
        }

        // ============================================================
        // HELPER: Process all tests in a single batch file
        // ============================================================
        private async Task<(List<(string TestId, string Result, int Tokens, string Coverage)> results, int cacheHits, int apiCalls)>
            ProcessBatchTestsAsync(
                ExcelReader excelReader, ExcelWriter excelWriter, AIAnalyzer aiAnalyzer,
                TestCaseCache? cache, List<ExtractedRequirement> requirements,
                AnalysisMode analysisMode, bool useCache, int testsToAnalyze, DateTime fileStartTime)
        {
            var results = new List<(string TestId, string Result, int Tokens, string Coverage)>();
            var progressTracker = new ProgressTracker(testsToAnalyze, fileStartTime);
            int cacheHits = 0;
            int apiCalls = 0;

            var testCases = excelReader.ReadAllTestCases(testsToAnalyze);

            for (int i = 0; i < testCases.Count; i++)
            {
                int rowNumber = i + 2;

                try
                {
                    var testCase = testCases[i];
                    string baseHash = cache?.GenerateHash(testCase) ?? "";
                    string cacheHash = analysisMode == AnalysisMode.QA ? baseHash : "ba_" + baseHash;

                    string quality;
                    string coverage;
                    int tokens;

                    if (useCache && cache != null && cache.TryGetCached(cacheHash, out CachedResult? cachedResult, CACHE_MAX_AGE_DAYS))
                    {
                        quality = cachedResult!.Quality;
                        coverage = cachedResult.Coverage;
                        tokens = 0;
                        cacheHits++;
                    }
                    else
                    {
                        (quality, coverage, tokens) = await CallAIAsync(aiAnalyzer, testCase, requirements, analysisMode);

                        if (useCache && cache != null)
                            cache.AddToCache(testCase.TestId, cacheHash, quality, coverage, tokens);

                        apiCalls++;
                        await Task.Delay(1000);
                    }

                    excelWriter.WriteAnalysis(rowNumber, quality, coverage, analysisMode);
                    results.Add((testCase.TestId, quality, tokens, coverage));
                    progressTracker.DisplayProgress(i + 1, testCase.TestId);
                }
                catch (Exception ex)
                {
                    results.Add(($"Row{rowNumber}", $"ERROR: {ex.Message}", 0, ""));
                    excelWriter.WriteAnalysis(rowNumber, $"ERROR: {ex.Message}", "None", analysisMode);
                }
            }

            progressTracker.Complete();
            excelWriter.FlushAnalysis();

            return (results, cacheHits, apiCalls);
        }

        // ============================================================
        // HELPER: Call AI for QA or BA mode — single test
        // ============================================================
        private async Task<(string quality, string coverage, int tokens)> CallAIAsync(
            AIAnalyzer aiAnalyzer, TestCase testCase,
            List<ExtractedRequirement> requirements, AnalysisMode analysisMode)
        {
            if (analysisMode == AnalysisMode.QA)
            {
                var (quality, tokens) = await aiAnalyzer.AnalyzeTestQualityAsync(testCase);
                return (quality, "", tokens);
            }
            else
            {
                var (reqFeedback, coverageIds, tokensUsed) = await aiAnalyzer.AnalyzeCoverageAndFeedbackAsync(testCase, requirements);
                return (reqFeedback, string.Join(", ", coverageIds), tokensUsed);
            }
        }

        // ============================================================
        // HELPER: Create output sheets after batch file processing
        // ============================================================
        private void CreateBatchOutputSheets(
            ExcelWriter excelWriter,
            List<(string TestId, string Result, int Tokens, string Coverage)> results,
            List<ExtractedRequirement> requirements,
            AnalysisMode analysisMode, DateTime startTime, DateTime endTime, int cacheHits)
        {
            if (analysisMode == AnalysisMode.QA)
            {
                excelWriter.CreateQualityIssuesSheet(results);
                excelWriter.CreateStatisticsDashboard(results, startTime, endTime);
            }
            else
            {
                WriteInfo("Creating Coverage Gap Analysis...");
                excelWriter.CreateCoverageGapSheet(results, requirements);
                WriteInfo("Creating BA Statistics Dashboard...");
                excelWriter.CreateBAStatisticsDashboard(
                    results, requirements,
                    results.Sum(r => r.Tokens),
                    cacheHits,
                    endTime - startTime);
            }
        }

        // ============================================================
        // HELPER: Calculate FileResult stats after processing
        // ============================================================
        private void CalculateFileResult(
            FileResult result,
            List<(string TestId, string Result, int Tokens, string Coverage)> results,
            AnalysisMode analysisMode, int cacheHits, int apiCalls,
            DateTime startTime, DateTime endTime)
        {
            result.TotalTests = results.Count;

            if (analysisMode == AnalysisMode.BA)
            {
                result.GoodTests = results.Count(r => !string.IsNullOrWhiteSpace(r.Coverage));
                result.IssueTests = results.Count(r => string.IsNullOrWhiteSpace(r.Coverage) && !r.Result.StartsWith("ERROR:"));
            }
            else
            {
                result.GoodTests = results.Count(r => r.Result == "GOOD");
                result.IssueTests = results.Count(r => r.Result != "GOOD" && !r.Result.StartsWith("ERROR:"));
            }

            result.ErrorTests = results.Count(r => r.Result.StartsWith("ERROR:"));
            result.TotalTokens = results.Sum(r => r.Tokens);
            result.TotalCost = result.TotalTokens * _promptConfig.CostPerToken;
            result.TimeTaken = (endTime - startTime).TotalSeconds;
            result.CacheHits = cacheHits;
            result.ApiCalls = apiCalls;
        }

        // ============================================================
        // METHOD 3: Process all files in a folder (BATCH MODE)
        // ============================================================
        /// <summary>
        /// Processes all Excel test files in a folder and generates individual analysis reports with aggregate statistics
        /// </summary>
        /// <param name="folderPath">Path to folder containing Excel test files (.xlsx format)</param>
        /// <param name="testLimitPerFile">Number of tests to analyze per file (null = analyze all tests in each file)</param>
        /// <param name="worksheetIndex">Zero-based worksheet index to read from (0=Sheet1, 1=Sheet2, etc.)</param>
        /// <param name="useCache">If true, uses cached results for unchanged tests to save API costs. If false, forces re-analysis of all tests</param>
        /// <returns>
        /// List of FileResult objects, one per processed Excel file.
        /// Each FileResult contains: FileName, OutputPath, TotalTests, TotalTokens, TotalCost, TimeTaken, CacheHits, ApiCalls, QualityScore.
        /// Returns empty list if no Excel files found in folder.
        /// </returns>
        /// <remarks>
        /// PERFORMANCE: Files are processed sequentially (not parallel) to avoid OpenAI rate limits.
        /// Each file gets 2-second pause before next file starts.
        /// 
        /// CACHING: Uses shared cache across all files. If File1 has TC-001 and File2 also has TC-001
        /// with same content, the second one is instant + free (cache hit).
        /// 
        /// OUTPUT: Each input file gets separate timestamped output file in ./output/ folder.
        /// Original input files are NEVER modified. Format: {originalname}_analysis_{timestamp}.xlsx
        /// 
        /// FAILURE HANDLING: If one file fails validation, it's skipped and processing continues
        /// with remaining files. Batch summary shows which files succeeded/failed.
        /// </remarks>
        /// <exception cref="DirectoryNotFoundException">Thrown when folderPath doesn't exist</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when folder access is denied (permissions issue)</exception>
        public async Task<List<FileResult>> ProcessBatchAsync(
            string folderPath,
            int? testLimitPerFile = null,
            int worksheetIndex = 0,
            bool useCache = true,
            AnalysisMode analysisMode = AnalysisMode.BA,
            TestCaseCache? externalCache = null,
            bool resume = false)
        {
            var allResults = new List<FileResult>();
            var batchStartTime = DateTime.Now;

            // Get all Excel files
            var excelFiles = GetExcelFiles(folderPath);

            if (excelFiles.Count == 0)
            {
                WriteWarning("No Excel files found in the specified folder.");
                WriteInfo("Make sure the folder contains .xlsx files that are not:");
                WriteInfo("  - Temporary files (starting with ~$)");
                WriteInfo("  - Previous output files (containing '_analysis_')");
                return allResults;
            }

            // Create output directory
            string outputDir = ExcelWriter.CreateOutputFolder();
            // ── Checkpoint setup ────────────────────────────────────────
            var checkpointManager = new CheckpointManager(folderPath);
            BatchCheckpoint checkpoint;

            if (resume && checkpointManager.CheckpointExists())
            {
                checkpoint = checkpointManager.Load() ?? new BatchCheckpoint
                {
                    BatchId = $"batch_{DateTime.Now:yyyyMMdd_HHmmss}",
                    FolderPath = folderPath,
                    TotalFiles = excelFiles.Count,
                    StartedAt = DateTime.Now
                };

                int skipping = checkpoint.CompletedFileNames.Count;
                WriteSuccess($"Resuming batch — {skipping} file(s) already completed, skipping them.");
            }
            else
            {
                // Fresh run — create new checkpoint
                checkpoint = new BatchCheckpoint
                {
                    BatchId = $"batch_{DateTime.Now:yyyyMMdd_HHmmss}",
                    FolderPath = folderPath,
                    TotalFiles = excelFiles.Count,
                    StartedAt = DateTime.Now
                };

                if (checkpointManager.CheckpointExists())
                    checkpointManager.Delete(); // Clean stale checkpoint from previous run
            }

            // Initialize shared cache for batch processing
            TestCaseCache? sharedCache = externalCache;
            if (useCache && sharedCache == null)
            {
                sharedCache = new TestCaseCache();
                int cacheSize = sharedCache.GetCacheSize();
                if (cacheSize > 0)
                {
                    WriteSuccess($"Loaded shared cache with {cacheSize} entries");
                }
            }

            WriteHeader("\n" + new string('═', 70));
            WriteHeader("🚀 BATCH PROCESSING MODE");
            WriteHeader(new string('═', 70));
            WriteInfo($"Source folder: {Path.GetFullPath(folderPath)}");
            WriteInfo($"Files found: {excelFiles.Count}");
            WriteInfo($"Output folder: {Path.GetFullPath(outputDir)}");
            WriteInfo($"Cache: {(useCache ? "Enabled" : "Disabled")}");
            if (testLimitPerFile.HasValue)
            {
                WriteInfo($"Test limit per file: {testLimitPerFile}");
            }
            WriteHeader(new string('═', 70));

            // List files to be processed
            Console.WriteLine();
            WriteInfo("Files to process:");
            foreach (var file in excelFiles)
            {
                Console.WriteLine($"   📄 {Path.GetFileName(file)}");
            }
            Console.WriteLine();

            // Process each file
            int fileNumber = 0;
            foreach (var filePath in excelFiles)
            {
                fileNumber++;
                string fileName = Path.GetFileName(filePath);

                // ── Resume: skip already-completed files ──────────────────
                if (checkpoint.CompletedFileNames.Contains(fileName))
                {
                    WriteInfo($"Skipping (already done): {fileName}");
                    continue;
                }

                Console.WriteLine();
                WriteHeader(new string('─', 60));
                WriteHeader($"📁 File {fileNumber} of {excelFiles.Count}: {fileName}");
                WriteHeader($"   Remaining after this: {excelFiles.Count - fileNumber} file(s)");
                WriteHeader(new string('─', 60));

                var result = await ProcessSingleFileAsync(
                    filePath,
                    outputDir,
                    testLimitPerFile,
                    worksheetIndex,
                    useCache,
                    sharedCache,
                    analysisMode);

                allResults.Add(result);

                // ── Save checkpoint only if file succeeded ────────────────
                if (!string.IsNullOrEmpty(result.OutputPath))
                {
                    checkpoint.CompletedFileNames.Add(fileName);
                    checkpointManager.Save(checkpoint);
                }
                else
                {
                    WriteWarning($"File had errors — not checkpointed, will retry on --resume: {fileName}");
                }

                if (fileNumber < excelFiles.Count)
                {
                    WriteInfo("Pausing before next file...");
                    await Task.Delay(2000);
                }
            }

            // ── All done — clean up checkpoint ───────────────────────────
            checkpointManager.Delete();
            WriteSuccess("Checkpoint cleared — batch complete.");

            // Save shared cache after all files are processed
            if (useCache && sharedCache != null)
            {
                WriteInfo("\nSaving cache...");
                int cleaned = sharedCache.CleanExpiredEntries(CACHE_MAX_AGE_DAYS);
                if (cleaned > 0)
                {
                    WriteInfo($"Cleaned {cleaned} expired cache entries");
                }
                sharedCache.SaveCache();
                WriteSuccess("Cache saved successfully");
            }

            // Display batch summary
            var batchEndTime = DateTime.Now;
            DisplayBatchSummary(allResults, batchStartTime, batchEndTime, useCache, analysisMode);

            return allResults;
        }

        // ============================================================
        // METHOD 4: Display combined batch summary
        // ============================================================
        private void DisplayBatchSummary(List<FileResult> results, DateTime startTime, DateTime endTime, bool cacheEnabled, AnalysisMode analysisMode)
        {
            var totalTime = (endTime - startTime).TotalSeconds;

            WriteHeader("\n" + new string('═', 70));
            WriteHeader("📊 BATCH PROCESSING SUMMARY");
            WriteHeader(new string('═', 70));
            string colLabel = analysisMode == AnalysisMode.BA ? "Coverage" : "Quality ";
            // Per-file summary table
            Console.WriteLine("\n┌─────────────────────────────────┬────────┬─────────┬────────┬──────────┬────────────┐");
            Console.WriteLine($"│ File                            │ Tests  │ {colLabel} │ Cache  │ Tokens   │ Cost       │");
            Console.WriteLine("├─────────────────────────────────┼────────┼─────────┼────────┼──────────┼────────────┤");

            foreach (var result in results)
            {
                string fileName = result.FileName.Length > 31
                    ? result.FileName.Substring(0, 28) + "..."
                    : result.FileName.PadRight(31);

                string quality = analysisMode == AnalysisMode.BA ? "  N/A   " : $"{result.QualityScore:F1}%";
                string cacheInfo = $"{result.CacheHits}/{result.TotalTests}";

                Console.Write($"│ {fileName} │ ");
                Console.Write($"{result.TotalTests,6} │ ");

                // Color-coded quality score
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = result.QualityScore >= 80 ? ConsoleColor.Green :
                                         result.QualityScore >= 50 ? ConsoleColor.Yellow : ConsoleColor.Red;
                Console.Write($"{quality,7}");
                Console.ForegroundColor = originalColor;

                Console.Write($" │ {cacheInfo,6} │ ");
                Console.WriteLine($"{result.TotalTokens,8:N0} │ ${result.TotalCost:F6} │");
            }

            Console.WriteLine("└─────────────────────────────────┴────────┴─────────┴────────┴──────────┴────────────┘");

            // Aggregate statistics
            int totalTests = results.Sum(r => r.TotalTests);
            int totalGood = results.Sum(r => r.GoodTests);
            int totalIssues = results.Sum(r => r.IssueTests);
            int totalErrors = results.Sum(r => r.ErrorTests);
            int totalTokens = results.Sum(r => r.TotalTokens);
            double totalCost = results.Sum(r => r.TotalCost);
            int totalCacheHits = results.Sum(r => r.CacheHits);
            int totalApiCalls = results.Sum(r => r.ApiCalls);
            double overallQuality = totalTests > 0 ? (totalGood * 100.0 / totalTests) : 0;

            WriteHeader("\n📈 AGGREGATE STATISTICS");
            Console.WriteLine($"   Files processed:      {results.Count}");
            Console.WriteLine($"   Total tests:          {totalTests}");

            Console.Write($"   Overall quality:      ");
            var color = Console.ForegroundColor;
            Console.ForegroundColor = overallQuality >= 80 ? ConsoleColor.Green :
                                     overallQuality >= 50 ? ConsoleColor.Yellow : ConsoleColor.Red;
            Console.WriteLine($"{overallQuality:F1}%");
            Console.ForegroundColor = color;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   ✅ Good tests:        {totalGood} ({(totalTests > 0 ? totalGood * 100.0 / totalTests : 0):F1}%)");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"   ⚠️  Tests with issues: {totalIssues} ({(totalTests > 0 ? totalIssues * 100.0 / totalTests : 0):F1}%)");
            Console.ResetColor();

            if (totalErrors > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"   ❌ Errors:            {totalErrors} ({(totalTests > 0 ? totalErrors * 100.0 / totalTests : 0):F1}%)");
                Console.ResetColor();
            }

            // Cache statistics
            if (cacheEnabled)
            {
                WriteHeader("\n💾 CACHE PERFORMANCE");
                double cacheHitRate = totalTests > 0 ? (totalCacheHits * 100.0 / totalTests) : 0;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"   ✅ Cache hits:        {totalCacheHits} ({cacheHitRate:F1}%)");
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"   🤖 API calls:         {totalApiCalls} ({(100 - cacheHitRate):F1}%)");
                Console.ResetColor();

                if (totalCacheHits > 0)
                {
                    int savedTokens = totalCacheHits * Constants.ESTIMATED_TOKENS_PER_CACHED_TEST; // Estimated
                    double savedCost = savedTokens * _promptConfig.CostPerToken;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"   💰 Tokens saved:      ~{savedTokens:N0}");
                    Console.WriteLine($"   💵 Cost saved:        ~${savedCost:F6}");
                    Console.ResetColor();
                }
            }

            WriteHeader("\n💰 COST SUMMARY");
            Console.WriteLine($"   Total tokens:         {totalTokens:N0}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"   Total cost:           ${totalCost:F6}");
            Console.ResetColor();
            Console.WriteLine($"   Avg cost/test:        ${(totalTests > 0 ? totalCost / totalTests : 0):F6}");
            Console.WriteLine($"   Avg cost/file:        ${(results.Count > 0 ? totalCost / results.Count : 0):F6}");

            WriteHeader("\n⏱️  TIME SUMMARY");
            Console.WriteLine($"   Total time:           {totalTime:F1} seconds ({totalTime / 60:F1} minutes)");
            Console.WriteLine($"   Avg time/file:        {(results.Count > 0 ? totalTime / results.Count : 0):F1} seconds");
            Console.WriteLine($"   Avg time/test:        {(totalTests > 0 ? totalTime / totalTests : 0):F2} seconds");

            WriteHeader("\n📁 OUTPUT FILES");
            foreach (var result in results)
            {
                if (!string.IsNullOrEmpty(result.OutputPath))
                {
                    Console.WriteLine($"   📄 {Path.GetFileName(result.OutputPath)}");
                }
            }

            WriteHeader("\n" + new string('═', 70));
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("🎉 BATCH PROCESSING COMPLETE!");
            Console.ResetColor();
            WriteHeader(new string('═', 70) + "\n");
        }

        // ============================================================
        // Color helper methods (matching Program.cs style)
        // ============================================================
        private void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("✅ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("⚠️  ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("❌ ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private void WriteInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("📊 ");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        private void WriteHeader(string message)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
