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
    /// and a Run Stats sheet.
    /// </summary>
    public class ArchModeExcelWriter
    {
        // ── STYLE: change these to match GEN Mode's look ─────────────
        private static readonly Color  _headerFill = Color.FromArgb(31, 78, 121);
        private static readonly Color _integrationFill = Color.FromArgb(64, 64, 64);
        private static readonly Color TotalFill = Color.FromArgb(230, 230, 230);
        private static readonly Color BorderColor = Color.FromArgb(191, 191, 191);
        private static readonly Color[] SectionFills =
        {
            Color.FromArgb(31, 78, 121),
            Color.FromArgb(55, 106, 64),
            Color.FromArgb(112, 48, 160),
            Color.FromArgb(156, 87, 0),
            Color.FromArgb(0, 112, 122),
            Color.FromArgb(153, 0, 0)
        };

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
            StyleBanner(ws.Cells[1, 1, 1, headers.Length], _headerFill);

            int row = 2;

            // Sections, in plan order. Tests are grouped by sub-topic in plan order.
            for (int s = 0; s < plan.Sections.Count; s++)
            {
                var section = plan.Sections[s];
                string idPrefix = $"TC-{section.TestIdPrefix}-";

                var tests = result.AllTestCases
                    .Where(t => t.TestId.StartsWith(idPrefix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => SubTopicIndex(section, t.Feature))
                    .ThenBy(t => t.TestId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                row = WriteBanner(ws, row,
                    $"{section.SectionName} ({section.TestIdPrefix}) — {tests.Count} tests",
                    SectionFills[s % SectionFills.Length]);

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
                    $"INTEGRATION TESTS — {integration.Count} tests", _integrationFill);

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
                    $"UNASSIGNED — {unassigned.Count} tests (ID prefix not in plan)", _integrationFill);
                foreach (var t in unassigned)
                    WriteTestRow(ws, row++, t);
            }

            ws.Column(1).Width = 14;
            ws.Column(2).Width = 26;
            ws.Column(3).Width = 50;
            ws.Column(4).Width = 10;
            ws.Column(5).Width = 60;
            ws.Column(6).Width = 50;
            ws.View.FreezePanes(2, 1);
        }

        private static int WriteBanner(ExcelWorksheet ws, int row, string text, Color fill)
        {
            var range = ws.Cells[row, 1, row, 6];
            range.Merge = true;
            ws.Cells[row, 1].Value = text;
            StyleBanner(range, fill);
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

            var range = ws.Cells[row, 1, row, 6];
            range.Style.WrapText = true;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Top;
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Bottom.Color.SetColor(BorderColor);

            var priority = ws.Cells[row, 4];
            priority.Style.Font.Bold = true;
            priority.Style.Font.Color.SetColor((t.Priority ?? "").Trim().ToLowerInvariant() switch
            {
                "high" => Color.FromArgb(192, 0, 0),
                "medium" => Color.FromArgb(156, 87, 0),
                _ => Color.FromArgb(84, 130, 53)
            });
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

            string[] headers = { "Section", "Sub-topic", "Planned", "Generated", "Difference", "Risk" };
            for (int c = 0; c < headers.Length; c++)
                ws.Cells[1, c + 1].Value = headers[c];
            StyleBanner(ws.Cells[1, 1, 1, headers.Length], _headerFill);

            int row = 2;
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
            StyleBanner(ws.Cells[row, 1, row, flowHeaders.Length], _integrationFill);
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
            ws.Cells[row, 1].Value = a;
            ws.Cells[row, 2].Value = b;
            ws.Cells[row, 3].Value = planned;
            ws.Cells[row, 4].Value = generated;
            ws.Cells[row, 5].Value = generated - planned;
            ws.Cells[row, 6].Value = risk;

            var range = ws.Cells[row, 1, row, 6];
            range.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            range.Style.Border.Bottom.Color.SetColor(BorderColor);

            if (isTotal)
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(TotalFill);
            }
        }

        // ============================================================
        // SHEET 3 — RUN STATS
        // ============================================================
        private void WriteRunStatsSheet(ExcelPackage package, ArchModeResult result, TimeSpan elapsed)
        {
            var ws = package.Workbook.Worksheets.Add("Run Stats");

            int integrationCount = result.AllTestCases.Count(t =>
                t.TestId.StartsWith("TC-INT-", StringComparison.OrdinalIgnoreCase));

            var stats = new (string Label, object Value)[]
            {
                ("Requirements source", result.RequirementsSource),
                ("Generated at", result.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")),
                ("Section tests", result.AllTestCases.Count - integrationCount),
                ("Integration tests", integrationCount),
                ("Total tests", result.AllTestCases.Count),
                ("AI passes", result.TotalPasses),
                ("Total tokens", result.TotalTokens),
                ("Elapsed time", $"{elapsed.TotalSeconds:F1}s"),
                ("Estimated cost (approx.)", $"${result.TotalTokens * _promptConfig.CostPerToken:F4}")
            };

            ws.Cells[1, 1].Value = "Metric";
            ws.Cells[1, 2].Value = "Value";
            StyleBanner(ws.Cells[1, 1, 1, 2], _headerFill);

            int row = 2;
            foreach (var (label, value) in stats)
            {
                ws.Cells[row, 1].Value = label;
                ws.Cells[row, 1].Style.Font.Bold = true;
                ws.Cells[row, 2].Value = value;
                ws.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
                row++;
            }

            ws.Column(1).Width = 28;
            ws.Column(2).Width = 44;
        }

        // ============================================================
        // SHARED STYLING
        // ============================================================
        private static void StyleBanner(ExcelRange range, Color fill)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(fill);
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }
    }
}
