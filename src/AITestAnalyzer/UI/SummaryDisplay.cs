using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AITestAnalyzer.Config;
using AITestAnalyzer.Models;

namespace AITestAnalyzer.UI
{
    public class SummaryDisplay
    {
        /// <summary>
        /// Displays comprehensive analysis summary to console with color-coded statistics and cache performance metrics
        /// </summary>
        /// <param name="results">
        /// List of analysis results (one tuple per test):
        /// - TestId: Test case identifier  
        /// - Result: AI feedback ("GOOD" or "Issue: ...")
        /// - Tokens: API tokens used (0 if cached)
        /// </param>
        /// <param name="startTime">Analysis start time (used to calculate total duration)</param>
        /// <param name="endTime">Analysis end time (used to calculate total duration)</param>
        /// <param name="outputPath">Path to generated Excel file (displayed to user)</param>
        /// <param name="cacheHits">Number of tests loaded from cache (instant + free)</param>
        /// <param name="apiCalls">Number of tests analyzed via OpenAI API (cost incurred)</param>
        /// <param name="cacheEnabled">Whether cache was enabled for this run (affects display)</param>
        /// <remarks>
        /// CONSOLE OUTPUT SECTIONS:
        /// 1. CACHE PERFORMANCE (if enabled):
        ///    - Cache hits with percentage (green)
        ///    - API calls with percentage (yellow)
        ///    - Tokens saved estimate (~150 per cached test)
        ///    - Cost savings calculation
        /// 
        /// 2. TEST QUALITY BREAKDOWN:
        ///    - Total tests analyzed
        ///    - Good tests count + percentage (green)
        ///    - Tests with issues count + percentage (yellow)
        /// 
        /// 3. COST & PERFORMANCE:
        ///    - Total tokens used
        ///    - Total cost (cyan, 6 decimal places)
        ///    - Average tokens per test
        ///    - Time taken (seconds with 1 decimal)
        /// 
        /// 4. OUTPUT FILE:
        ///    - Filename (green)
        ///    - Full directory path (dark gray)
        /// 
        /// COLOR CODING:
        /// - Green (✅): Success metrics, good tests, cache hits, output file
        /// - Yellow (⚠️): Warnings, tests with issues, API calls
        /// - Cyan (📊): Info headers, cost metrics
        /// 
        /// EXAMPLE OUTPUT:
        /// - 56 tests, 3 cache hits, 53 API calls, 100% issues → Shows all sections with appropriate colors
        /// - Cache disabled (--no-cache) → Shows warning instead of cache section
        /// </remarks>
        public static void Display(List<(string TestId, string Result, int Tokens, string Coverage)> results,
                                  DateTime startTime, DateTime endTime, string outputPath, int cacheHits, int apiCalls, bool cacheEnabled, PromptConfig promptConfig, AnalysisMode analysisMode = AnalysisMode.QA)
        {
            int goodTests = results.Count(r => r.Result.StartsWith("GOOD", StringComparison.OrdinalIgnoreCase));
            int issueTests = results.Count - goodTests;
            int totalTokens = results.Sum(r => r.Tokens);
            double totalCost = totalTokens * promptConfig.CostPerToken;
            int avgTokens = results.Count > 0 ? totalTokens / results.Count : 0;
            var duration = (endTime - startTime).TotalSeconds;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("📊 ANALYSIS SUMMARY");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.ResetColor();

            // ADD THIS ENTIRE CACHE SECTION:
            if (cacheEnabled)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine();
                Console.WriteLine("💾 CACHE PERFORMANCE");
                Console.WriteLine("───────────────────────────────────────────────");
                Console.ResetColor();

                int totalTests = results.Count;
                double cacheHitRate = totalTests > 0 ? (cacheHits * 100.0) / totalTests : 0;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("✅ ");
                Console.ResetColor();
                Console.WriteLine($"Cache hits: {cacheHits} ({cacheHitRate:F1}%)");

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("🤖 ");
                Console.ResetColor();
                Console.WriteLine($"API calls: {apiCalls} ({(100 - cacheHitRate):F1}%)");

                if (cacheHits > 0)
                {
                    // Calculate savings (approximate)
                    int avgTokensPerTest = Constants.ESTIMATED_TOKENS_PER_CACHED_TEST; // Estimated average
                    int savedTokens = cacheHits * avgTokensPerTest;
                    double savedCost = savedTokens * promptConfig.CostPerToken;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("💰 ");
                    Console.ResetColor();
                    Console.WriteLine($"Tokens saved: ~{savedTokens:N0}");

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("💵 ");
                    Console.ResetColor();
                    Console.WriteLine($"Cost saved: ~${savedCost:F6}");
                }

                Console.WriteLine();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine();
                Console.WriteLine("⚠️  Cache was disabled for this run (--no-cache flag)");
                Console.WriteLine();
                Console.ResetColor();
            }

            Console.WriteLine($"Tests analyzed: {results.Count}");

            if (analysisMode == AnalysisMode.QA)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Good tests: {goodTests} ({(goodTests * 100.0 / results.Count):F0}%)");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  Tests with issues: {issueTests} ({(issueTests * 100.0 / results.Count):F0}%)");
                Console.ResetColor();
            }
            else
            {
                int covered = results.Count(r => !string.IsNullOrWhiteSpace(r.Coverage) && r.Coverage != "None");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✅ Tests with coverage: {covered} ({(covered * 100.0 / results.Count):F0}%)");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"⚠️  Tests with no coverage: {results.Count - covered} ({((results.Count - covered) * 100.0 / results.Count):F0}%)");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine($"Total tokens: {totalTokens:N0}");

            // Cost in CYAN
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Total cost: ${totalCost:F6}");
            Console.ResetColor();

            Console.WriteLine($"Avg tokens/test: {avgTokens}");
            Console.WriteLine($"⏱️  Time: {duration:F1} seconds");

            Console.WriteLine();

            // Output path in GREEN
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"📁 Output: {Path.GetFileName(outputPath)}");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"   Location: {Path.GetDirectoryName(outputPath)}");
            Console.ResetColor();

            // Closing line
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================");
            Console.ResetColor();
        }
    }
}
