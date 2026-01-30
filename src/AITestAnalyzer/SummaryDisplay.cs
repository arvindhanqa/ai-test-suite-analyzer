using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AITestAnalyzer
{
    public class SummaryDisplay
    {
        public static void Display(List<(string TestId, string Result, int Tokens)> results,
                                  DateTime startTime, DateTime endTime, string outputPath)
        {
            int goodTests = results.Count(r => r.Result.StartsWith("GOOD", StringComparison.OrdinalIgnoreCase));
            int issueTests = results.Count - goodTests;
            int totalTokens = results.Sum(r => r.Tokens);
            double totalCost = totalTokens * 0.00000015;
            int avgTokens = results.Count > 0 ? totalTokens / results.Count : 0;
            var duration = (endTime - startTime).TotalSeconds;

            // Header with color
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("===============================================");
            Console.WriteLine("📊 ANALYSIS SUMMARY");
            Console.WriteLine("===============================================");
            Console.ResetColor();

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