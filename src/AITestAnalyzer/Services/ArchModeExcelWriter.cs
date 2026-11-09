using System.Drawing;
using AITestAnalyzer.Config;
using AITestAnalyzer.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace AITestAnalyzer.Services
{
    /// <summary>
    /// Writes ARCH Mode output to Excel: a Test Suite sheet (grouped by section,
    /// then integration tests by flow), a Plan Summary sheet (planned vs generated),
    /// and a Run Stats sheet. Styling matches GEN Mode (ExcelWriter.CreateGeneratedTestsSheet
    /// and CreateGenStatisticsDashboard).
    /// </summary>
    public class ArchModeExcelWriter
    {
        // ── STYLE: matches GEN Mode ──────────────────────────────────
        private static readonly Color _headerFill = ColorTranslator.FromHtml("#1A6B3C");      // GEN dark green
        private static readonly Color _sectionBanner = ColorTranslator.FromHtml("#D9E1F2");   // GEN section-header blue-grey
        private static readonly Color _integrationFill = ColorTranslator.FromHtml("#7030A0"); // ARCH-only purple banner
        private static readonly Color _totalFill = ColorTranslator.FromHtml("#D9E1F2");

        // Status colours used in GEN's dashboard rows
        private static readonly Color _goodFill = ColorTranslator.FromHtml("#C6EFCE");
        private static readonly Color _warnFill = ColorTranslator.FromHtml("#FFEB9C");
        private static readonly Color _badFill = ColorTranslator.FromHtml("#FFC7CE");

        // A shortfall of up to this fraction of the planned count is shown as a warning, not an error.
        private const double WarnShortfallRatio = 0.20;

        private readonly PromptConfig _promptConfig;

        /// <summary>
        /// Initialises a new instance of <see cref="ArchModeExcelWriter"/>.
        /// </summary>
        /// <param name="promptConfig">Prompt configuration, used for the cost estimate.</param>
        public ArchModeExcelWriter(PromptConfig promptConfig)
        {
            _promptConfig = promptConfig;
        }

        /// <summary>
        /// Creates arch_tests_{timestamp}.xlsx in the output folder with three sheets:
        /// Test Suite, Plan Summary and Run Stats.
        /// </summary>
        /// <param name="result">The ARCH Mode result: plan, test cases and run statistics.</param>
        /// <param name="elapsed">Total elapsed time for the run.</param>
        /// <returns>Full path to the created Excel file.</returns>
        public string WriteOutput(ArchModeResult result, TimeSpan elapsed)
        {
            string outputDir = ExcelWriter.CreateOutputFolder();
            string fileName = $"arch_tests_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            string outputPath = Path.Combine(outputDir, fileName);

            using (var package = new ExcelPackage())
            {
                Console.WriteLine("   📝 Creating Test Suite sheet...");
                WriteTestSuiteSheet(package, result);

                Console.WriteLine("   📊 Creating Plan Summary sheet...");
                WritePlanSummarySheet(package, result);

                Console.WriteLine("   📈 Creating Run Stats sheet...");
                WriteRunStatsSheet(package, result, elapsed);

                package.SaveAs(new FileInfo(outputPath));
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   ✅ ARCH Mode output saved: {fileName}");
            Console.ResetColor();

            return outputPath;
        }

        // ============================================================
        // SHEET 1 — TEST SUITE
        // ============================================================
        private static void WriteTestSuiteSheet(ExcelPackage package, ArchModeResult result)
        {
            var ws = package.Workbook.Worksheets.Add("Test Suite");
            var plan = result.Plan;
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string[] headers = { "Test ID", "Sub-topic", "Scenario", "Priority", "Steps", "Expected Result" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cells[1, c + 1].Value = headers[c];
            StyleTableHeader(ws.Cells[1, 1, 1, headers.Length]);

            int row = 2;

            // Sections, in plan order. Tests are grouped by sub-topic in plan order.
            foreach (var section in plan.Sections)
            {
                string idPrefix = $"TC-{section.TestIdPrefix}-";

                var tests = result.AllTestCases
                    .Where(t => t.TestId.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => SubTopicIndex(section, t.Feature))
                    .ThenBy(t => t.TestId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                row = WriteBanner(ws, row,
                    $"{section.SectionName} ({section.TestIdPrefix}) — {tests.Count} tests",
                    _sectionBanner, Color.Black);

                foreach (var t in tests)
                {
                    WriteTestRow(ws, row++, t);
                    written.Add(t.TestId);
                }
            }

            // Integration tests, grouped by flow in plan order.
            var integration = result.AllTestCases
                .Where(t => t.TestId.StartsWith("TC-INT-", StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => FlowIndex(plan, t.Feature))
                .ThenBy(t => t.TestId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (integration.Count > 0)
            {
                row = WriteBanner(ws, row,
                    $"INTEGRATION TESTS — {integration.Count} tests", _integrationFill, Color.White);

                foreach (var t in integration)
                {
                    WriteTestRow(ws, row++, t);
                    written.Add(t.TestId);
                }
            }

            // Safety net: never silently drop a test whose ID matches no section.
            var unassigned = result.AllTestCases.Where(t => !written.Contains(t.TestId)).ToList();
            if (unassigned.Count > 0)
            {
                row = WriteBanner(ws, row,
                    $"UNASSIGNED — {unassigned.Count} tests (ID prefix not in plan)",
                    _integrationFill, Color.White);
                foreach (var t in unassigned)
                    WriteTestRow(ws, row++, t);
            }

            // Column widths (same as GEN, Sub-topic slightly wider than GEN's Feature)
            ws.Column(1).Width = 15;  // Test ID
            ws.Column(2).Width = 22;  // Sub-topic
            ws.Column(3).Width = 45;  // Scenario
            ws.Column(4).Width = 12;  // Priority
            ws.Column(5).Width = 50;  // Steps
            ws.Column(6).Width = 40;  // Expected Result

            ws.View.FreezePanes(2, 1);
            ws.Cells[1, 1, 1, headers.Length].AutoFilter = true;
        }

        private static int WriteBanner(ExcelWorksheet ws, int row, string text, Color fill, Color fontColor)
        {
            var range = ws.Cells[row, 1, row, 6];
            range.Merge = true;
            ws.Cells[row, 1].Value = text;

            range.Style.Font.Bold = true;
            range.Style.Font.Size = 11;
            range.Style.Font.Color.SetColor(fontColor);
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(fill);
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
            return row + 1;
        }

        private static void WriteTestRow(ExcelWorksheet ws, int row, GeneratedTestCase t)
        {
            ws.Cells[row, 1].Value = t.TestId;
            ws.Cells[row, 2].Value = t.Feature;
            ws.Cells[row, 3].Value = t.Scenario;
            ws.Cells[row, 4].Value = t.Priority;
            ws.Cells[row, 5].Value = t.Steps;
            ws.Cells[row, 6].Value = t.ExpectedResult;

            // GEN: top-aligned, thin border around every cell
            for (int col = 1; col <= 6; col++)
            {
                var cell = ws.Cells[row, col];
                cell.Style.WrapText = true;
                cell.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // GEN priority colours: High red+bold, Medium orange-red, Low green
            var priority = ws.Cells[row, 4];
            switch ((t.Priority ?? "").Trim().ToLowerInvariant())
            {
                case "high":
                    priority.Style.Font.Color.SetColor(Color.Red);
                    priority.Style.Font.Bold = true;
                    break;
                case "medium":
                    priority.Style.Font.Color.SetColor(Color.OrangeRed);
                    break;
                case "low":
                    priority.Style.Font.Color.SetColor(Color.Green);
                    break;
            }
        }

        private static int SubTopicIndex(SectionTestPlan section, string feature)
        {
            int i = section.SubTopics.FindIndex(st =>
                st.SubTopicName.Equals(feature, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? int.MaxValue : i;
        }

        private static int FlowIndex(ArchitecturePlan plan, string flowName)
        {
            int i = plan.IntegrationFlows.FindIndex(f =>
                f.FlowName.Equals(flowName, StringComparison.OrdinalIgnoreCase));
            return i < 0 ? int.MaxValue : i;
        }

        // ============================================================
        // SHEET 2 — PLAN SUMMARY (planned vs generated)
        // ============================================================
        private static void WritePlanSummarySheet(ExcelPackage package, ArchModeResult result)
        {
            var ws = package.Workbook.Worksheets.Add("Plan Summary");
            var plan = result.Plan;

            // Title row (same pattern as GEN dashboard)
            WriteTitle(ws, "ARCH MODE — PLAN SUMMARY", 6);

            string[] headers = { "Section", "Sub-topic", "Planned", "Generated", "Difference", "Risk" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cells[3, c + 1].Value = headers[c];
            StyleTableHeader(ws.Cells[3, 1, 3, headers.Length]);

            int row = 4;
            foreach (var section in plan.Sections)
            {
                string idPrefix = $"TC-{section.TestIdPrefix}-";

                foreach (var sub in section.SubTopics)
                {
                    int generated = result.AllTestCases.Count(t =>
                        t.TestId.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase) &&
                        t.Feature.Equals(sub.SubTopicName, StringComparison.OrdinalIgnoreCase));

                    WriteSummaryRow(ws, row++, section.SectionName, sub.SubTopicName,
                        sub.RecommendedTests, generated, section.RiskLevel, isTotal: false);
                }

                int sectionGenerated = result.AllTestCases.Count(t =>
                    t.TestId.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase));

                WriteSummaryRow(ws, row++, section.SectionName, "Section total",
                    section.TotalRecommended, sectionGenerated, "", isTotal: true);
            }

            // Integration flows
            row++;
            string[] flowHeaders = { "Flow", "Sections", "Planned", "Generated", "Difference" };
            for (int c = 0; c < flowHeaders.Length; c++)
                ws.Cells[row, c + 1].Value = flowHeaders[c];
            var flowHeaderRange = ws.Cells[row, 1, row, flowHeaders.Length];
            StyleTableHeader(flowHeaderRange);
            flowHeaderRange.Style.Fill.BackgroundColor.SetColor(_integrationFill);
            row++;

            foreach (var flow in plan.IntegrationFlows)
            {
                int generated = result.AllTestCases.Count(t =>
                    t.TestId.StartsWith("TC-INT-", StringComparison.OrdinalIgnoreCase) &&
                    t.Feature.Equals(flow.FlowName, StringComparison.OrdinalIgnoreCase));

                WriteSummaryRow(ws, row++, flow.FlowName, string.Join(", ", flow.SectionsInvolved),
                    flow.RecommendedTests, generated, "", isTotal: false);
            }

            WriteSummaryRow(ws, row++, "GRAND TOTAL", "",
                plan.TotalSectionTests + plan.TotalIntegrationTests,
                result.AllTestCases.Count, "", isTotal: true);

            ws.Column(1).Width = 36;
            ws.Column(2).Width = 44;
            ws.Column(3).Width = 10;
            ws.Column(4).Width = 11;
            ws.Column(5).Width = 12;
            ws.Column(6).Width = 10;
            ws.View.FreezePanes(2, 1);
        }

        private static void WriteSummaryRow(ExcelWorksheet ws, int row, string a, string b,
            int planned, int generated, string risk, bool isTotal)
        {
            int diff = generated - planned;

            ws.Cells[row, 1].Value = a;
            ws.Cells[row, 2].Value = b;
            ws.Cells[row, 3].Value = planned;
            ws.Cells[row, 4].Value = generated;
            ws.Cells[row, 5].Value = diff;
            ws.Cells[row, 6].Value = risk;

            for (int col = 1; col <= 6; col++)
                ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);

            if (isTotal)
            {
                var range = ws.Cells[row, 1, row, 6];
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(_totalFill);
                return;
            }

            // Difference cell: green = met plan, yellow = small shortfall, red = big shortfall
            Color fill;
            if (diff >= 0)
                fill = _goodFill;
            else if (planned > 0 && (-diff / (double)planned) <= WarnShortfallRatio)
                fill = _warnFill;
            else
                fill = _badFill;

            var diffCell = ws.Cells[row, 5];
            diffCell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            diffCell.Style.Fill.BackgroundColor.SetColor(fill);
        }

        // ============================================================
        // SHEET 3 — RUN STATS (same layout as GEN Statistics Dashboard)
        // ============================================================
        private void WriteRunStatsSheet(ExcelPackage package, ArchModeResult result, TimeSpan elapsed)
        {
            var ws = package.Workbook.Worksheets.Add("Run Stats");

            int integrationCount = result.AllTestCases.Count(t =>
                t.TestId.StartsWith("TC-INT-", StringComparison.OrdinalIgnoreCase));

            WriteTitle(ws, "ARCH MODE — STATISTICS DASHBOARD", 4);

            int row = 3;

            // ── GENERATION SUMMARY ─────────────────────────────
            row = WriteSectionHeader(ws, row, "GENERATION SUMMARY", 4);

            var genData = new (string Label, object Value)[]
            {
                ("Requirements Source", result.RequirementsSource),
                ("Generated At", result.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                ("Section Tests", result.AllTestCases.Count - integrationCount),
                ("Integration Tests", integrationCount),
                ("Total Tests", result.AllTestCases.Count),
                ("AI Passes", result.TotalPasses)
            };
            row = WriteLabelRows(ws, row, genData);
            row++; // empty row

            // ── COST & PERFORMANCE ─────────────────────────────
            row = WriteSectionHeader(ws, row, "COST & PERFORMANCE", 4);

            int total = result.AllTestCases.Count;
            double totalCost = result.TotalTokens * _promptConfig.CostPerToken;

            var perfData = new (string Label, object Value)[]
            {
                ("Total Tokens Used", $"{result.TotalTokens:N0}"),
                ("Estimated Cost (approx.)", $"${totalCost:F6} USD"),
                ("Time Elapsed", elapsed.ToString(@"mm\:ss")),
                ("Cost per Test Case", total > 0 ? $"${totalCost / total:F6} USD" : "$0.000000 USD")
            };
            WriteLabelRows(ws, row, perfData);

            ws.Column(1).Width = 35;
            ws.Column(2).Width = 30;
            ws.Column(3).Width = 15;
            ws.View.FreezePanes(2, 1);
        }

        // ============================================================
        // SHARED STYLING (GEN look)
        // ============================================================

        /// <summary>Dark green table header: bold 12pt white, centered, medium border.</summary>
        private static void StyleTableHeader(ExcelRange range)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.Size = 12;
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(_headerFill);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.Border.BorderAround(ExcelBorderStyle.Medium);
        }

        /// <summary>Merged dark green title bar: bold 14pt white, centered.</summary>
        private static void WriteTitle(ExcelWorksheet ws, string text, int columnSpan)
        {
            var range = ws.Cells[1, 1, 1, columnSpan];
            range.Merge = true;
            ws.Cells[1, 1].Value = text;
            range.Style.Font.Bold = true;
            range.Style.Font.Size = 14;
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(_headerFill);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        /// <summary>Merged blue-grey section header: bold 11pt.</summary>
        private static int WriteSectionHeader(ExcelWorksheet ws, int row, string text, int columnSpan)
        {
            var range = ws.Cells[row, 1, row, columnSpan];
            range.Merge = true;
            ws.Cells[row, 1].Value = text;
            range.Style.Font.Bold = true;
            range.Style.Font.Size = 11;
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(_sectionBanner);
            return row + 1;
        }

        private static int WriteLabelRows(ExcelWorksheet ws, int row, (string Label, object Value)[] rows)
        {
            foreach (var (label, value) in rows)
            {
                ws.Cells[row, 1].Value = label;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 2].Value = value;
                ws.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                row++;
            }
            return row;
        }
    }
}
