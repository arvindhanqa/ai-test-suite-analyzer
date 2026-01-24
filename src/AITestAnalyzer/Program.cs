using System;
using System.IO;
using OfficeOpenXml;

namespace AITestAnalyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            ExcelPackage.License.SetNonCommercialPersonal("Aravindhan Rajasekaran");

            Console.WriteLine("=== AI Test Suite Analyzer - Day 5 ===");
            Console.WriteLine("Reading complete test case from Excel...\n");

            string excelPath = @"C:\Projects\ai-test-analyzer\ai-test-suite-analyzer\data\test_cases_shopease.xlsx";

            if (!File.Exists(excelPath))
            {
                Console.WriteLine($"ERROR: File not found at: {excelPath}");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            try
            {
                using (var package = new ExcelPackage(new FileInfo(excelPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1];

                    Console.WriteLine($"Worksheet Name: {worksheet.Name}");
                    Console.WriteLine($"Total Rows: {worksheet.Dimension.Rows}");
                    Console.WriteLine($"Total Columns: {worksheet.Dimension.Columns}\n");


                    // Read ENTIRE first test case (all 7 columns)
                    var testCase = new TestCase
                    {
                        TestId = worksheet.Cells[2, 1].Value?.ToString() ?? "",
                        Feature = worksheet.Cells[2, 2].Value?.ToString() ?? "",
                        Scenario = worksheet.Cells[2, 3].Value?.ToString() ?? "",
                        Priority = worksheet.Cells[2, 4].Value?.ToString() ?? "",
                        Steps = worksheet.Cells[2, 5].Value?.ToString() ?? "",
                        ExpectedResult = worksheet.Cells[2, 6].Value?.ToString() ?? "",
                        Status = worksheet.Cells[2, 7].Value?.ToString() ?? ""
                    };

                    // Display the complete test case
                    Console.WriteLine("=== COMPLETE TEST CASE LOADED ===");
                    Console.WriteLine($"Test ID: {testCase.TestId}");
                    Console.WriteLine($"Feature: {testCase.Feature}");
                    Console.WriteLine($"Scenario: {testCase.Scenario}");
                    Console.WriteLine($"Priority: {testCase.Priority}");
                    Console.WriteLine($"Steps:\n{testCase.Steps}");
                    Console.WriteLine($"Expected Result: {testCase.ExpectedResult}");
                    Console.WriteLine($"Status: {testCase.Status}");
                    Console.WriteLine("\n✅ Success! Complete test case read successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}