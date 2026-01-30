using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AITestAnalyzer
{
    public class SummaryDisplay
    {
        public static void Display(List<(string TestId, string Result, int Tokens)> results,
                                  DateTime startTime, DateTime endTime, string outputPath, int cacheHits, int apiCalls, bool cacheEnabled)
        {
            int goodTests = results.Count(r => r.Result.StartsWith("GOOD", StringComparison.OrdinalIgnoreCase));
            int issueTests = results.Count - goodTests;
            int totalTokens = results.Sum(r => r.Tokens);
            double totalCost = totalTokens * 0.00000015;
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
                    int avgTokensPerTest = 150; // Estimated average
                    int savedTokens = cacheHits * avgTokensPerTest;
                    double savedCost = savedTokens * 0.000000150; // GPT-4o-mini cost per token

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

            // Then the existing test quality section starts...
            Console.WriteLine($"Tests analyzed: {results.Count}");

            // Good tests in GREEN
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✅ Good tests: {goodTests} ({(goodTests * 100.0 / results.Count):F0}%)");
            Console.ResetColor();

            // Issues in YELLOW
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  Tests with issues: {issueTests} ({(issueTests * 100.0 / results.Count):F0}%)");
            Console.ResetColor();

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