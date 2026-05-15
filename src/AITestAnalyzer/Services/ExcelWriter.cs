using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using AITestAnalyzer.Config;
using AITestAnalyzer.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace AITestAnalyzer.Services
{
    public class ExcelWriter : IExcelWriter
    {
        private readonly string _outputPath;
        private readonly int _worksheetIndex;
        private readonly PromptConfig _promptConfig;
        private readonly List<(int Row, string Analysis, string Coverage, AnalysisMode Mode)> _pendingWrites = new();

        public ExcelWriter(string outputPath, PromptConfig promptConfig, int worksheetIndex = 0)
        {
            _outputPath = outputPath;
            _promptConfig = promptConfig;
            _worksheetIndex = worksheetIndex;
        }

        // ============================================================
        // METHOD 1: Create Output Folder
        // ============================================================
        public static string CreateOutputFolder()
        {
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"   ✅ Created output directory");
            }

            return outputDir;
        }

        // ============================================================
        // METHOD 2: Prepare Output File (Copy Input + Timestamp)
        // ============================================================
        public static string PrepareOutputFile(string inputPath, string outputDir)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string outputFileName = $"analysis_results_{timestamp}.xlsx";
            string outputPath = Path.Combine(outputDir, outputFileName);

            // Copy input file to output location
            File.Copy(inputPath, outputPath, overwrite: true);

            Console.WriteLine($"   ✅ Output file: {outputFileName}");

            return outputPath;
        }

        // ============================================================
        // METHOD 3: Rename Original Sheet to "AI Detailed Analysis"
        // ============================================================
        public void RenameOriginalSheet()
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    var worksheet = package.Workbook.Worksheets[_worksheetIndex]; // Sheet2 (index 1)
                    worksheet.Name = "AI Detailed Analysis";
                    package.Save();
                    Console.WriteLine("   ✅ Renamed sheet to 'AI Detailed Analysis'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not rename sheet in '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }

        // ============================================================
        // METHOD 4: Add AI Analysis Column Header
        // ============================================================
        // Add analysis column header with formatting
        public void AddAnalysisColumnHeader(AnalysisMode mode = AnalysisMode.BA)
        {
            using (var package = new ExcelPackage(new FileInfo(_outputPath)))
            {
                var worksheet = package.Workbook.Worksheets[_worksheetIndex];

                if (mode == AnalysisMode.QA)
                {
                    // ============================================================
                    // QA MODE: 1 column only (AI Analysis)
                    // ============================================================

                    // Add "AI Analysis" header in column H (8th column)
                    var headerCell = worksheet.Cells[1, 8];
                    headerCell.Value = "AI Analysis";
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Font.Size = 12;
                    headerCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerCell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    headerCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    headerCell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                    // Freeze panes
                    worksheet.View.FreezePanes(2, 1);

                    // Auto-filter
                    worksheet.Cells[1, 1, 1, 8].AutoFilter = true;

                    // Auto-size columns 1-7
                    for (int col = 1; col <= 7; col++)
                    {
                        worksheet.Column(col).AutoFit();
                    }

                    // Set column H width
                    worksheet.Column(8).Width = 60;
                }
                else // BA Mode
                {
                    // ============================================================
                    // BA MODE: 2 columns (Requirement Feedback | Coverage)
                    // ============================================================

                    // Add "Requirement Feedback" header in column H (8th column)
                    var headerCell = worksheet.Cells[1, 8];
                    headerCell.Value = "Requirement Feedback";
                    headerCell.Style.Font.Bold = true;
                    headerCell.Style.Font.Size = 12;
                    headerCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    headerCell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightCoral);
                    headerCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    headerCell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                    // Add "Coverage" header in column I (9th column)
                    var coverageHeaderCell = worksheet.Cells[1, 9];
                    coverageHeaderCell.Value = "Coverage";
                    coverageHeaderCell.Style.Font.Bold = true;
                    coverageHeaderCell.Style.Font.Size = 12;
                    coverageHeaderCell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    coverageHeaderCell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                    coverageHeaderCell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    coverageHeaderCell.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                    // Freeze panes
                    worksheet.View.FreezePanes(2, 1);

                    // Auto-filter
                    worksheet.Cells[1, 1, 1, 9].AutoFilter = true;

                    // Auto-size columns 1-7
                    for (int col = 1; col <= 7; col++)
                    {
                        worksheet.Column(col).AutoFit();
                    }

                    // Set column widths
                    worksheet.Column(8).Width = 60; // Requirement Feedback
                    worksheet.Column(9).Width = 40; // Coverage
                }

                package.Save();
            }
        }

        public void WriteAnalysis(int rowNumber, string analysis, string coverage, AnalysisMode mode = AnalysisMode.BA)
        {
            _pendingWrites.Add((rowNumber, analysis, coverage, mode));
        }

        /// <summary>
        /// Writes all buffered analysis results to Excel in a single file open/save operation.
        /// Call once after the analysis loop completes. Clears the buffer after writing.
        /// </summary>
        public void FlushAnalysis()
        {
            if (_pendingWrites.Count == 0)
                return;

            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    var worksheet = package.Workbook.Worksheets[_worksheetIndex];

                    foreach (var (rowNumber, analysis, coverage, mode) in _pendingWrites)
                    {
                        if (mode == AnalysisMode.QA)
                        {
                            worksheet.Cells[rowNumber, 8].Value = analysis;

                            if (analysis == "GOOD")
                                worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                            else if (analysis.StartsWith("Issue:") || analysis.StartsWith("Steps"))
                                worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                            else if (analysis.StartsWith("ERROR:"))
                                worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Red);

                            worksheet.Cells[rowNumber, 8].Style.WrapText = true;
                            worksheet.Cells[rowNumber, 8].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                            worksheet.Cells[rowNumber, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                        }
                        else
                        {
                            worksheet.Cells[rowNumber, 8].Value = analysis;
                            worksheet.Cells[rowNumber, 8].Style.WrapText = true;
                            worksheet.Cells[rowNumber, 8].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                            worksheet.Cells[rowNumber, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                            if (string.IsNullOrWhiteSpace(analysis))
                            {
                                worksheet.Cells[rowNumber, 8].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                worksheet.Cells[rowNumber, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                            }
                            else if (analysis.StartsWith("ERROR:"))
                                worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Red);

                            worksheet.Cells[rowNumber, 9].Value = coverage;
                            worksheet.Cells[rowNumber, 9].Style.WrapText = true;
                            worksheet.Cells[rowNumber, 9].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                            worksheet.Cells[rowNumber, 9].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);

                            if (string.IsNullOrWhiteSpace(coverage) || coverage == "None")
                            {
                                worksheet.Cells[rowNumber, 9].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                worksheet.Cells[rowNumber, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightYellow);
                            }
                            else
                            {
                                worksheet.Cells[rowNumber, 9].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                                worksheet.Cells[rowNumber, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGreen);
                            }
                        }
                    }

                    package.Save();
                }

                int writeCount = _pendingWrites.Count;
                _pendingWrites.Clear();
                Console.WriteLine($"   ✅ Wrote {writeCount} analysis results to Excel");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not flush {_pendingWrites.Count} analysis results to '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the "Quality Issues Summary" sheet containing only test cases that need improvement
        /// </summary>
        /// <param name="results">
        /// List of analysis results (one tuple per test analyzed):
        /// - TestId: Test case identifier
        /// - Result: AI analysis feedback ("GOOD" or "Issue: ...")
        /// - Tokens: OpenAI API tokens used (not used in this sheet)
        /// </param>
        /// <remarks>
        /// SHEET STRUCTURE:
        /// - Deletes existing "Quality Issues Summary" sheet if present
        /// - Creates new sheet with 3 columns: Test ID, Issue Found, Status
        /// - Only includes tests where Result != "GOOD" and doesn't start with "ERROR:"
        /// - Adds summary row at bottom with total issue count
        /// 
        /// FORMATTING:
        /// - Light blue header with bold text
        /// - Freeze panes (header row stays visible when scrolling)
        /// - Auto-filter enabled on all columns
        /// - Column widths: Test ID=15, Issue Found=60, Status=15
        /// - Orange text color for Test ID column (visual warning)
        /// 
        /// EXAMPLE OUTPUT:
        /// - 56 tests analyzed, 52 have issues → sheet contains 52 rows + header + summary = 54 rows total
        /// </remarks>
        public void CreateQualityIssuesSheet(List<(string TestId, string Result, int Tokens, string Coverage)> results)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    // Delete existing sheet if it exists
                    var existingSheet = package.Workbook.Worksheets["Quality Issues Summary"];
                    if (existingSheet != null)
                    {
                        package.Workbook.Worksheets.Delete(existingSheet);
                    }
                    // Create new worksheet
                    var issuesSheet = package.Workbook.Worksheets.Add("Quality Issues Summary");

                    // HEADERS
                    issuesSheet.Cells[1, 1].Value = "Test ID";
                    issuesSheet.Cells[1, 2].Value = "Issue Found";
                    issuesSheet.Cells[1, 3].Value = "Status";

                    // Format headers
                    using (var headerRange = issuesSheet.Cells[1, 1, 1, 3])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                        headerRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        headerRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);
                    }
                    // Freeze panes on Quality Issues sheet
                    issuesSheet.View.FreezePanes(2, 1); // Freeze header row

                    // Auto-filter on Quality Issues sheet
                    issuesSheet.Cells[1, 1, 1, 3].AutoFilter = true;

                    // Auto-size columns
                    issuesSheet.Column(1).AutoFit(); // Test ID
                    issuesSheet.Column(2).Width = 80; // Issue Found (wider for readability)
                    issuesSheet.Column(3).AutoFit(); // Status

                    // DATA ROWS - only tests with issues
                    int currentRow = 2;
                    foreach (var (testId, result, tokens, coverage) in results)
                    {
                        if (result != "GOOD" && !result.StartsWith("ERROR:"))
                        {
                            issuesSheet.Cells[currentRow, 1].Value = testId;
                            issuesSheet.Cells[currentRow, 2].Value = result.Replace("Issue: ", "");
                            issuesSheet.Cells[currentRow, 3].Value = "Needs Review";

                            // Format issue row
                            issuesSheet.Cells[currentRow, 2].Style.WrapText = true;
                            issuesSheet.Cells[currentRow, 1].Style.Font.Color.SetColor(System.Drawing.Color.Orange);

                            currentRow++;
                        }
                    }

                    // Summary at the bottom
                    currentRow++; // Empty row
                    issuesSheet.Cells[currentRow, 1].Value = "TOTAL ISSUES:";
                    issuesSheet.Cells[currentRow, 2].Value = currentRow - 3; // Subtract header + empty row
                    issuesSheet.Cells[currentRow, 1].Style.Font.Bold = true;

                    // Auto-fit columns
                    issuesSheet.Column(1).Width = 15;
                    issuesSheet.Column(2).Width = 60;
                    issuesSheet.Column(3).Width = 15;

                    package.Save();
                    Console.WriteLine("   ✅ Created 'Quality Issues Summary' sheet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not create 'Quality Issues Summary' sheet in '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }


        /// <summary>
        /// Creates the "Statistics Dashboard" sheet with executive-level test suite quality metrics and recommendations
        /// </summary>
        /// <param name="results">
        /// List of analysis results (one tuple per test analyzed):
        /// - TestId: Test case identifier
        /// - Result: AI analysis feedback ("GOOD" or "Issue: ...")
        /// - Tokens: OpenAI API tokens used for this test (0 if cached)
        /// </param>
        /// <param name="startTime">Analysis start time (used to calculate total duration)</param>
        /// <param name="endTime">Analysis end time (used to calculate total duration)</param>
        /// <remarks>
        /// Creates a new "Statistics Dashboard" sheet (deletes existing if present).
        /// 
        /// SECTIONS CREATED:
        /// 1. QUALITY OVERVIEW - Overall quality score with color coding
        /// 2. TEST BREAKDOWN - Table showing good tests, issues, errors (counts + percentages)
        /// 3. COST & PERFORMANCE METRICS - Total tokens, cost breakdown, time taken, averages
        /// 4. RECOMMENDATIONS - Dynamic advice based on quality score threshold
        /// 
        /// QUALITY SCORE COLOR CODING:
        /// - Green: >= 80% → "Excellent - maintain quality standards"
        /// - Yellow: >= 50% → "Moderate - address issues in Quality Issues Summary sheet"  
        /// - Red: < 50% → "Critical - less than half of tests meet standards"
        /// 
        /// BUDGET PROJECTIONS:
        /// Calculates:
        /// - "Tests you can analyze with $10" (based on current avg cost/test)
        /// - "Cost to analyze 500 tests" (realistic scaling estimate)
        /// Helps users understand ongoing API costs for regular use.
        /// 
        /// PROFESSIONAL FORMATTING:
        /// - Merged cells for section headers (visual hierarchy)
        /// - Bold fonts and color coding for emphasis
        /// - Auto-sized columns for readability (15-50 char range)
        /// - Freeze panes (keeps title visible when scrolling)
        /// - Medium borders around entire used range
        /// </remarks>
        public void CreateStatisticsDashboard(List<(string TestId, string Result, int Tokens, string Coverage)> results, DateTime startTime, DateTime endTime)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    // Delete existing sheet if it exists
                    var existingSheet = package.Workbook.Worksheets["Statistics Dashboard"];
                    if (existingSheet != null)
                    {
                        package.Workbook.Worksheets.Delete(existingSheet);
                    }

                    var statsSheet = package.Workbook.Worksheets.Add("Statistics Dashboard");

                    // Calculate metrics
                    int totalTests = results.Count;
                    int goodTests = results.Count(r => r.Result == "GOOD");
                    int issueTests = results.Count(r => r.Result != "GOOD" && !r.Result.StartsWith("ERROR:"));
                    int errorTests = results.Count(r => r.Result.StartsWith("ERROR:"));
                    int totalTokens = results.Sum(r => r.Tokens);
                    double totalCost = totalTokens * _promptConfig.CostPerToken;
                    int avgTokens = totalTests > 0 ? totalTokens / totalTests : 0;
                    double timeTaken = (endTime - startTime).TotalSeconds;
                    double qualityScore = totalTests > 0 ? (goodTests * 100.0 / totalTests) : 0;

                    // TITLE
                    statsSheet.Cells[1, 1].Value = "AI TEST SUITE ANALYZER - STATISTICS DASHBOARD";
                    statsSheet.Cells[1, 1, 1, 4].Merge = true;
                    statsSheet.Cells[1, 1].Style.Font.Size = 16;
                    statsSheet.Cells[1, 1].Style.Font.Bold = true;
                    statsSheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    statsSheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkBlue);
                    statsSheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);

                    int row = 3;

                    // SECTION 1: QUALITY OVERVIEW
                    statsSheet.Cells[row, 1].Value = "QUALITY OVERVIEW";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Overall Quality Score:";
                    statsSheet.Cells[row, 2].Value = $"{qualityScore:F1}%";
                    statsSheet.Cells[row, 2].Style.Font.Bold = true;
                    statsSheet.Cells[row, 2].Style.Font.Size = 12;
                    if (qualityScore >= 80)
                        statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    else if (qualityScore >= 50)
                        statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                    else
                        statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Total Tests Analyzed:";
                    statsSheet.Cells[row, 2].Value = totalTests;
                    row++;

                    row++; // Empty row

                    // SECTION 2: TEST BREAKDOWN
                    statsSheet.Cells[row, 1].Value = "TEST BREAKDOWN";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    // Table headers
                    statsSheet.Cells[row, 1].Value = "Category";
                    statsSheet.Cells[row, 2].Value = "Count";
                    statsSheet.Cells[row, 3].Value = "Percentage";
                    statsSheet.Cells[row, 1, row, 3].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1, row, 3].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1, row, 3].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
                    row++;

                    // Good tests
                    statsSheet.Cells[row, 1].Value = "✅ Good Quality Tests";
                    statsSheet.Cells[row, 2].Value = goodTests;
                    statsSheet.Cells[row, 3].Value = $"{(totalTests > 0 ? goodTests * 100.0 / totalTests : 0):F1}%";
                    statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    row++;

                    // Tests with issues
                    statsSheet.Cells[row, 1].Value = "⚠️ Tests with Issues";
                    statsSheet.Cells[row, 2].Value = issueTests;
                    statsSheet.Cells[row, 3].Value = $"{(totalTests > 0 ? issueTests * 100.0 / totalTests : 0):F1}%";
                    statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                    row++;

                    // Errors
                    if (errorTests > 0)
                    {
                        statsSheet.Cells[row, 1].Value = "❌ Analysis Errors";
                        statsSheet.Cells[row, 2].Value = errorTests;
                        statsSheet.Cells[row, 3].Value = $"{(totalTests > 0 ? errorTests * 100.0 / totalTests : 0):F1}%";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                        row++;
                    }

                    row++; // Empty row

                    // SECTION 3: COST & PERFORMANCE
                    statsSheet.Cells[row, 1].Value = "COST & PERFORMANCE METRICS";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Total Tokens Used:";
                    statsSheet.Cells[row, 2].Value = $"{totalTokens:N0}";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Average Tokens/Test:";
                    statsSheet.Cells[row, 2].Value = avgTokens;
                    row++;

                    statsSheet.Cells[row, 1].Value = "Total Cost:";
                    statsSheet.Cells[row, 2].Value = $"${totalCost:F6}";
                    statsSheet.Cells[row, 2].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    row++;

                    statsSheet.Cells[row, 1].Value = "Average Cost/Test:";
                    statsSheet.Cells[row, 2].Value = $"${(totalTests > 0 ? totalCost / totalTests : 0):F6}";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Analysis Time:";
                    statsSheet.Cells[row, 2].Value = $"{timeTaken:F1} seconds";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Average Time/Test:";
                    statsSheet.Cells[row, 2].Value = $"{(totalTests > 0 ? timeTaken / totalTests : 0):F2} seconds";
                    row++;

                    row++; // Empty row

                    // SECTION 4: RECOMMENDATIONS
                    statsSheet.Cells[row, 1].Value = "RECOMMENDATIONS";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    statsSheet.Cells[row, 1].Style.Font.Size = 14;
                    statsSheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    statsSheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    row++;

                    if (qualityScore < 50)
                    {
                        statsSheet.Cells[row, 1].Value = "❌ CRITICAL: Less than 50% of tests meet quality standards.";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                        row++;
                        statsSheet.Cells[row, 1].Value = "   Action: Review 'Quality Issues Summary' sheet and prioritize fixes.";
                        row++;
                    }
                    else if (qualityScore < 80)
                    {
                        statsSheet.Cells[row, 1].Value = "⚠️ MODERATE: Test suite quality needs improvement.";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                        row++;
                        statsSheet.Cells[row, 1].Value = "   Action: Address issues in 'Quality Issues Summary' sheet.";
                        row++;
                    }
                    else
                    {
                        statsSheet.Cells[row, 1].Value = "✅ EXCELLENT: Test suite quality is high!";
                        statsSheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                        row++;
                        statsSheet.Cells[row, 1].Value = "   Action: Maintain quality standards and address remaining issues.";
                        row++;
                    }

                    row++;

                    // Budget projection
                    statsSheet.Cells[row, 1].Value = "💰 BUDGET PROJECTION";
                    statsSheet.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    int testsPerDollar = totalCost > 0 ? (int)(1.0 / (totalCost / totalTests)) : 0;
                    statsSheet.Cells[row, 1].Value = "Tests you can analyze with $10:";
                    statsSheet.Cells[row, 2].Value = $"{testsPerDollar * 10:N0}";
                    row++;

                    statsSheet.Cells[row, 1].Value = "Cost to analyze 500 tests:";
                    statsSheet.Cells[row, 2].Value = $"${(totalTests > 0 ? (totalCost / totalTests) * 500 : 0):F4}";

                    // Column widths
                    statsSheet.Column(1).Width = 40;
                    statsSheet.Column(2).Width = 20;
                    statsSheet.Column(3).Width = 15;

                    // Add borders to all used cells
                    var usedRange = statsSheet.Cells[1, 1, row, 3];
                    usedRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);

                    //Auto-size columns for better readability
                    for (int col = 1; col <= 2; col++)
                    {
                        statsSheet.Column(col).AutoFit(15, 50); // Min 15, Max 50 characters
                    }

                    //Freeze panes to keep title visible
                    statsSheet.View.FreezePanes(2, 1); // Freeze first row

                    package.Save();
                    Console.WriteLine("   ✅ Created 'Statistics Dashboard' sheet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not create 'Statistics Dashboard' sheet in '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }

        // ============================================================
        // HELPER: Build coverage map from results
        // requirement ID → list of TestIds that cover it
        // ============================================================
        private Dictionary<string, List<string>> BuildCoverageMap(
            List<(string TestId, string Result, int Tokens, string Coverage)> results,
            List<ExtractedRequirement> requirements)
        {
            // Initialize every requirement with an empty list
            var coverageMap = new Dictionary<string, List<string>>();
            foreach (var req in requirements)
            {
                if (!string.IsNullOrWhiteSpace(req.Id))
                    coverageMap[req.Id] = new List<string>();
            }

            // Parse coverage string per test and map back to requirement IDs
            foreach (var (testId, result, tokens, coverage) in results)
            {
                if (string.IsNullOrWhiteSpace(coverage) || coverage == "None")
                    continue;

                // Coverage string format: "UA-01, UA-02, UA-03" or "TM-01, TM-02"
                var ids = coverage.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(id => id.Trim())
                                  .Where(id => !string.IsNullOrWhiteSpace(id));

                foreach (var id in ids)
                {
                    if (coverageMap.ContainsKey(id))
                    {
                        coverageMap[id].Add(testId);
                    }
                    // If ID not in map, it came from AI but isn't in our requirements list — skip it
                }
            }

            return coverageMap;
        }

        // ============================================================
        // METHOD: Create Coverage Gap Summary Sheet (BA Mode only)
        // ============================================================
        public void CreateCoverageGapSheet(
            List<(string TestId, string Result, int Tokens, string Coverage)> results,
            List<ExtractedRequirement> requirements)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    // Delete existing sheet if present
                    var existingSheet = package.Workbook.Worksheets["Coverage Gap Analysis"];
                    if (existingSheet != null)
                        package.Workbook.Worksheets.Delete(existingSheet);

                    var sheet = package.Workbook.Worksheets.Add("Coverage Gap Analysis");

                    // ── TITLE ──────────────────────────────────────────
                    sheet.Cells[1, 1].Value = "REQUIREMENT COVERAGE GAP ANALYSIS";
                    sheet.Cells[1, 1, 1, 5].Merge = true;
                    sheet.Cells[1, 1].Style.Font.Size = 14;
                    sheet.Cells[1, 1].Style.Font.Bold = true;
                    sheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                    sheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.DarkSlateBlue);
                    sheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);

                    // ── HEADERS ────────────────────────────────────────
                    sheet.Cells[2, 1].Value = "Req ID";
                    sheet.Cells[2, 2].Value = "Description";
                    sheet.Cells[2, 3].Value = "Tests Covering It";
                    sheet.Cells[2, 4].Value = "Count";
                    sheet.Cells[2, 5].Value = "Status";

                    using (var headerRange = sheet.Cells[2, 1, 2, 5])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.SteelBlue);
                        headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        headerRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        headerRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);
                    }

                    // Freeze header rows
                    sheet.View.FreezePanes(3, 1);
                    sheet.Cells[2, 1, 2, 5].AutoFilter = true;

                    // ── BUILD COVERAGE MAP ─────────────────────────────
                    var coverageMap = BuildCoverageMap(results, requirements);

                    // ── DATA ROWS ──────────────────────────────────────
                    int row = 3;
                    int notCovered = 0;
                    int lowCoverage = 0;
                    int covered = 0;

                    foreach (var req in requirements)
                    {
                        if (string.IsNullOrWhiteSpace(req.Id))
                            continue;

                        var testsCovering = coverageMap.ContainsKey(req.Id)
                            ? coverageMap[req.Id]
                            : new List<string>();

                        int count = testsCovering.Count;
                        string testsStr = count > 0 ? string.Join(", ", testsCovering) : "";
                        string description = req.IsCompressedFormat()
                            ? req.Description
                            : (!string.IsNullOrWhiteSpace(req.Subtopic) ? req.Subtopic : req.Topic);

                        // Determine status
                        string status;
                        System.Drawing.Color rowColor;

                        if (count == 0)
                        {
                            status = "❌ NOT COVERED";
                            rowColor = System.Drawing.Color.FromArgb(255, 199, 206); // Light red
                            notCovered++;
                        }
                        else if (count == 1)
                        {
                            status = "⚠️ LOW (1 test)";
                            rowColor = System.Drawing.Color.FromArgb(255, 235, 156); // Light yellow
                            lowCoverage++;
                        }
                        else
                        {
                            status = $"✅ COVERED ({count} tests)";
                            rowColor = System.Drawing.Color.FromArgb(198, 239, 206); // Light green
                            covered++;
                        }

                        sheet.Cells[row, 1].Value = req.Id;
                        sheet.Cells[row, 2].Value = description;
                        sheet.Cells[row, 3].Value = testsStr;
                        sheet.Cells[row, 4].Value = count;
                        sheet.Cells[row, 5].Value = status;

                        // Apply row background color
                        using (var rowRange = sheet.Cells[row, 1, row, 5])
                        {
                            rowRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            rowRange.Style.Fill.BackgroundColor.SetColor(rowColor);
                            rowRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                        }

                        sheet.Cells[row, 2].Style.WrapText = true;
                        sheet.Cells[row, 3].Style.WrapText = true;

                        row++;
                    }

                    // ── SUMMARY ROW ────────────────────────────────────
                    row++; // Empty row
                    sheet.Cells[row, 1].Value = "SUMMARY";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 12;
                    row++;

                    sheet.Cells[row, 1].Value = "❌ Not Covered:";
                    sheet.Cells[row, 2].Value = notCovered;
                    sheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    sheet.Cells[row, 1].Value = "⚠️ Low Coverage (1 test):";
                    sheet.Cells[row, 2].Value = lowCoverage;
                    sheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.OrangeRed);
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    sheet.Cells[row, 1].Value = "✅ Covered (2+ tests):";
                    sheet.Cells[row, 2].Value = covered;
                    sheet.Cells[row, 1].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    row++;

                    int total = notCovered + lowCoverage + covered;
                    double coveragePct = total > 0 ? (covered * 100.0 / total) : 0;
                    sheet.Cells[row, 1].Value = "Coverage Score:";
                    sheet.Cells[row, 2].Value = $"{coveragePct:F1}%";
                    sheet.Cells[row, 2].Style.Font.Bold = true;
                    sheet.Cells[row, 2].Style.Font.Color.SetColor(
                        coveragePct >= 80 ? System.Drawing.Color.Green :
                        coveragePct >= 50 ? System.Drawing.Color.OrangeRed :
                        System.Drawing.Color.Red);

                    // ── COLUMN WIDTHS ──────────────────────────────────
                    sheet.Column(1).Width = 12;  // Req ID
                    sheet.Column(2).Width = 45;  // Description
                    sheet.Column(3).Width = 35;  // Tests Covering It
                    sheet.Column(4).Width = 10;  // Count
                    sheet.Column(5).Width = 22;  // Status

                    // Auto-fit row heights for wrapped text
                    for (int r = 3; r < row; r++)
                    {
                        sheet.Row(r).CustomHeight = false;
                    }

                    package.Save();
                    Console.WriteLine("   ✅ Created 'Coverage Gap Analysis' sheet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not create 'Coverage Gap Analysis' sheet in '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }

        // ============================================================
        // METHOD: Create Statistics dashboard Like QA mode  Sheet (BA Mode only)
        // ============================================================
        public void CreateBAStatisticsDashboard(
    List<(string TestId, string Result, int Tokens, string Coverage)> results,
    List<ExtractedRequirement> requirements,
    int totalTokens,
    int cacheHits,
    TimeSpan elapsed)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    var existingSheet = package.Workbook.Worksheets["BA Statistics Dashboard"];
                    if (existingSheet != null)
                        package.Workbook.Worksheets.Delete(existingSheet);

                    var sheet = package.Workbook.Worksheets.Add("BA Statistics Dashboard");
                    int row = 1;

                    // ── SECTION 1: COVERAGE SCORE ──────────────────────────────
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "BA MODE — STATISTICS DASHBOARD";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 14;
                    sheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(ColorTranslator.FromHtml("#2F4F4F")); // dark slate
                    sheet.Cells[row, 1].Style.Font.Color.SetColor(Color.White);
                    sheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    row += 2;

                    // Build coverage map (same logic as CreateCoverageGapSheet)
                    var coverageMap = BuildCoverageMap(results, requirements);
                    int covered = 0, low = 0, notCovered = 0;

                    foreach (var req in requirements)
                    {
                        var tests = coverageMap.ContainsKey(req.Id) ? coverageMap[req.Id] : new List<string>();
                        if (tests.Count >= 2)
                            covered++;
                        else if (tests.Count == 1)
                            low++;
                        else
                            notCovered++;
                    }

                    int total = requirements.Count;
                    double coverageScore = total > 0 ? Math.Round((covered / (double)total) * 100, 1) : 0;

                    // Big coverage score display
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "COVERAGE SCORE";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 11;
                    row++;

                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = $"{coverageScore}%";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 28;
                    sheet.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    var scoreColor = coverageScore >= 80
                        ? ColorTranslator.FromHtml("#C6EFCE")   // green
                        : coverageScore >= 50
                            ? ColorTranslator.FromHtml("#FFEB9C") // orange
                            : ColorTranslator.FromHtml("#FFC7CE"); // red

                    sheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(scoreColor);
                    row += 2;

                    // ── SECTION 2: REQUIREMENTS BREAKDOWN ─────────────────────
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "REQUIREMENTS BREAKDOWN";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 11;
                    sheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(ColorTranslator.FromHtml("#D9E1F2"));
                    row++;

                    // Header row
                    string[] headers = { "Status", "Count", "Percentage", "" };
                    for (int c = 0; c < 3; c++)
                    {
                        sheet.Cells[row, c + 1].Value = headers[c];
                        sheet.Cells[row, c + 1].Style.Font.Bold = true;
                    }
                    row++;

                    // Data rows
                    var breakdownData = new[]
                    {
        ("✅ Covered (2+ tests)",    covered,    total > 0 ? $"{Math.Round(covered / (double)total * 100, 1)}%" : "0%",   "#C6EFCE"),
        ("⚠️ Low Coverage (1 test)", low,        total > 0 ? $"{Math.Round(low / (double)total * 100, 1)}%" : "0%",       "#FFEB9C"),
        ("❌ Not Covered (0 tests)", notCovered, total > 0 ? $"{Math.Round(notCovered / (double)total * 100, 1)}%" : "0%","#FFC7CE"),
    };

                    foreach (var (label, count, pct, hex) in breakdownData)
                    {
                        sheet.Cells[row, 1].Value = label;
                        sheet.Cells[row, 2].Value = count;
                        sheet.Cells[row, 3].Value = pct;
                        sheet.Cells[row, 1, row, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        sheet.Cells[row, 1, row, 3].Style.Fill.BackgroundColor.SetColor(ColorTranslator.FromHtml(hex));
                        row++;
                    }

                    // Total row
                    sheet.Cells[row, 1].Value = "TOTAL";
                    sheet.Cells[row, 2].Value = total;
                    sheet.Cells[row, 3].Value = "100%";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 2].Style.Font.Bold = true;
                    row += 2;

                    // ── SECTION 3: COST & PERFORMANCE ─────────────────────────
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "COST & PERFORMANCE";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 11;
                    sheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(ColorTranslator.FromHtml("#D9E1F2"));
                    row++;

                    int apiCalls = results.Count - cacheHits;
                    double estimatedCost = totalTokens * _promptConfig.CostPerToken; // GPT-4o-mini rate

                    var perfData = new[]
                    {
        ("Test Cases Analyzed", results.Count.ToString()),
        ("Requirements Analyzed", total.ToString()),
        ("Total Tokens Used", totalTokens.ToString("N0")),
        ("API Calls Made", apiCalls.ToString()),
        ("Cache Hits", cacheHits.ToString()),
        ("Estimated Cost", $"${estimatedCost:F4} USD"),
        ("Time Elapsed", elapsed.ToString(@"mm\:ss")),
    };

                    foreach (var (label, value) in perfData)
                    {
                        sheet.Cells[row, 1].Value = label;
                        sheet.Cells[row, 2].Value = value;
                        sheet.Cells[row, 1].Style.Font.Bold = true;
                        row++;
                    }

                    // Column widths
                    sheet.Column(1).Width = 30;
                    sheet.Column(2).Width = 15;
                    sheet.Column(3).Width = 15;
                    package.Save();
                    Console.WriteLine("   ✅ Created 'BA Statistics Dashboard' sheet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not create 'BA Statistics Dashboard' sheet in '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }

        // ============================================================
        // METHOD: Create Generated Tests Sheet (GEN Mode only)
        // ============================================================
        public void CreateGeneratedTestsSheet(List<GeneratedTestCase> testCases)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    // Delete existing sheet if present
                    var existingSheet = package.Workbook.Worksheets["Generated Tests"];
                    if (existingSheet != null)
                        package.Workbook.Worksheets.Delete(existingSheet);

                    var sheet = package.Workbook.Worksheets.Add("Generated Tests");

                    // ── HEADERS ────────────────────────────────────────
                    string[] headers = {
                        "Test ID", "Feature", "Scenario", "Priority",
                        "Steps", "Expected Result", "Pass", "QA Score"
                    };

                    for (int col = 1; col <= headers.Length; col++)
                    {
                        sheet.Cells[1, col].Value = headers[col - 1];
                    }

                    // Header formatting — dark green (distinct from QA blue / BA coral)
                    using (var headerRange = sheet.Cells[1, 1, 1, headers.Length])
                    {
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Font.Size = 12;
                        headerRange.Style.Font.Color.SetColor(System.Drawing.Color.White);
                        headerRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        headerRange.Style.Fill.BackgroundColor.SetColor(
                            System.Drawing.ColorTranslator.FromHtml("#1A6B3C")); // dark green
                        headerRange.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                        headerRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);
                    }

                    // Freeze header row
                    sheet.View.FreezePanes(2, 1);

                    // Auto-filter
                    sheet.Cells[1, 1, 1, headers.Length].AutoFilter = true;

                    // ── DATA ROWS ──────────────────────────────────────
                    for (int i = 0; i < testCases.Count; i++)
                    {
                        var tc = testCases[i];
                        int row = i + 2;

                        sheet.Cells[row, 1].Value = tc.TestId;
                        sheet.Cells[row, 2].Value = tc.Feature;
                        sheet.Cells[row, 3].Value = tc.Scenario;
                        sheet.Cells[row, 4].Value = tc.Priority;
                        sheet.Cells[row, 5].Value = tc.Steps;
                        sheet.Cells[row, 6].Value = tc.ExpectedResult;
                        sheet.Cells[row, 7].Value = tc.PassNumber;
                        sheet.Cells[row, 8].Value = tc.QAScore;

                        // Wrap text on Steps and Expected Result
                        sheet.Cells[row, 5].Style.WrapText = true;
                        sheet.Cells[row, 6].Style.WrapText = true;
                        sheet.Cells[row, 8].Style.WrapText = true;

                        // Vertical alignment — top for all cells in row
                        for (int col = 1; col <= headers.Length; col++)
                        {
                            sheet.Cells[row, col].Style.VerticalAlignment =
                                OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                            sheet.Cells[row, col].Style.Border.BorderAround(
                                OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                        }

                        // Pass number column — highlight refinement passes
                        if (tc.PassNumber > 1)
                        {
                            sheet.Cells[row, 7].Style.Fill.PatternType =
                                OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            sheet.Cells[row, 7].Style.Fill.BackgroundColor.SetColor(
                                System.Drawing.ColorTranslator.FromHtml("#FFEB9C")); // light yellow
                            sheet.Cells[row, 7].Style.Font.Bold = true;
                        }

                        // QA Score column — color code by result
                        if (!string.IsNullOrEmpty(tc.QAScore))
                        {
                            if (tc.QAScore.StartsWith("GOOD", StringComparison.OrdinalIgnoreCase))
                            {
                                sheet.Cells[row, 8].Style.Font.Color.SetColor(
                                    System.Drawing.Color.Green);
                            }
                            else if (tc.QAScore.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                            {
                                sheet.Cells[row, 8].Style.Font.Color.SetColor(
                                    System.Drawing.Color.Red);
                            }
                            else
                            {
                                sheet.Cells[row, 8].Style.Font.Color.SetColor(
                                    System.Drawing.Color.OrangeRed);
                            }
                        }

                        // Priority column — color code
                        switch (tc.Priority.ToLower())
                        {
                            case "high":
                                sheet.Cells[row, 4].Style.Font.Color.SetColor(
                                    System.Drawing.Color.Red);
                                sheet.Cells[row, 4].Style.Font.Bold = true;
                                break;
                            case "medium":
                                sheet.Cells[row, 4].Style.Font.Color.SetColor(
                                    System.Drawing.Color.OrangeRed);
                                break;
                            case "low":
                                sheet.Cells[row, 4].Style.Font.Color.SetColor(
                                    System.Drawing.Color.Green);
                                break;
                        }
                    }

                    // ── COLUMN WIDTHS ──────────────────────────────────
                    sheet.Column(1).Width = 15;  // Test ID
                    sheet.Column(2).Width = 20;  // Feature
                    sheet.Column(3).Width = 45;  // Scenario
                    sheet.Column(4).Width = 12;  // Priority
                    sheet.Column(5).Width = 50;  // Steps
                    sheet.Column(6).Width = 40;  // Expected Result
                    sheet.Column(7).Width = 10;  // Pass
                    sheet.Column(8).Width = 60;  // QA Score

                    package.Save();
                    Console.WriteLine($"   ✅ Created 'Generated Tests' sheet ({testCases.Count} test cases)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not create 'Generated Tests' sheet " +
                                  $"in '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }

        // ============================================================
        // METHOD: Create Gen Statistics Dashboard (GEN Mode only)
        // ============================================================
        public void CreateGenStatisticsDashboard(GenModeResult result, TimeSpan elapsed)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    // Delete existing sheet if present
                    var existingSheet = package.Workbook.Worksheets["Gen Statistics Dashboard"];
                    if (existingSheet != null)
                        package.Workbook.Worksheets.Delete(existingSheet);

                    var sheet = package.Workbook.Worksheets.Add("Gen Statistics Dashboard");

                    // ── TITLE ──────────────────────────────────────────
                    sheet.Cells[1, 1, 1, 4].Merge = true;
                    sheet.Cells[1, 1].Value = "GEN MODE — STATISTICS DASHBOARD";
                    sheet.Cells[1, 1].Style.Font.Bold = true;
                    sheet.Cells[1, 1].Style.Font.Size = 14;
                    sheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                    sheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(
                        System.Drawing.ColorTranslator.FromHtml("#1A6B3C")); // dark green
                    sheet.Cells[1, 1].Style.HorizontalAlignment =
                        OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

                    // Freeze title row
                    sheet.View.FreezePanes(2, 1);

                    int row = 3;

                    // ── SECTION 1: GENERATION SUMMARY ─────────────────
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "GENERATION SUMMARY";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 11;
                    sheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(
                        System.Drawing.ColorTranslator.FromHtml("#D9E1F2"));
                    row++;

                    int droppedCount = 0; // tests lost between generation and final output
                    var genData = new[]
                    {
                        ("Passes Used",            result.TotalPasses.ToString()),
                        ("Tests Generated (final)", result.TestCases.Count.ToString()),
                        ("Tests Dropped",           droppedCount.ToString()),
                        ("Requirements Source",     result.RequirementsSource),
                        ("Generated At",            result.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))
                    };

                    foreach (var (label, value) in genData)
                    {
                        sheet.Cells[row, 1].Value = label;
                        sheet.Cells[row, 2].Value = value;
                        sheet.Cells[row, 1].Style.Font.Bold = true;
                        row++;
                    }

                    row++; // empty row

                    // ── SECTION 2: QA SCORE SUMMARY ───────────────────
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "QA SCORE SUMMARY";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 11;
                    sheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(
                        System.Drawing.ColorTranslator.FromHtml("#D9E1F2"));
                    row++;

                    // Table headers
                    sheet.Cells[row, 1].Value = "Category";
                    sheet.Cells[row, 2].Value = "Count";
                    sheet.Cells[row, 3].Value = "Percentage";
                    sheet.Cells[row, 1, row, 3].Style.Font.Bold = true;
                    sheet.Cells[row, 1, row, 3].Style.Fill.PatternType =
                        OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Cells[row, 1, row, 3].Style.Fill.BackgroundColor.SetColor(
                        System.Drawing.ColorTranslator.FromHtml("#1A6B3C"));
                    sheet.Cells[row, 1, row, 3].Style.Font.Color.SetColor(
                        System.Drawing.Color.White);
                    row++;

                    int total = result.TestCases.Count;
                    int goodCount = result.TestCases.Count(t =>
                        t.QAScore.StartsWith("GOOD", StringComparison.OrdinalIgnoreCase));
                    int errorCount = result.TestCases.Count(t =>
                        t.QAScore.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase));
                    int issueCount = total - goodCount - errorCount;

                    var qaRows = new[]
                    {
                        ("✅ GOOD",           goodCount,  "#C6EFCE"),
                        ("⚠️ Issues",         issueCount, "#FFEB9C"),
                        ("❌ Errors",         errorCount, "#FFC7CE")
                    };

                    foreach (var (label, count, hex) in qaRows)
                    {
                        string pct = total > 0
                            ? $"{count * 100.0 / total:F1}%"
                            : "0%";
                        sheet.Cells[row, 1].Value = label;
                        sheet.Cells[row, 2].Value = count;
                        sheet.Cells[row, 3].Value = pct;
                        sheet.Cells[row, 1, row, 3].Style.Fill.PatternType =
                            OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        sheet.Cells[row, 1, row, 3].Style.Fill.BackgroundColor.SetColor(
                            System.Drawing.ColorTranslator.FromHtml(hex));
                        row++;
                    }

                    // Total row
                    sheet.Cells[row, 1].Value = "TOTAL";
                    sheet.Cells[row, 2].Value = total;
                    sheet.Cells[row, 3].Value = "100%";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 2].Style.Font.Bold = true;
                    row += 2;

                    // ── SECTION 3: COST & PERFORMANCE ─────────────────
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "COST & PERFORMANCE";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 11;
                    sheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(
                        System.Drawing.ColorTranslator.FromHtml("#D9E1F2"));
                    row++;

                    double totalCost = result.TotalTokens * _promptConfig.CostPerToken;

                    var perfData = new[]
                    {
                        ("Total Tokens Used",    $"{result.TotalTokens:N0}"),
                        ("Estimated Cost",        $"${totalCost:F6} USD"),
                        ("Time Elapsed",          elapsed.ToString(@"mm\:ss")),
                        ("Avg Tokens per Pass",   result.TotalPasses > 0
                                                    ? $"{result.TotalTokens / result.TotalPasses:N0}"
                                                    : "0"),
                        ("Cost per Test Case",    total > 0
                                                    ? $"${totalCost / total:F6} USD"
                                                    : "$0.000000 USD")
                    };

                    foreach (var (label, value) in perfData)
                    {
                        sheet.Cells[row, 1].Value = label;
                        sheet.Cells[row, 2].Value = value;
                        sheet.Cells[row, 1].Style.Font.Bold = true;
                        row++;
                    }

                    row++; // empty row

                    // ── SECTION 4: REQUIREMENTS SOURCE ────────────────
                    sheet.Cells[row, 1, row, 4].Merge = true;
                    sheet.Cells[row, 1].Value = "REQUIREMENTS SOURCE";
                    sheet.Cells[row, 1].Style.Font.Bold = true;
                    sheet.Cells[row, 1].Style.Font.Size = 11;
                    sheet.Cells[row, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    sheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(
                        System.Drawing.ColorTranslator.FromHtml("#D9E1F2"));
                    row++;

                    bool isProvided = result.RequirementsSource
                        .Equals("provided", StringComparison.OrdinalIgnoreCase);

                    sheet.Cells[row, 1].Value = "Source Type";
                    sheet.Cells[row, 2].Value = isProvided
                        ? "✅ User-provided file"
                        : "⚠️ AI-generated";
                    sheet.Cells[row, 2].Style.Font.Color.SetColor(
                        isProvided ? System.Drawing.Color.Green : System.Drawing.Color.OrangeRed);
                    sheet.Cells[row, 1].Style.Font.Bold = true;

                    // Column widths
                    sheet.Column(1).Width = 35;
                    sheet.Column(2).Width = 20;
                    sheet.Column(3).Width = 15;

                    package.Save();
                    Console.WriteLine("   ✅ Created 'Gen Statistics Dashboard' sheet (skeleton)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not create 'Gen Statistics Dashboard' sheet " +
                                  $"in '{Path.GetFileName(_outputPath)}': {ex.Message}");
            }
        }
    }
}
