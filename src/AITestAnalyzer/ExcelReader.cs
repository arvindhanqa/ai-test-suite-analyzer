using OfficeOpenXml;
using System;
using System.IO;

namespace AITestAnalyzer
{
    public class ExcelReader
    {

        private readonly string _excelPath;
        public ExcelReader(string excelPath)
        {
            _excelPath = excelPath;
        }

        // ============================================================
        // METHOD: Count Total Test Rows in Excel
        // ============================================================
        public int CountTestRows()
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_excelPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2
                    int row = 2; // Start from first data row (row 1 is header)
                    int count = 0;

                    // Count rows until we hit an empty Test ID
                    while (worksheet.Cells[row, 1].Value != null &&
                           !string.IsNullOrWhiteSpace(worksheet.Cells[row, 1].Value.ToString()))
                    {
                        count++;
                        row++;
                    }

                    return count;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: Could not count rows in Excel: {ex.Message}");
                return 0;
            }
        }

        // ============================================================
        // METHOD 2: Read Test Case from Excel
        // ============================================================
        public TestCase ReadTestCase(int rowNumber = 2)
        {
            if (!File.Exists(_excelPath))
            {
                Console.WriteLine($"❌ ERROR: Excel file not found at: {_excelPath}");
                return null;
            }

            try
            {
                using (var package = new ExcelPackage(new FileInfo(_excelPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2

                    // Check if row is empty
                    if (worksheet.Cells[rowNumber, 1].Value == null ||
                        string.IsNullOrWhiteSpace(worksheet.Cells[rowNumber, 1].Value.ToString()))
                    {
                        return null;
                    }

                    var testCase = new TestCase
                    {
                        TestId = worksheet.Cells[rowNumber, 1].Value?.ToString() ?? "",
                        Feature = worksheet.Cells[rowNumber, 2].Value?.ToString() ?? "",
                        Scenario = worksheet.Cells[rowNumber, 3].Value?.ToString() ?? "",
                        Priority = worksheet.Cells[rowNumber, 4].Value?.ToString() ?? "",
                        Steps = worksheet.Cells[rowNumber, 5].Value?.ToString() ?? "",
                        ExpectedResult = worksheet.Cells[rowNumber, 6].Value?.ToString() ?? "",
                        Status = worksheet.Cells[rowNumber, 7].Value?.ToString() ?? ""
                    };

                    return testCase;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Excel Error: {ex.Message}");
                return null;
            }
        }
    }
}
