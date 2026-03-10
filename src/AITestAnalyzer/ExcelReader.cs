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
        /// Reads a single test case from specified row in Excel worksheet
        /// </summary>
        /// <param name="rowNumber">Excel row number to read (1-based, row 1 is header, data starts row 2)</param>
        /// <returns>
        /// TestCase object populated with values from Excel row, or null if:
        /// - rowNumber exceeds worksheet rows
        /// - Test ID cell (column 1) is empty/whitespace
        /// - Exception occurs reading the row
        /// </returns>
        /// <remarks>
        /// EXCEL COLUMN MAPPING (assumes 7-column structure):
        /// - Column 1: Test ID (required, row skipped if empty)
        /// - Column 2: Feature (defaults to "Not Specified" if null)
        /// - Column 3: Scenario (defaults to "Not Specified" if null)
        /// - Column 4: Priority (defaults to "Medium" if null)
        /// - Column 5: Steps (defaults to "Not Specified" if null)
        /// - Column 6: Expected Result (defaults to "Not Specified" if null)
        /// - Column 7: Status (defaults to "Not Run" if null)
        /// 
        /// NULL HANDLING: Uses null-coalescing operator (??) to provide default values for empty cells.
        /// Excel cells with no value return null from EPPlus library.
        /// 
        /// ERROR HANDLING: Returns null on exceptions with console warning.
        /// Allows batch processing to continue if individual test read fails.
        /// 
        /// EXAMPLE: ReadTestCase(2) reads first data row (row 1 is header).
        /// Test ID "TC-001" with empty Priority → TestCase with Priority="Medium"
        /// </remarks>
        public TestCase? ReadTestCase(int rowNumber)
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
                    string? testId = worksheet.Cells[rowNumber, 1].Value?.ToString()?.Trim();
                    string? feature = worksheet.Cells[rowNumber, 2].Value?.ToString()?.Trim();
                    string? scenario = worksheet.Cells[rowNumber, 3].Value?.ToString()?.Trim();
                    string? priority = worksheet.Cells[rowNumber, 4].Value?.ToString()?.Trim();
                    string? steps = worksheet.Cells[rowNumber, 5].Value?.ToString()?.Trim();
                    string? expectedResult = worksheet.Cells[rowNumber, 6].Value?.ToString()?.Trim();
                    string? status = worksheet.Cells[rowNumber, 7].Value?.ToString()?.Trim();

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
        /// Reads all test cases from the worksheet in a single file open operation.
        /// Use this instead of calling ReadTestCase() in a loop to avoid repeated file I/O.
        /// </summary>
        /// <param name="limit">Max test cases to read. 0 = read all.</param>
        /// <returns>List of TestCase objects. Empty list if file is unreadable.</returns>
        public List<TestCase> ReadAllTestCases(int limit = 0)
        {
            var testCases = new List<TestCase>();

            try
            {
                using (var package = new ExcelPackage(new FileInfo(_excelPath)))
                {
                    var worksheet = package.Workbook.Worksheets[_worksheetIndex];

                    if (worksheet.Dimension == null)
                        return testCases;

                    int lastRow = worksheet.Dimension.End.Row;
                    int row = 2; // Row 1 is header
                    int count = 0;

                    while (row <= lastRow)
                    {
                        if (limit > 0 && count >= limit)
                            break;

                        string? testId = worksheet.Cells[row, 1].Value?.ToString()?.Trim();

                        if (string.IsNullOrWhiteSpace(testId))
                            break; // Empty Test ID = end of data

                        testCases.Add(new TestCase(
                            testId: testId,
                            feature: worksheet.Cells[row, 2].Value?.ToString()?.Trim() ?? "Not Specified",
                            scenario: worksheet.Cells[row, 3].Value?.ToString()?.Trim() ?? "Not Specified",
                            priority: worksheet.Cells[row, 4].Value?.ToString()?.Trim() ?? "Medium",
                            steps: worksheet.Cells[row, 5].Value?.ToString()?.Trim() ?? "Not Specified",
                            expectedResult: worksheet.Cells[row, 6].Value?.ToString()?.Trim() ?? "Not Specified",
                            status: worksheet.Cells[row, 7].Value?.ToString()?.Trim() ?? "Not Run"
                        ));

                        count++;
                        row++;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR: Could not read test cases from Excel: {ex.Message}");
            }

            return testCases;
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
        public (bool isValid, string? errorMessage) ValidateExcelStructure()
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_excelPath)))
                {
                    if (package.Workbook.Worksheets.Count == 0)
                        return (false, "Excel file has no worksheets");

                    var worksheet = package.Workbook.Worksheets[_worksheetIndex];

                    if (worksheet.Dimension == null)
                        return (false, "Excel worksheet is empty");

                    int colCount = worksheet.Dimension.End.Column;
                    if (colCount < 5)
                        return (false, $"Excel has only {colCount} columns, need at least 5 (Test ID, Feature, Scenario, Priority, Steps, Expected Result, Status)");

                    // Check header row exists
                    var testIdHeader = worksheet.Cells[1, 1].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(testIdHeader))
                        return (false, "First row (header) is empty. Expected column headers.");

                    // Validate expected column header names
                    var expectedHeaders = new[]
                    {
                (col: 1, name: "Test ID"),
                (col: 2, name: "Feature"),
                (col: 3, name: "Scenario"),
                (col: 4, name: "Priority"),
                (col: 5, name: "Steps")
            };

                    foreach (var expected in expectedHeaders)
                    {
                        string? actual = worksheet.Cells[1, expected.col].Value?.ToString()?.Trim();
                        if (!string.Equals(actual, expected.name, StringComparison.OrdinalIgnoreCase))
                        {
                            return (false, $"Column {expected.col} header mismatch. Expected '{expected.name}', found '{actual ?? "empty"}'");
                        }
                    }

                    // Check at least one data row exists
                    int rowCount = worksheet.Dimension.End.Row;
                    if (rowCount < 2)
                        return (false, "Excel has only header row, no test cases found");

                    // Check TestId column has actual data in first data row
                    string? firstTestId = worksheet.Cells[2, 1].Value?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(firstTestId))
                        return (false, "Test ID column (column 1) has no data in first data row. Is this the right worksheet?");

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
