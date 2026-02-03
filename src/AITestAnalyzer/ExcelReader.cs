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

        /// <summary>
        /// Counts the number of test case rows in the Excel worksheet
        /// </summary>
        /// <returns>
        /// Number of test cases found (rows with non-empty Test ID in column 1).
        /// Returns 0 if no tests found or if an error occurs during counting.
        /// </returns>
        /// <remarks>
        /// BEHAVIOR:
        /// - Starts counting from row 2 (row 1 is header)
        /// - Continues until finding empty Test ID in column 1
        /// - Stops at first empty row (doesn't scan entire sheet)
        /// - Returns 0 on error (logs error message to console)
        /// 
        /// EXAMPLE:
        /// - Rows 1-57: Test ID present → returns 56
        /// - Rows 1-2 only (header + 1 test) → returns 1
        /// - Empty sheet or header only → returns 0
        /// </remarks>
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

        /// <summary>
        /// Reads a single test case from specified Excel row
        /// </summary>
        /// <param name="rowNumber">Excel row number to read (1-based indexing). Row 1 is header, row 2 is first test case. Example: rowNumber=2 reads first data row.</param>
        /// <returns>
        /// TestCase object populated with data from the specified row.
        /// Returns null if row is empty (no Test ID), beyond last row, or if error occurs during reading.
        /// </returns>
        /// <remarks>
        /// BEHAVIOR:
        /// - Checks for null or empty values in each Excel cell
        /// - Assigns default values if data missing (e.g., "Not Specified" for empty Feature)
        /// - Returns null if Test ID cell is empty (indicates empty row)
        /// - Handles exceptions gracefully and logs errors to console
        /// 
        /// COLUMN MAPPING:
        /// - Column 1: Test ID (required - null if empty)
        /// - Column 2: Feature (default: "Not Specified")
        /// - Column 3: Scenario (default: "Not Specified")
        /// - Column 4: Priority (default: "Medium")
        /// - Column 5: Steps (default: "Not Specified")
        /// - Column 6: Expected Result (default: "Not Specified")
        /// - Column 7: Status (default: "Not Run")
        /// </remarks>
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

        /// <summary>
        /// Validates that the Excel file has correct structure for test case analysis
        /// </summary>
        /// <returns>
        /// Tuple containing:
        /// - isValid: true if file structure is valid, false otherwise
        /// - errorMessage: descriptive error message if invalid, "Excel structure is valid" if valid
        /// </returns>
        /// <remarks>
        /// Validation checks performed:
        /// - File has at least one worksheet
        /// - Worksheet has data (not empty)
        /// - Minimum 5 columns present (Test ID, Feature, Scenario, Steps, Expected Result)
        /// - Header row exists (row 1)
        /// - At least one data row exists (row 2+)
        /// 
        /// This method does NOT validate test case content, only file structure.
        /// Call before processing to fail fast on malformed files.
        /// </remarks>
        /// <exception cref="IOException">Thrown if file is locked by another program</exception>
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
