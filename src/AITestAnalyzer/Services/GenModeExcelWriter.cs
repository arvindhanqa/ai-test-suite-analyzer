using AITestAnalyzer.Models;

namespace AITestAnalyzer.Services
{
    /// <summary>
    /// Orchestrates GEN Mode Excel output — creates the output file and
    /// delegates sheet creation to ExcelWriter.
    /// Produces two sheets: Generated Tests and Gen Statistics Dashboard.
    /// </summary>
    public class GenModeExcelWriter
    {
        private readonly PromptConfig _promptConfig;

        /// <summary>
        /// Initializes a new instance of GenModeExcelWriter.
        /// </summary>
        /// <param name="promptConfig">Prompt configuration for cost calculations in dashboard.</param>
        public GenModeExcelWriter(PromptConfig promptConfig)
        {
            _promptConfig = promptConfig;
        }

        /// <summary>
        /// Creates the GEN Mode output Excel file and writes both sheets.
        /// Output filename: generated_tests_{timestamp}.xlsx in the outputs/ folder.
        /// </summary>
        /// <param name="result">GEN Mode result containing generated test cases and statistics.</param>
        /// <param name="elapsed">Total elapsed time for the GEN Mode run.</param>
        /// <returns>Full path to the created output Excel file.</returns>
        public string WriteOutput(GenModeResult result, TimeSpan elapsed)
        {
            // ── CREATE OUTPUT FOLDER AND FILE ─────────────────────────
            string outputDir = ExcelWriter.CreateOutputFolder();
            string outputPath = CreateOutputFile(outputDir);

            Console.WriteLine();

            // ── WRITE BOTH SHEETS ─────────────────────────────────────
            var excelWriter = new ExcelWriter(outputPath, _promptConfig);

            Console.WriteLine("   📝 Creating Generated Tests sheet...");
            excelWriter.CreateGeneratedTestsSheet(result.TestCases);

            Console.WriteLine("   📊 Creating Gen Statistics Dashboard...");
            excelWriter.CreateGenStatisticsDashboard(result, elapsed);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   ✅ GEN Mode output saved: {Path.GetFileName(outputPath)}");
            Console.ResetColor();

            return outputPath;
        }

        /// <summary>
        /// Creates an empty Excel file in the output directory with a timestamped filename.
        /// Format: generated_tests_{yyyyMMdd_HHmmss}.xlsx
        /// </summary>
        /// <param name="outputDir">Directory where the output file will be created.</param>
        /// <returns>Full path to the created Excel file.</returns>
        private static string CreateOutputFile(string outputDir)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"generated_tests_{timestamp}.xlsx";
            string outputPath = Path.Combine(outputDir, fileName);

            // Create a minimal valid xlsx file using EPPlus
            using var package = new OfficeOpenXml.ExcelPackage();
            package.Workbook.Worksheets.Add("Sheet1");
            package.SaveAs(new FileInfo(outputPath));

            Console.WriteLine($"   ✅ Output file: {fileName}");

            return outputPath;
        }
    }
}
