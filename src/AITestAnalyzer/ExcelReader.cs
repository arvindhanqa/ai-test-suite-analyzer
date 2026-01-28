using OfficeOpenXml;
using System;
using System.IO;

namespace AITestAnalyzer
{
    public class ExcelReader
    {

        private readonly string _excelPath;
        private readonly int _worksheetIndex;

        public ExcelReader(string excelPath, int worksheetIndex = 0)
        {
            _excelPath = excelPath;
            _worksheetIndex = worksheetIndex;
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
                    var worksheet = package.Workbook.Worksheets[_worksheetIndex]; // Sheet2
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
        // Read a single test case from specified row
        public TestCase ReadTestCase(int rowNumber)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_excelPath)))
                {
                    var worksheet = package.Workbook.Worksheets[_worksheetIndex];

                    // Check if row exists
                    if (rowNumber > worksheet.Dimension.End.Row)
                    {
                        return null; // Beyond last row
                    }

                    // Read values with null safety
                    string testId = worksheet.Cells[rowNumber, 1].Value?.ToString()?.Trim();
                    string feature = worksheet.Cells[rowNumber, 2].Value?.ToString()?.Trim();
                    string scenario = worksheet.Cells[rowNumber, 3].Value?.ToString()?.Trim();
                    string priority = worksheet.Cells[rowNumber, 4].Value?.ToString()?.Trim();
                    string steps = worksheet.Cells[rowNumber, 5].Value?.ToString()?.Trim();
                    string expectedResult = worksheet.Cells[rowNumber, 6].Value?.ToString()?.Trim();
                    string status = worksheet.Cells[rowNumber, 7].Value?.ToString()?.Trim();

                    // Skip if Test ID is empty (empty row)
                    if (string.IsNullOrWhiteSpace(testId))
                    {
                        return null;
                    }

                    // Create TestCase with default values for missing data
                    return new TestCase(
                        testId: testId,
                        feature: feature ?? "Not Specified",
                        scenario: scenario ?? "Not Specified",
                        priority: priority ?? "Medium",
                        steps: steps ?? "Not Specified",
                        expectedResult: expectedResult ?? "Not Specified",
                        status: status ?? "Not Run"
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"      ⚠️  Error reading row {rowNumber}: {ex.Message}");
                return null; // Return null on error, continue processing other tests
            }
        }

        // Validate Excel file structure
        public (bool isValid, string errorMessage) ValidateExcelStructure()
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_excelPath)))
                {
                    // Check if workbook has any worksheets
                    if (package.Workbook.Worksheets.Count == 0)
                    {
                        return (false, "Excel file has no worksheets");
                    }

                    var worksheet = package.Workbook.Worksheets[_worksheetIndex];

                    // Check if worksheet has any data
                    if (worksheet.Dimension == null)
                    {
                        return (false, "Excel worksheet is empty");
                    }

                    // Check minimum columns (need at least: Test ID, Feature, Scenario, Steps, Expected Result)
                    int colCount = worksheet.Dimension.End.Column;
                    if (colCount < 5)
                    {
                        return (false, $"Excel has only {colCount} columns, need at least 5 (Test ID, Feature, Scenario, Priority, Steps, Expected Result, Status)");
                    }

                    // Check header row exists
                    var testIdHeader = worksheet.Cells[1, 1].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(testIdHeader))
                    {
                        return (false, "First row (header) is empty. Expected column headers.");
                    }

                    // Check if there's at least one data row
                    int rowCount = worksheet.Dimension.End.Row;
                    if (rowCount < 2)
                    {
                        return (false, "Excel has only header row, no test cases found");
                    }

                    // All validations passed
                    return (true, "Excel structure is valid");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Error reading Excel file: {ex.Message}");
            }
        }
    }
}
