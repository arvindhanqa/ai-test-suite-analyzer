# AI Test Suite Analyzer

> AI-powered test case quality analysis and requirement coverage validation — analyze 56 test cases for $0.001, re-run for free

An intelligent test analysis tool that reads Excel test cases, evaluates quality using AI, and validates requirement coverage. Supports two analysis modes for QA Engineers and Business Analysts.

---

## 🎯 Problem Statement

Manual test case review is:
- ⏰ **Time-consuming**: 5-10 minutes per test case
- 👁️ **Inconsistent**: Quality varies by reviewer
- 📈 **Not scalable**: Impossible to review 500+ test cases regularly
- 💼 **Expensive**: Senior QA time costs $50-100/hour
- 🕳️ **Gap-blind**: Hard to spot which requirements have no test coverage

**This tool solves all of that.**

---

## ✨ Features

### 🆕 Dual Analysis Modes (Phase 2)

The tool now supports two distinct analysis modes depending on your role and goal:

#### QA Mode — Test Quality Analysis
For QA Engineers who want to assess test case quality without requirements.

- Analyzes each test for completeness, clarity, and best practices
- Single "AI Analysis" column output (blue header)
- Color-coded: Green = GOOD, Orange = Issues, Red = Errors
- ~150 tokens per test (~$0.001 for 56 tests)
- Full cache support (repeat runs = $0.00)

**Sample QA Mode output:**
```
TC-001  GOOD
TC-002  GOOD
TC-003  INCOMPLETE - Missing detailed requirements for user registration process.
TC-004  GOOD
```

#### BA Mode — Requirement Coverage Analysis
For Business Analysts and QA Leads who want to validate requirement coverage.

- Identifies which requirements each test covers
- Flags missing requirement coverage with actionable ❌ items
- Two-column output: "Requirement Feedback" (coral) + "Coverage" (green)
- Coverage shows specific requirement IDs (FR-AUTH-001, TM-003, etc.)
- ~800-1000 tokens per test (~$0.007 for 56 tests)
- Mode-aware cache (separate namespace from QA cache, repeat runs = $0.00)
- **Coverage Gap Analysis sheet** — one row per requirement showing which tests cover it

**Sample BA Mode output:**
```
Requirement Feedback                                          Coverage
❌ Email format validation missing (FR-AUTH-002) - add step  FR-AUTH-001, FR-AUTH-002
❌ Password strength validation missing (FR-AUTH-003)...
❌ Age confirmation checkbox validation missing (BR-AUTH-001)
```

**Coverage Gap Analysis sheet:**
```
Req ID       Description                    Tests Covering It    Count  Status
FR-AUTH-001  new users create account...    TC-001, TC-002       5      ✅ COVERED (5 tests)
BR-AUTH-001  each email linked to one...                         0      ❌ NOT COVERED
VR-AUTH-001  email must be valid format...                       0      ❌ NOT COVERED
```

---

### Core Capabilities
- 📊 **AI-Powered Analysis**: Uses GPT-4o-mini to evaluate test quality and requirement coverage
- 📈 **Multi-Sheet Excel Output**: Separate sheets for detailed analysis, issues, and statistics
- ⚡ **Real-Time Progress**: Visual progress bar with ETA calculation
- 💰 **Cost-Optimized**: 84% token reduction through prompt engineering
- 🎨 **Color-Coded Feedback**: Green for good tests, orange for issues, red for errors
- 📋 **Actionable Insights**: Specific, concise improvement suggestions per test
- 🔄 **Retry Logic**: Automatic retry with exponential backoff for API failures
- ✅ **Excel Validation**: Pre-flight checks before processing
- 🎯 **Professional Output**: Freeze panes, auto-filters, optimized column widths

---

### Interactive File Selector

No command-line arguments needed. An interactive menu guides you through everything:

```
  AI TEST SUITE ANALYZER

  What would you like to do?

    [1] Analyze a single Excel file
    [2] Batch analyze all Excel files in a folder
    [3] Exit
```

After selecting a file, you choose your analysis mode:

```
  SELECT ANALYSIS MODE

    [1] QA Mode  — Test quality analysis (no requirements needed)
    [2] BA Mode  — Requirement coverage validation
```

**Single file flow:**
- Auto-discovers Excel files in common project locations
- Shows test count before you commit to running
- Configure test limit and sheet index from the menu

**Batch flow:**
- Discovers folders containing Excel files
- Applies selected mode across all files in folder
- Shared cache across files for maximum cost savings

---

### Intelligent Requirement Auto-Detection (BA Mode)

BA Mode automatically finds the matching requirements file based on your test file name:

```
test_cases_taskflow.xlsx  →  auto-finds  requirements_taskflow.md
test_cases_shopease.xlsx  →  auto-finds  requirements_shopease.md
```

If the file isn't found, you're prompted to enter the path manually. Requirements are extracted once via AI and cached — subsequent runs use cached requirements at zero cost.

---

### Intelligent Caching System

A production-ready dual-layer cache system:

**Test Analysis Cache (QA + BA)**
- Content-aware: Tests cached by SHA256 hash of test content (not Test ID)
- Mode-separated: QA results use plain hash, BA results use `ba_` prefix hash — no collisions
- 30-day expiry with automatic cleanup
- Persistent across application restarts

**Requirement Cache**
- Requirement documents extracted once, cached by file path + modification date
- Subsequent BA Mode runs load requirements instantly at $0.00

**Real-world impact:**
- First run (56 tests, BA Mode): ~$0.007, ~3 minutes
- Second run (same tests): $0.00, ~4 seconds ⚡
- Batch (2 files, 100% cached): $0.00, 3.7 seconds for 20 tests

---

### Batch Processing

Process multiple Excel files in a single run with full mode support:

- **Multi-File Processing**: Analyze entire folders in one command
- **Mode Consistent**: Selected mode (QA/BA) applies to all files in batch
- **Separate Output Files**: Each input file gets its own timestamped report
- **Shared Cache**: Cache works across files — same test in two files = one API call
- **Aggregate Statistics**: Combined summary across all files

**Example batch summary:**
```
┌─────────────────────────────────┬────────┬─────────┬────────┬──────────┬────────────┐
│ File                            │ Tests  │ Quality │ Cache  │ Tokens   │ Cost       │
├─────────────────────────────────┼────────┼─────────┼────────┼──────────┼────────────┤
│ test_cases_shopease.xlsx        │     10 │   0.0%  │  10/10 │        0 │ $0.000000  │
│ test_cases_taskflow.xlsx        │     10 │   0.0%  │  10/10 │        0 │ $0.000000  │
└─────────────────────────────────┴────────┴─────────┴────────┴──────────┴────────────┘

📈 AGGREGATE STATISTICS
   Files processed:     2
   Total tests:         20
   Cache hits:          20 (100.0%)
   Total cost:          $0.000000
   Total time:          3.7 seconds
```

---

## 🏗️ Architecture

```
FileSelector (Interactive Menu + Mode Selection)
       ↓
  [QA Mode]                    [BA Mode]
      ↓                            ↓
ExcelReader              RequirementExtractor → RequirementCache
      ↓                            ↓
AIAnalyzer.AnalyzeTestQuality   AIAnalyzer.AnalyzeCoverageAndFeedback
      ↓                            ↓
  TestCaseCache              TestCaseCache (ba_ prefix)
      ↓                            ↓
ExcelWriter (1 column)     ExcelWriter (2 columns + Gap Sheet)
      ↓                            ↓
[AI Analysis Sheet]        [Requirement Feedback + Coverage + Gap Analysis]

BatchProcessor → Applies selected mode across all files → Aggregate Summary
```

### Code Structure
```
AITestAnalyzer/
├── Program.cs                  # Main orchestration + mode routing
├── Configuration.cs            # App configuration model
├── PromptConfig.cs             # AI prompt configuration
├── TestCase.cs                 # Test case data model
├── FileSelector.cs             # Interactive menu + mode selection
├── ExcelReader.cs              # Excel reading + validation
├── ExcelWriter.cs              # Mode-aware Excel writing + formatting
├── AIAnalyzer.cs               # OpenAI integration (2 analysis methods)
├── ProgressTracker.cs          # Real-time progress display
├── SummaryDisplay.cs           # Console output formatting
├── TestCaseCache.cs            # Dual-mode cache (QA + BA namespaces)
├── RequirementExtractor.cs     # AI requirement extraction
├── RequirementCache.cs         # Requirement document cache
├── ExtractedRequirement.cs     # Requirement data model
├── ConfigurationValidator.cs   # Startup validation
└── BatchProcessor.cs           # Multi-file batch (mode-aware)
```

**Key Design Principles:**
- ✅ Single Responsibility (each class has one job)
- ✅ Modular "lego block" architecture — modes snap in cleanly
- ✅ Context-aware analysis (93% API cost reduction vs separate passes)
- ✅ Backward-compatible cache migration

---

## 📦 Installation

### Prerequisites
- Visual Studio 2022 (or later)
- .NET 10.0 SDK
- OpenAI API Key ([Get one here](https://platform.openai.com))

### Setup Steps

1. **Clone the repository**
```bash
git clone https://github.com/arvindhanqa/ai-test-suite-analyzer.git
cd ai-test-suite-analyzer
```

2. **Configure API Key**
```bash
cd src/AITestAnalyzer
cp appsettings.json.sample appsettings.json
```

Edit `appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "YOUR-API-KEY-HERE",
    "Model": "gpt-4o-mini"
  }
}
```

3. **Install Dependencies**
```bash
dotnet restore
```

4. **Build and Run**
```bash
dotnet build
dotnet run
```

The interactive menu handles everything from here.

---

## Usage

### Interactive Mode (Default)
```bash
dotnet run
```

### Cache Controls
```bash
dotnet run -- --no-cache       # Force fresh analysis
dotnet run -- --clear-cache    # Clear all cached results
```

### Help and Version
```bash
dotnet run -- --help
dotnet run -- --version
```

---

## 💰 Cost Analysis

### QA Mode Token Usage

| Version | Tokens/Test | Cost/Test | 56 Tests |
|---------|-------------|-----------|----------|
| Initial (Verbose) | 750 | $0.000113 | $0.0063 |
| Optimized | 154 | $0.000023 | $0.0013 |
| **With Cache (100% Hit)** | **0** | **$0.00** | **$0.00** ⚡ |

### BA Mode Token Usage

| Scenario | Tokens/Test | Cost/Test | 56 Tests |
|----------|-------------|-----------|----------|
| Fresh analysis | ~900 | $0.000135 | $0.0075 |
| **With Cache (100% Hit)** | **0** | **$0.00** | **$0.00** ⚡ |

### Real-World Cost Scenarios

| Scenario | Tests | Mode | Cost | Time |
|----------|-------|------|------|------|
| First run | 56 | QA | ~$0.001 | ~2 min |
| First run | 56 | BA | ~$0.007 | ~3 min |
| Re-run (100% cached) | 56 | QA or BA | $0.00 | ~0.4s |
| Batch (2 files, first run) | 86 | BA | ~$0.012 | ~5 min |
| Batch (2 files, 100% cached) | 86 | BA | $0.00 | ~4s |

**Your $10 OpenAI budget covers hundreds of fresh full-suite analyses — or virtually unlimited re-runs with cache.**

---

## 🛠️ Technology Stack

- **Language**: C# (.NET 10.0)
- **Excel Processing**: EPPlus 7.x
- **AI Integration**: Betalgo.OpenAI 8.7.2
- **AI Model**: OpenAI GPT-4o-mini (temperature: 0.2)
- **Configuration**: Microsoft.Extensions.Configuration
- **Platform**: Windows 10/11

---

## 📋 Project Structure
```
ai-test-suite-analyzer/
├── src/
│   └── AITestAnalyzer/
│       ├── Program.cs
│       ├── FileSelector.cs
│       ├── AIAnalyzer.cs
│       ├── ExcelReader.cs
│       ├── ExcelWriter.cs
│       ├── TestCaseCache.cs
│       ├── RequirementExtractor.cs
│       ├── RequirementCache.cs
│       ├── ExtractedRequirement.cs
│       ├── BatchProcessor.cs
│       ├── ProgressTracker.cs
│       ├── SummaryDisplay.cs
│       ├── ConfigurationValidator.cs
│       ├── Configuration.cs
│       ├── PromptConfig.cs
│       ├── TestCase.cs
│       ├── appsettings.json
│       └── PromptConfig.json
├── data/
│   ├── requirements_shopease.md
│   ├── test_cases_shopease.xlsx
│   ├── requirements_taskflow.md
│   └── test_cases_taskflow.xlsx
├── cache/                          # Cache storage (gitignored)
├── output/                         # Generated analysis reports
├── docs/
│   └── daily-logs/                 # Development progress logs
└── README.md
```

---

## 🎯 Roadmap

### Phase 1 ✅ COMPLETE (Days 1-14)
- [x] Environment setup and test data creation
- [x] Excel reading with EPPlus
- [x] OpenAI API integration (GPT-4o-mini)
- [x] Cost optimization (84% token reduction)
- [x] Professional code architecture
- [x] Multi-sheet Excel output with color coding
- [x] Real-time progress bar with ETA
- [x] Statistics dashboard
- [x] Error handling with automatic retry logic
- [x] Intelligent cache system (content-aware, 30-day expiry)
- [x] Batch processing (multiple Excel files)
- [x] Interactive FileSelector menu

### Phase 2 ✅ COMPLETE (Days 15-20)
- [x] Requirement extraction via AI (pipe-delimited, compressed format)
- [x] Requirement caching system
- [x] Context-aware test analysis (93% cost reduction vs separate passes)
- [x] QA Mode — quality-only analysis
- [x] BA Mode — requirement coverage gap analysis
- [x] Auto-detection of requirement files from test file naming
- [x] Coverage column in Excel output (color-coded, ID-based)
- [x] Dual-mode cache (separate QA/BA namespaces, no collisions)
- [x] Batch mode fully mode-aware (QA/BA across all files)

### Phase 3 ✅ IN PROGRESS (Days 21-23)
- [x] BA Mode cache + batch mode fix (Day 21)
- [x] Coverage Gap Analysis sheet — color-coded per-requirement status (Day 22)
- [x] Fix FormatRequirements to include IDs in AI prompt (Day 22)
- [x] Fix coverage storage to use raw IDs for accurate gap mapping (Day 22)
- [x] Dead code cleanup — removed unused AIAnalyzer methods (Day 23)
- [x] Fix row height truncation in Coverage Gap sheet (Day 23)
- [ ] QA Mode summary sheets in batch
- [ ] Export enhancements

### Future
- [ ] Web interface (Blazor)
- [ ] JIRA/TestRail integration
- [ ] CI/CD pipeline integration
- [ ] Local LLM support (Ollama)

---

## 👤 Author

**Aravindhan Rajasekaran**
- **Current Role**: Lead Test Engineer @ Acumatica (2020-Present)
- **Experience**: 10+ years in QA, Test Automation, and Software Development
- **Expertise**: C#, Java, Python, Selenium, API Testing, CI/CD
- **Certifications**: ISTQB Certified, Certified Scrum Master
- **GitHub**: [@arvindhanqa](https://github.com/arvindhanqa)
- **LinkedIn**: [linkedin.com/in/aravindrajsekar](https://www.linkedin.com/in/aravindrajsekar)
- **Location**: Saskatoon, Saskatchewan, Canada

### Notable Achievements
- 🏆 Increased test automation coverage from 15% to 95% (3,000 automated tests)
- 🏆 Reduced production bugs from 50 to 5 per month through Test SOP implementation
- 🏆 Led distributed QA teams across USA, Serbia, and Sri Lanka
- 🏆 Created in-house test coverage calculator tool
- 🏆 Automated 500+ scenarios for 22 new features with 100% coverage

---

## 🤝 About This Project

This is a personal project built as part of a 90-day commitment (January 20 - April 19, 2026) to build practical AI-powered tooling and demonstrate the ability to ship complete software from concept to production.

**Status (Day 23)**: Phase 3 in progress. Coverage Gap Analysis sheet complete. Dual-mode QA/BA analysis verified in single and batch modes. 23 consecutive days of commits.

### Development Progress
- **Days 1-7 (Phase 0)**: Foundation — setup, data, Excel processing, OpenAI integration, cost optimization
- **Days 8-14 (Phase 1)**: Professional features — progress bar, caching, error handling, CLI, batch processing, interactive menu
- **Days 15-20 (Phase 2)**: Intelligence — requirement extraction, dual-mode analysis (QA/BA), coverage tracking, mode-aware cache
- **Days 21-23 (Phase 3)**: Polish — BA Mode cache for batch, Coverage Gap Analysis sheet, dead code cleanup

---

## 📝 License

MIT License - Feel free to use and modify for your own projects.

---

## 🙏 Acknowledgments

Built with:
- OpenAI GPT-4o-mini for intelligent test analysis
- EPPlus for Excel manipulation
- Visual Studio 2022 for development

---

**⭐ If you find this useful, please star the repository!**

---

*Last Updated: February 13, 2026 (Day 23)*