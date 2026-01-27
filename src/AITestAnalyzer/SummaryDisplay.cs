using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AITestAnalyzer
{
    public class SummaryDisplay
    {
        public static void Display(List<(string TestId, string Result, int Tokens)> results, DateTime startTime, DateTime endTime, string outputPath)
        {
            Console.WriteLine("===============================================");
            Console.WriteLine("📊 ANALYSIS SUMMARY");
            Console.WriteLine("===============================================");

            int totalTests = results.Count;
            int goodTests = results.Count(r => r.Result == "GOOD");
            int issueTests = totalTests - goodTests;
            int totalTokens = results.Sum(r => r.Tokens);
            double totalCost = totalTokens * 0.00000015;
            int avgTokens = totalTests > 0 ? totalTokens / totalTests : 0;
            var timeTaken = (endTime - startTime).TotalSeconds;

            Console.WriteLine($"Tests analyzed: {totalTests}");
            Console.WriteLine($"✅ Good tests: {goodTests} ({(totalTests > 0 ? goodTests * 100.0 / totalTests : 0):F0}%)");
            Console.WriteLine($"⚠️  Tests with issues: {issueTests} ({(totalTests > 0 ? issueTests * 100.0 / totalTests : 0):F0}%)");
            Console.WriteLine();
            Console.WriteLine($"Total tokens: {totalTokens:N0}");
            Console.WriteLine($"Total cost: ${totalCost:F6}");
            Console.WriteLine($"Avg tokens/test: {avgTokens}");
            Console.WriteLine($"⏱️  Time: {timeTaken:F1} seconds");
            Console.WriteLine();
            Console.WriteLine($"📁 Output: {Path.GetFileName(outputPath)}");
            Console.WriteLine($"   Location: {Path.GetDirectoryName(outputPath)}");
            Console.WriteLine("===============================================");
        }
    }
}