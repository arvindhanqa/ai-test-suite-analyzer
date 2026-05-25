using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AITestAnalyzer.Config;

namespace AITestAnalyzer.UI
{
    /// <summary>
    /// Handles all interactive file/folder selection for the analyzer.
    /// Replaces command-line arguments and hardcoded appsettings paths
    /// with a menu-driven experience — just hit Play and pick what you want.
    /// </summary>
    public class FileSelector
    {
        // ============================================================
        // RESULT CLASS - holds whatever the user selected
        // ============================================================
        public class SelectionResult
        {
            public enum Mode { Single, Batch, Gen, Exit }
            public Mode SelectedMode { get; set; }
            public AnalysisMode SelectedAnalysisMode { get; set; }
            public string FilePath { get; set; } = "";
            public string FolderPath { get; set; } = "";
            public int TestLimit { get; set; } = 0;
            public int SheetIndex { get; set; } = 1;

            // GEN Mode specific properties
            public string? RequirementsPath { get; set; }
            public int TargetTestCount { get; set; } = 10;
            public int MaxPasses { get; set; } = 3;
        }


        /// <summary>
        /// Displays the interactive main menu to select analysis mode and configure options.
        /// Replaces command-line arguments with guided user interface.
        /// </summary>
        /// <returns>
        /// SelectionResult containing:
        /// - SelectedMode: Single (analyze one file), Batch (analyze folder), or Exit
        /// - FilePath: Set for Single mode only
        /// - FolderPath: Set for Batch mode only  
        /// - TestLimit: 0 for all tests, or user-specified number
        /// - SheetIndex: Excel worksheet index to analyze
        /// </returns>
        /// <remarks>
        /// MENU OPTIONS:
        /// [1] Analyze single file → calls SelectSingleFile()
        /// [2] Batch analyze folder → calls SelectBatchFolder()
        /// [3] Exit → returns SelectedMode.Exit
        /// 
        /// BEHAVIOR:
        /// - Loops until valid choice entered (1, 2, or 3)
        /// - Invalid input shows warning and re-displays menu  
        /// - Clears screen each iteration for clean UX
        /// 
        /// DESIGN PURPOSE:
        /// Entry point for interactive mode that replaces CLI argument parsing.
        /// Users no longer need to edit appsettings.json or remember command-line flags.
        /// Just run the app and follow the prompts.
        /// </remarks>
        public static SelectionResult ShowMainMenu()
        {
            while (true)
            {
                Console.Clear();
                WriteHeader("AI TEST SUITE ANALYZER");
                Console.WriteLine();
                Console.WriteLine("  What would you like to do?");
                Console.WriteLine();
                WriteMenuItem("1", "Analyze a single Excel file — QA Mode");
                WriteMenuItem("2", "Analyze a single Excel file — BA Mode");
                WriteMenuItem("3", "Generate test cases — GEN Mode  🆕");
                WriteMenuItem("4", "Batch analyze all Excel files in a folder");
                WriteMenuItem("5", "Exit");
                Console.WriteLine();
                string? choice = ReadIntegerInput($"Enter your choice (1-5): ", 1, 5);

                switch (choice)
                {
                    case "1":
                        return SelectSingleFileWithMode(AnalysisMode.QA);
                    case "2":
                        return SelectSingleFileWithMode(AnalysisMode.BA);
                    case "3":
                        return SelectGenMode();
                    case "4":
                        return SelectBatchFolder();
                    case "5":
                        return new SelectionResult { SelectedMode = SelectionResult.Mode.Exit };
                }
            }
        }

        /// <summary>
        /// Called via --gen-mode CLI flag — skips the main menu entirely.
        /// Shows only the GEN Mode selection screen.
        /// </summary>
        public static SelectionResult? SelectGenModeDirect()
        {
            var result = SelectGenMode();
            if (result.SelectedMode == SelectionResult.Mode.Exit)
                return null;
            return result;
        }

        /// <summary>
        /// Prompts user to select QA Mode or BA Mode for analysis
        /// </summary>
        public static AnalysisMode SelectAnalysisMode()
        {
            while (true)
            {
                Console.Clear();
                WriteHeader("SELECT ANALYSIS TYPE");
                Console.WriteLine();
                Console.WriteLine("  Choose your analysis mode:");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("    [1] QA MODE - Test Quality Analysis");
                Console.ResetColor();
                Console.WriteLine("        • Reviews test structure and clarity");
                Console.WriteLine("        • No requirements needed");
                Console.WriteLine("        • Fast analysis (~150 tokens per test)");
                Console.WriteLine("        • Output: 1 column (AI Analysis)");
                Console.WriteLine();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("    [2] BA MODE - Requirement Coverage Analysis");
                Console.ResetColor();
                Console.WriteLine("        • Validates tests against requirements");
                Console.WriteLine("        • Shows coverage gaps");
                Console.WriteLine("        • Requires requirement document");
                Console.WriteLine("        • Output: 2 columns (Requirement Feedback | Coverage)");
                Console.WriteLine();
                string? choice = ReadIntegerInput($"Enter your choice (1-2): ", 1, 2);

                switch (choice)
                {
                    case "1":
                        return AnalysisMode.QA;
                    case "2":
                        return AnalysisMode.BA;
                    default:
                        WriteWarning("Invalid choice. Please enter 1 or 2.");
                        PauseForUser();
                        break;
                }
            }
        }

        // ============================================================
        // SINGLE FILE SELECTION WITH PRE-SELECTED MODE
        // Called from main menu — mode already known
        // ============================================================
        private static SelectionResult SelectSingleFileWithMode(AnalysisMode mode)
        {
            var result = SelectSingleFile();
            if (result.SelectedMode != SelectionResult.Mode.Exit)
                result.SelectedAnalysisMode = mode;
            return result;
        }

        // ============================================================
        // GEN MODE SELECTION
        // Prompts for requirements file, test count, max passes
        // ============================================================
        private static SelectionResult SelectGenMode()
        {
            while (true)
            {
                Console.Clear();
                WriteHeader("GEN MODE — GENERATE TEST CASES");
                Console.WriteLine();
                Console.WriteLine("  GEN Mode generates test cases from a requirements");
                Console.WriteLine("  document using a Generate → Critique → Refine loop.");
                Console.WriteLine();

                // Requirements file — auto-detect from known locations
                var reqFiles = FindRequirementsFiles();
                string? reqPath = null;

                if (reqFiles.Count > 0)
                {
                    Console.WriteLine("  Available requirements files:");
                    Console.WriteLine();
                    for (int i = 0; i < reqFiles.Count; i++)
                    {
                        string fileName = Path.GetFileName(reqFiles[i]);
                        string? folder = GetShortPath(Path.GetDirectoryName(reqFiles[i]));
                        Console.WriteLine($"    [{i + 1}] {fileName}");
                        Console.WriteLine($"        {folder}");
                        Console.WriteLine();
                    }
                    WriteMenuItem("T", "Type a file path manually");
                    WriteMenuItem("B", "Back to main menu");
                    Console.WriteLine();
                    WritePrompt($"  Select file (1-{reqFiles.Count}, T, or B): ");

                    string? fileChoice = Console.ReadLine()?.Trim();

                    if (fileChoice?.ToUpper() == "B")
                        return ShowMainMenu();

                    if (fileChoice?.ToUpper() == "T")
                    {
                        WritePrompt("  📁 Enter path to requirements file (.md or .txt): ");
                        reqPath = Console.ReadLine()?.Trim().Trim('"').Trim('\'');
                    }
                    else if (int.TryParse(fileChoice, out int fileIndex)
                             && fileIndex >= 1 && fileIndex <= reqFiles.Count)
                    {
                        reqPath = reqFiles[fileIndex - 1];
                    }
                    else
                    {
                        WriteWarning("  Invalid selection.");
                        PauseForUser();
                        continue;
                    }
                }
                else
                {
                    // No files found — fall back to manual entry
                    Console.WriteLine("  No requirements files found in common locations.");
                    Console.WriteLine();
                    WritePrompt("  📁 Enter path to requirements file (.md or .txt): ");
                    reqPath = Console.ReadLine()?.Trim().Trim('"').Trim('\'');
                }

                if (string.IsNullOrWhiteSpace(reqPath) || !File.Exists(reqPath))
                {
                    WriteWarning($"  File not found: {reqPath}");
                    PauseForUser();
                    continue;
                }

                string ext = Path.GetExtension(reqPath).ToLower();
                if (ext != ".md" && ext != ".txt")
                {
                    WriteWarning("  Unsupported file type. Please provide a .md or .txt file.");
                    PauseForUser();
                    continue;
                }

                // Target test count
                Console.WriteLine();
                WritePrompt("  How many test cases to generate in total? [default: 10]: ");
                string? countInput = Console.ReadLine()?.Trim();
                int targetCount = int.TryParse(countInput, out int parsedCount) && parsedCount > 0
                    ? parsedCount : 10;

                // Max passes
                WritePrompt("  Maximum refinement passes? [1-3, default: 3]: ");
                string? passInput = Console.ReadLine()?.Trim();
                int maxPasses = int.TryParse(passInput, out int parsedPasses)
                    && parsedPasses >= 1 && parsedPasses <= 3
                    ? parsedPasses : 3;

                // Summary before running
                Console.WriteLine();
                Console.WriteLine("  ─── Ready to run ───────────────────────────────────");
                Console.WriteLine($"  Requirements: {Path.GetFileName(reqPath)}");
                Console.WriteLine($"  Target tests: {targetCount}");
                Console.WriteLine($"  Max passes:   {maxPasses}");
                Console.WriteLine("  ────────────────────────────────────────────────────");
                Console.WriteLine();
                WritePrompt("  Press Enter to start, or B to go back: ");

                string? confirm = Console.ReadLine()?.Trim().ToUpper();
                if (confirm == "B")
                    return ShowMainMenu();

                return new SelectionResult
                {
                    SelectedMode = SelectionResult.Mode.Gen,
                    RequirementsPath = reqPath,
                    TargetTestCount = targetCount,
                    MaxPasses = maxPasses
                };
            }
        }

        // ============================================================
        // SINGLE FILE SELECTION
        // Scans common locations, lets user pick from a list
        // ============================================================
        private static SelectionResult SelectSingleFile()
        {
            while (true)
            {
                Console.Clear();
                WriteHeader("SELECT EXCEL FILE");
                Console.WriteLine();

                // Find all .xlsx files in known locations
                var files = FindExcelFiles();

                if (files.Count == 0)
                {
                    WriteWarning("No .xlsx files found in common locations.");
                    Console.WriteLine();
                    Console.WriteLine("  Common locations checked:");
                    foreach (var loc in GetSearchLocations())
                        Console.WriteLine($"    - {loc}");
                    Console.WriteLine();
                    WriteMenuItem("1", "Type a file path manually");
                    WriteMenuItem("B", "Back to main menu");
                    Console.WriteLine();
                    WritePrompt("Your choice: ");

                    string? input = Console.ReadLine()?.Trim();
                    if (input?.ToUpper() == "B") return ShowMainMenu();
                    if (input == "1") return ManualFilePath();
                    continue;
                }

                // Show numbered list of files
                Console.WriteLine("  Available Excel files:");
                Console.WriteLine();
                for (int i = 0; i < files.Count; i++)
                {
                    string fileName = Path.GetFileName(files[i]);
                    string? folder = GetShortPath(Path.GetDirectoryName(files[i]));
                    Console.WriteLine($"    [{i + 1}] {fileName}");
                    Console.WriteLine($"        {folder}");
                    Console.WriteLine();
                }

                WriteMenuItem("T", "Type a file path manually");
                WriteMenuItem("B", "Back to main menu");
                Console.WriteLine();
                WritePrompt($"Select file (1-{files.Count}, T, or B): ");

                string? choice = Console.ReadLine()?.Trim();

                // Back
                if (choice?.ToUpper() == "B") return ShowMainMenu();

                // Type manually
                if (choice?.ToUpper() == "T") return ManualFilePath();

                // Number selection
                if (int.TryParse(choice, out int index) && index >= 1 && index <= files.Count)
                {
                    string? selectedFile = files[index - 1];
                    return ConfirmAndConfigureSingle(selectedFile);
                }

                WriteWarning("Invalid selection. Try again.");
                PauseForUser();
            }
        }

        // ============================================================
        // BATCH FOLDER SELECTION
        // ============================================================
        private static SelectionResult SelectBatchFolder()
        {
            while (true)
            {
                Console.Clear();
                WriteHeader("SELECT FOLDER FOR BATCH PROCESSING");
                Console.WriteLine();

                // Find folders that contain .xlsx files
                var folders = FindExcelFolders();

                if (folders.Count > 0)
                {
                    Console.WriteLine("  Folders with Excel files:");
                    Console.WriteLine();
                    for (int i = 0; i < folders.Count; i++)
                    {
                        int fileCount = Directory.GetFiles(folders[i], "*.xlsx").Length;
                        string shortPath = GetShortPath(folders[i]);
                        Console.WriteLine($"    [{i + 1}] {shortPath}  ({fileCount} files)");
                    }
                    Console.WriteLine();
                }

                WriteMenuItem("T", "Type a folder path manually");
                WriteMenuItem("B", "Back to main menu");
                Console.WriteLine();

                string prompt = folders.Count > 0
                    ? $"Select folder (1-{folders.Count}, T, or B): "
                    : "Your choice (T or B): ";
                WritePrompt(prompt);

                string? choice = Console.ReadLine()?.Trim();

                if (choice?.ToUpper() == "B") return ShowMainMenu();
                if (choice?.ToUpper() == "T") return ManualFolderPath();

                if (folders.Count > 0 && int.TryParse(choice, out int index) && index >= 1 && index <= folders.Count)
                {
                    return ConfirmAndConfigureBatch(folders[index - 1]);
                }

                WriteWarning("Invalid selection. Try again.");
                PauseForUser();
            }
        }

        // ============================================================
        // CONFIRMATION + OPTIONS SCREEN (Single)
        // Lets user set test limit and sheet before running
        // ============================================================
        private static SelectionResult ConfirmAndConfigureSingle(string filePath)
        {
            int testCount = CountTestsInFile(filePath);
            int testLimit = 0;    // 0 = all tests — persists across loop iterations
            int sheetIndex = 1;   // default sheet — persists across loop iterations

            while (true)
            {
                Console.Clear();
                WriteHeader("CONFIGURE ANALYSIS");
                Console.WriteLine();
                Console.WriteLine($"  📄 File: {Path.GetFileName(filePath)}");
                Console.WriteLine($"      Path: {GetShortPath(Path.GetDirectoryName(filePath))}");
                Console.WriteLine($"      Tests found: {testCount}");
                Console.WriteLine();

                // Show current settings so user knows what's actually set
                Console.WriteLine("  Current settings:");
                Console.WriteLine($"      Tests to analyze: {(testLimit == 0 ? $"ALL ({testCount})" : testLimit.ToString())}");
                Console.WriteLine($"      Sheet index:      {sheetIndex}");
                Console.WriteLine();

                Console.WriteLine("  Options:");
                Console.WriteLine($"    [1] Analyze ALL {testCount} tests");
                Console.WriteLine($"    [2] Analyze first N tests");
                Console.WriteLine($"    [3] Change sheet index");
                Console.WriteLine($"    [R] Run with current settings");
                Console.WriteLine($"    [B] Back");
                Console.WriteLine();
                WritePrompt("Your choice: ");

                string? choice = Console.ReadLine()?.Trim().ToUpper();

                switch (choice)
                {
                    case "1":
                        testLimit = 0; // Reset to all
                        WriteSuccess("  ✅ Set to analyze all tests.");
                        PauseForUser();
                        continue; // Back to config screen to confirm before running

                    case "R":
                        // Ask for analysis mode before returning
                        var analysisMode = SelectAnalysisMode();

                        return new SelectionResult
                        {
                            SelectedMode = SelectionResult.Mode.Single,
                            SelectedAnalysisMode = analysisMode, 
                            FilePath = filePath,
                            TestLimit = testLimit,
                            SheetIndex = sheetIndex
                        };

                    case "2":
                        Console.WriteLine();
                        WritePrompt($"  How many tests? (1-{testCount}): ");
                        string? limitInput = Console.ReadLine()?.Trim();
                        if (int.TryParse(limitInput, out int limit) && limit >= 1 && limit <= testCount)
                        {
                            testLimit = limit;
                            WriteSuccess($"  ✅ Will analyze first {limit} tests.");
                            PauseForUser();
                        }
                        else
                        {
                            WriteWarning($"  Invalid number. Must be 1-{testCount}.");
                            PauseForUser();
                        }
                        continue; // Back to config screen, testLimit is saved

                    case "3":
                        Console.WriteLine();
                        WritePrompt($"  Enter sheet index (current: {sheetIndex}) — 0=Sheet1, 1=Sheet2, etc.: ");
                        string? sheetInput = Console.ReadLine()?.Trim();
                        if (int.TryParse(sheetInput, out int sheet) && sheet >= 0)
                        {
                            sheetIndex = sheet;
                            WriteSuccess($"  ✅ Sheet index set to {sheet}.");
                            PauseForUser();
                        }
                        else
                        {
                            WriteWarning("  Invalid sheet index. Must be 0 or higher.");
                            PauseForUser();
                        }
                        continue; // Back to config screen, sheetIndex is saved

                    case "B":
                        return ShowMainMenu();

                    default:
                        WriteWarning("  Invalid choice.");
                        PauseForUser();
                        continue;
                }
            }
        }

        // ============================================================
        // CONFIRMATION + OPTIONS SCREEN (Batch)
        // ============================================================
        private static SelectionResult ConfirmAndConfigureBatch(string folderPath)
        {
            Console.Clear();
            WriteHeader("CONFIGURE BATCH PROCESSING");
            Console.WriteLine();

            var files = Directory.GetFiles(folderPath, "*.xlsx");
            Console.WriteLine($"  📂 Folder: {GetShortPath(folderPath)}");
            Console.WriteLine($"      Files found: {files.Length}");
            Console.WriteLine();
            Console.WriteLine("  Files to process:");
            foreach (var f in files)
                Console.WriteLine($"    • {Path.GetFileName(f)}");
            Console.WriteLine();

            // Ask for test limit
            WritePrompt("  Analyze how many tests per file? (Enter for ALL): ");
            string? limitInput = Console.ReadLine()?.Trim();
            int testLimit = 0;
            if (string.IsNullOrEmpty(limitInput))
            {
                testLimit = 0; // Enter = ALL
            }
            else if (int.TryParse(limitInput, out int limit) && limit > 0)
            {
                testLimit = limit;
            }
            else
            {
                WriteWarning("  Invalid input. Defaulting to ALL tests.");
            }

            // Ask for sheet index
            WritePrompt("  Sheet index (Enter for default=1): ");
            string? sheetInput = Console.ReadLine()?.Trim();
            int sheetIndex = 1;
            if (string.IsNullOrEmpty(sheetInput))
            {
                sheetIndex = 1; // Enter = default
            }
            else if (int.TryParse(sheetInput, out int sheet) && sheet >= 0)
            {
                sheetIndex = sheet;
            }
            else
            {
                WriteWarning("  Invalid input. Defaulting to sheet index 1.");
            }

            // Summary before running
            Console.WriteLine();
            Console.WriteLine("  ─── Ready to run ───");
            Console.WriteLine($"    📂 Folder:      {GetShortPath(folderPath)}");
            Console.WriteLine($"    📄 Files:       {files.Length}");
            Console.WriteLine($"    🔢 Tests/file:  {(testLimit == 0 ? "ALL" : testLimit.ToString())}");
            Console.WriteLine($"    📋 Sheet index: {sheetIndex}");
            Console.WriteLine();
            WritePrompt("  Press Enter to start, or B to go back: ");

            string? confirm = Console.ReadLine()?.Trim().ToUpper();
            if (confirm == "B") return ShowMainMenu();

            // Ask for analysis mode before returning
            var analysisMode = SelectAnalysisMode();

            return new SelectionResult
            {
                SelectedMode = SelectionResult.Mode.Batch,
                SelectedAnalysisMode = analysisMode,
                FolderPath = folderPath,
                TestLimit = testLimit,
                SheetIndex = sheetIndex
            };
        }

        // ============================================================
        // MANUAL PATH INPUT (File)
        // ============================================================
        private static SelectionResult ManualFilePath()
        {
            Console.WriteLine();
            WritePrompt("  Type the full path to your .xlsx file: ");
            string? path = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(path))
            {
                WriteWarning("  No path entered.");
                PauseForUser();
                return ShowMainMenu();
            }

            // Strip quotes if user copy-pasted with them
            path = path.Trim('"').Trim('\'');

            if (!File.Exists(path))
            {
                WriteWarning($"  File not found: {path}");
                PauseForUser();
                return ShowMainMenu();
            }

            if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                WriteWarning("  File must be an .xlsx file.");
                PauseForUser();
                return ShowMainMenu();
            }

            return ConfirmAndConfigureSingle(path);
        }

        // ============================================================
        // MANUAL PATH INPUT (Folder)
        // ============================================================
        private static SelectionResult ManualFolderPath()
        {
            Console.WriteLine();
            WritePrompt("  Type the full path to your folder: ");
            string? path = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(path))
            {
                WriteWarning("  No path entered.");
                PauseForUser();
                return ShowMainMenu();
            }

            path = path.Trim('"').Trim('\'');

            if (!Directory.Exists(path))
            {
                WriteWarning($"  Folder not found: {path}");
                PauseForUser();
                return ShowMainMenu();
            }

            var xlsxFiles = Directory.GetFiles(path, "*.xlsx");
            if (xlsxFiles.Length == 0)
            {
                WriteWarning("  No .xlsx files found in that folder.");
                PauseForUser();
                return ShowMainMenu();
            }

            return ConfirmAndConfigureBatch(path);
        }

        // ============================================================
        // HELPER: Find .xlsx files in common project locations
        // ============================================================
        private static List<string> FindExcelFiles()
        {
            var files = new List<string>();
            foreach (var location in GetSearchLocations())
            {
                if (Directory.Exists(location))
                {
                    files.AddRange(
                        Directory.GetFiles(location, "*.xlsx", SearchOption.TopDirectoryOnly)
                    );
                }
            }
            return files.Distinct().ToList();
        }

        // ============================================================
        // HELPER: Find folders that contain .xlsx files
        // ============================================================
        private static List<string> FindExcelFolders()
        {
            var folders = new List<string>();
            foreach (var location in GetSearchLocations())
            {
                if (Directory.Exists(location))
                {
                    var xlsxFiles = Directory.GetFiles(location, "*.xlsx", SearchOption.TopDirectoryOnly);
                    if (xlsxFiles.Length > 0 && !folders.Contains(location))
                    {
                        folders.Add(location);
                    }
                }
            }
            return folders;
        }

        // ============================================================
        // HELPER: Find .md and .txt requirements files in known locations
        // ============================================================
        private static List<string> FindRequirementsFiles()
        {
            var files = new List<string>();
            foreach (var location in GetSearchLocations())
            {
                if (Directory.Exists(location))
                {
                    files.AddRange(
                        Directory.GetFiles(location, "requirements_*.md",
                            SearchOption.TopDirectoryOnly));
                    files.AddRange(
                        Directory.GetFiles(location, "requirements_*.txt",
                            SearchOption.TopDirectoryOnly));
                }
            }
            return files.Distinct().OrderBy(f => Path.GetFileName(f)).ToList();
        }

        // ============================================================
        // HELPER: Locations to scan for .xlsx files
        // These cover typical project layouts — adjust if needed
        // ============================================================
        private static List<string> GetSearchLocations()
        {
            var locations = new List<string>();

            // Relative to where the app runs (bin/Debug or bin/Release)
            // Go up to project root, then into data/
            string appDir = Directory.GetCurrentDirectory();

            locations.Add(Path.Combine(appDir, "data"));                          // ./data/
            locations.Add(Path.Combine(appDir, "..", "data"));                    // ../data/
            locations.Add(Path.Combine(appDir, "..", "..", "data"));              // ../../data/
            locations.Add(Path.Combine(appDir, "..", "..", "..", "data"));        // ../../../data/ (bin/Debug/net10.0)
            locations.Add(Path.Combine(appDir, "..", "..", "..", "..", "data"));           // ../../../../data
            locations.Add(Path.Combine(appDir, "..", "..", "..", "..", "..", "data"));     // ../../../../../data
            locations.Add(appDir);                                                 // current dir itself
            locations.Add(Path.Combine(appDir, ".."));                            // parent dir

            // Normalize all paths
            return locations.Select(l => Path.GetFullPath(l)).Distinct().ToList();
        }

        // ============================================================
        // HELPER: Count test rows in an Excel file (without EPPlus dependency here)
        // Returns -1 if it can't read the file
        // ============================================================
        private static int CountTestsInFile(string filePath)
        {
            try
            {
                using (var package = new OfficeOpenXml.ExcelPackage(new System.IO.FileInfo(filePath)))
                {
                    // Try sheet index 1 first (Sheet2), fall back to 0 (Sheet1)
                    var worksheet = package.Workbook.Worksheets.Count > 1
                        ? package.Workbook.Worksheets[1]
                        : package.Workbook.Worksheets[0];

                    // Count rows that have a Test ID in column 1 (skip header)
                    int count = 0;
                    for (int row = 2; row <= worksheet.Dimension.End.Row; row++)
                    {
                        var cellValue = worksheet.Cells[row, 1].Value?.ToString();
                        if (!string.IsNullOrWhiteSpace(cellValue))
                            count++;
                    }
                    return count;
                }
            }
            catch
            {
                return -1; // Couldn't read file
            }
        }

        private static string ReadIntegerInput(string prompt, int min, int max)
        {
            while (true)
            {
                Console.Write(prompt);
                string? input = Console.ReadLine();

                if (int.TryParse(input, out int value))
                {
                    if (value >= min && value <= max)
                    {
                        return input;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"⚠️  Please enter a number between {min} and {max}");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠️  Invalid input. Please enter a number.");
                    Console.ResetColor();
                }
            }
        }

        // ============================================================
        // HELPER: Shorten long paths for display
        // ============================================================
        private static string GetShortPath(string? fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return "";

            // If path is short enough, show it all
            if (fullPath.Length <= 60) return fullPath;

            // Otherwise show first part ... last part
            string? root = Path.GetPathRoot(fullPath);     // e.g., "C:\"
            string end = fullPath.Substring(fullPath.Length - 40);
            return (root ?? "") + "..." + end;
        }

        // ============================================================
        // CONSOLE FORMATTING HELPERS (matches project style)
        // ============================================================
        private static void WriteHeader(string text)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  ╔══════════════════════════════════════════╗");
            Console.WriteLine($"  ║  {text.PadRight(42)}║");
            Console.WriteLine($"  ╚══════════════════════════════════════════╝");
            Console.ResetColor();
        }

        private static void WriteMenuItem(string key, string description)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"    [{key}] ");
            Console.ResetColor();
            Console.WriteLine(description);
        }

        private static void WritePrompt(string text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write(text);
            Console.ResetColor();
        }

        private static void WriteSuccess(string text)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        private static void WriteWarning(string text)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        private static void PauseForUser()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("  Press any key to continue...");
            Console.ResetColor();
            Console.ReadKey();
        }
    }
}
