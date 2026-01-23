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

            Console.WriteLine("=== AI Test Suite Analyzer - Day 4 ===");
            Console.WriteLine("Reading Excel file...\n");

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

                    string testId = worksheet.Cells[2, 1].Value?.ToString();

                    Console.WriteLine("Reading First Test Case ID:");
                    Console.WriteLine($"Cell [2,1] Value: {testId}");
                    Console.WriteLine("\nSuccess! Excel file read successfully! ✅");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}