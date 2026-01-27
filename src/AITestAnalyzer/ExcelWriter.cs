using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace AITestAnalyzer
{
    public class ExcelWriter
    {
        private readonly string _outputPath;

        public ExcelWriter(string outputPath)
        {
            _outputPath = outputPath;
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
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2 (index 1)
                    worksheet.Name = "AI Detailed Analysis";
                    package.Save();
                    Console.WriteLine("   ✅ Renamed sheet to 'AI Detailed Analysis'");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not rename sheet: {ex.Message}");
            }
        }

        // ============================================================
        // METHOD 4: Add AI Analysis Column Header
        // ============================================================
        public void AddAnalysisColumnHeader()
        {
            using (var package = new ExcelPackage(new FileInfo(_outputPath)))
            {
                var worksheet = package.Workbook.Worksheets[1]; // Sheet2

                // Add header in column 8 (H)
                worksheet.Cells[1, 8].Value = "AI Analysis";
                worksheet.Cells[1, 8].Style.Font.Bold = true;

                package.Save();
            }
        }

        // ============================================================
        // METHOD 5: Write Analysis to Excel with Color Coding
        // ============================================================
        public void WriteAnalysis(int rowNumber, string analysis)
        {
            try
            {
                using (var package = new ExcelPackage(new FileInfo(_outputPath)))
                {
                    var worksheet = package.Workbook.Worksheets[1]; // Sheet2

                    // Write to column 8 (AI Analysis)
                    worksheet.Cells[rowNumber, 8].Value = analysis;

                    // Color coding
                    if (analysis == "GOOD")
                    {
                        worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                    }
                    else if (analysis.StartsWith("Issue:"))
                    {
                        worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Orange);
                    }
                    else if (analysis.StartsWith("ERROR:"))
                    {
                        worksheet.Cells[rowNumber, 8].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                    }

                    worksheet.Cells[rowNumber, 8].Style.WrapText = true;
                    worksheet.Cells[rowNumber, 8].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Top;
                    worksheet.Cells[rowNumber, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
                    worksheet.Column(8).Width = 50;  // Set AI Analysis column to 50 characters wide

                    // In AddAnalysisColumnHeader method, add:
                    worksheet.Cells[1, 8].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    worksheet.Cells[1, 8].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
                    worksheet.Cells[1, 8].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);

                    package.Save();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Warning: Could not write to Excel row {rowNumber}: {ex.Message}");
            }
        }


        // ============================================================
        // METHOD 6: Create Quality Issues Summary Sheet
        // ============================================================
        public void CreateQualityIssuesSheet(List<(string TestId, string Result, int Tokens)> results)
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
                        headerRange.Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Medium);
                    }

                    // DATA ROWS - only tests with issues
                    int currentRow = 2;
                    foreach (var (testId, result, tokens) in results)
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
                Console.WriteLine($"   ⚠️  Warning: Could not create issues sheet: {ex.Message}");
            }
        }


        // ============================================================
        // METHOD 7: Create Statistics Dashboard Sheet
        // ============================================================
        public void CreateStatisticsDashboard(List<(string TestId, string Result, int Tokens)> results, DateTime startTime, DateTime endTime)
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
                    double totalCost = totalTokens * 0.00000015;
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

                    package.Save();
                    Console.WriteLine("   ✅ Created 'Statistics Dashboard' sheet");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️ Warning: Could not create statistics sheet: {ex.Message}");
            }
        }
    }
}
