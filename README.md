# AI Test Suite Analyzer

> Automated test case quality analysis using OpenAI GPT-4o-mini - Analyze 56 test cases for $0.001

An intelligent test analysis tool that reads Excel test cases, evaluates quality using AI, and generates comprehensive reports with actionable insights.

---

## 🎯 Problem Statement

Manual test case review is:
- ⏰ **Time-consuming**: 5-10 minutes per test case
- 👁️ **Inconsistent**: Quality varies by reviewer
- 📈 **Not scalable**: Impossible to review 500+ test cases regularly
- 💼 **Expensive**: Senior QA time costs $50-100/hour

**This tool solves that.**

---

## ✨ Features

### Core Capabilities
- 📊 **AI-Powered Analysis**: Uses GPT-4o-mini to evaluate test quality
- 📈 **Multi-Sheet Excel Output**: Separate sheets for detailed analysis, issues, and statistics
- ⚡ **Real-Time Progress**: Visual progress bar with ETA calculation
- 💰 **Cost-Optimized**: 84% token reduction through prompt engineering ($0.001 for 56 tests)
- 🎨 **Color-Coded Feedback**: Green for good tests, orange for issues, red for errors
- 📋 **Actionable Insights**: Specific, concise improvement suggestions
- 🔄 **Retry Logic**: Automatic retry with exponential backoff for API failures
- ✅ **Excel Validation**: Pre-flight checks before processing
- 🎯 **Professional Output**: Freeze panes, auto-filters, optimized column widths

### 🆕 Interactive File Selector (NEW!)
No more command-line arguments or config file editing to run the tool. An interactive menu guides you through everything:

```
  AI TEST SUITE ANALYZER

  What would you like to do?

    [1] Analyze a single Excel file
    [2] Batch analyze all Excel files in a folder
    [3] Exit
```

**Single file flow:**
- Auto-discovers Excel files in common project locations
- Shows test count before you commit to running
- Configure test limit and sheet index from the menu
- Validates the file via EPPlus before you even see the settings screen

**Batch flow:**
- Discovers folders containing Excel files
- Shows file count per folder
- Same limit/sheet controls apply across all files

### Batch Processing
Process multiple Excel files in a single run:

- **Multi-File Processing**: Analyze entire folders of test suites at once
- **Separate Output Files**: Each input file gets its own timestamped analysis report
- **Aggregate Statistics**: Combined summary showing quality across all files
- **Color-Coded Table**: Visual comparison of quality scores per file
- **Cache Integration**: Cached results work across files for massive cost savings

**Example batch output:**
```
┌─────────────────────────────────┬────────┬─────────┬──────────┬────────────┐
│ File                            │ Tests  │ Quality │ Tokens   │ Cost       │
├─────────────────────────────────┼────────┼─────────┼──────────┼────────────┤
│ test_cases_shopease.xlsx        │     56 │   5.4%  │    6,900 │ $0.001035  │
│ test_cases_taskflow.xlsx        │     30 │  10.0%  │    3,700 │ $0.000555  │
└─────────────────────────────────┴────────┴─────────┴──────────┴────────────┘

📈 AGGREGATE STATISTICS
   Files processed:     2
   Total tests:         86
   Overall quality:     7.0%
```

### Intelligent Caching System
A production-ready cache system that dramatically reduces API costs and analysis time:

- **Content-Aware Caching**: Tests are cached based on their content hash, not Test ID
- **Automatic Expiry**: Cache entries expire after 30 days to prevent stale analysis
- **Smart Detection**: Only changed tests are re-analyzed; unchanged tests use cached results
- **Cost Savings**: 100% cache hit = $0.00 API cost + instant results (0.4s vs 120s)
- **Persistent Storage**: Cache survives application restarts

**Real-world impact:**
- First run (56 tests): ~$0.001 cost, ~2 minutes
- Second run (same tests): $0.00 cost, ~0.4 seconds ⚡
- Modified 5 tests: ~$0.0001 cost (only changed tests re-analyzed)

### Output Sheets
1. **AI Detailed Analysis**: Original test cases with AI feedback column
   - Freeze panes (header stays visible when scrolling)
   - Auto-filter on all columns (one-click filtering)
   - Color-coded feedback (green/orange/red)
   - Optimized column widths

2. **Quality Issues Summary**: Filtered list of tests needing improvement
   - Only problematic tests shown
   - Auto-filter enabled
   - Actionable issue descriptions

3. **Statistics Dashboard**: Executive-level metrics and recommendations
   - Quality score with color coding
   - Test breakdown by category
   - Cost and performance metrics
   - Budget projections
   - Dynamic recommendations

### Quality Checks
- ✅ Test completeness (preconditions, steps, expected results)
- ✅ Clarity and specificity
- ✅ Best practices compliance
- ✅ Missing error scenarios
- ✅ Boundary condition coverage

### Reliability Features
- 🔄 **Automatic retry** with exponential backoff (3 attempts: 1s, 2s, 4s)
- ✅ **Excel structure validation** before processing
- 🛡️ **Graceful error handling** (continues on individual test failures)
- 📝 **Clear error messages** for troubleshooting

---

## 📸 Screenshots

### Interactive Main Menu
```
  AI TEST SUITE ANALYZER

  What would you like to do?

    [1] Analyze a single Excel file
    [2] Batch analyze all Excel files in a folder
    [3] Exit

  Enter your choice (1-3):
```

### Configure Analysis Screen
```
  CONFIGURE ANALYSIS

  📄 File: test_cases_shopease.xlsx
      Path: C:\Projects\ai-test-analyzer\data
      Tests found: 56

  Current settings:
      Tests to analyze: 7
      Sheet index:      1

  Options:
    [1] Analyze ALL 56 tests
    [2] Analyze first N tests
    [3] Change sheet index
    [R] Run with current settings
    [B] Back
```

### Real-Time Progress Bar
```
[============>.......] 67.9% | 38/56 | TC-038 | ETA: 35s
```

### Cache Performance (100% hit)
```
  CACHE PERFORMANCE
   ✅ Cache hits:       7 (100.0%)
   📡 API calls:        0 (0.0%)
   💾 Tokens saved:     ~1,050
   💰 Cost saved:       ~$0.000157
```

---

## 🏗️ Architecture
```
FileSelector (Interactive Menu)
       ↓
Excel Input → ExcelReader → AIAnalyzer → OpenAI API (with retry)
                ↓              ↓              ↓
           Validation    ProgressTracker   Error Handling
                ↓              ↓              ↓
           ExcelWriter → [3 Professional Sheets] → Output
                ↓
         BatchProcessor → [Multiple Files] → Aggregate Summary
```

### Code Structure (Professional SOLID Architecture)
```
AITestAnalyzer/
├── Program.cs              # Main orchestration
├── Configuration.cs        # App configuration model
├── PromptConfig.cs         # AI prompt configuration
├── TestCase.cs             # Test case data model
├── FileSelector.cs         # Interactive menu system (NEW!)
├── ExcelReader.cs          # Excel reading + validation
├── ExcelWriter.cs          # Excel writing + formatting
├── AIAnalyzer.cs           # OpenAI integration + retry logic
├── ProgressTracker.cs      # Real-time progress display
├── SummaryDisplay.cs       # Console output formatting
├── TestCaseCache.cs        # Intelligent cache system
├── ConfigurationValidator.cs # Startup validation
└── BatchProcessor.cs       # Multi-file batch processing
```

**Key Design Principles:**
- ✅ Single Responsibility Principle (each class has one job)
- ✅ Dependency Injection via constructors
- ✅ Separation of Concerns (I/O, AI, UI separated)
- ✅ 68% code reduction from initial monolithic design

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

   Copy the sample config and add your key:
```bash
cd src/AITestAnalyzer
cp appsettings.json.sample appsettings.json
```

   Edit `appsettings.json` and replace `YOUR-OPENAI-API-KEY-HERE` with your actual API key:
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

The interactive menu handles everything from here — no file paths or flags needed.

---

## ⚙️ Configuration Validation

The tool automatically validates your configuration on startup:
```
🔍 Validating configuration...

✅ API key format valid
✅ Worksheet index valid (Sheet: 'Sheet2')
✅ OpenAI API connection successful
```

**Validation checks:**
- ✅ API key exists and has correct format (starts with `sk-`)
- ✅ Selected Excel file is accessible (not locked by another program)
- ✅ Worksheet index is within valid range
- ✅ OpenAI API connection works (test API call)

---

## Usage

### Interactive Mode (Default)

Just run the tool:
```bash
dotnet run
```

The interactive menu walks you through:
1. Choose single file or batch mode
2. Select your Excel file (auto-discovered or manual path)
3. Configure test count and sheet index
4. Hit Run

### Cache Controls

**Skip cache (force fresh analysis):**
```bash
dotnet run -- --no-cache
```

**Clear all cached results:**
```bash
dotnet run -- --clear-cache
```

### Help and Version
```bash
dotnet run -- --help
dotnet run -- --version
```

---

## 🛡️ Error Handling

### Automatic Retry Logic
- **3 attempts** with exponential backoff (1s, 2s, 4s)
- Handles temporary network issues and rate limits
- Clear console feedback during retries

### Pre-Flight Validation
- Checks Excel file structure before processing
- Validates minimum column count
- Ensures header row exists
- Confirms at least one data row present

### Graceful Degradation
- Individual test failures don't stop processing
- Clear error messages for failed tests
- Tool continues analyzing remaining tests

---

## 💰 Cost Analysis

### Token Usage Optimization Journey

| Version | Tokens/Test | Cost/Test | 56 Tests | Savings |
|---------|-------------|-----------|----------|---------|
| Initial (Verbose) | 750 | $0.000113 | $0.0063 | - |
| Optimized (Tweet-style) | 184 | $0.000028 | $0.0015 | 76% |
| Final (Optimized) | 123-154 | $0.000018-$0.000023 | $0.001-$0.0013 | 84% |
| **With Cache (100% Hit)** | **0** | **$0.00** | **$0.00** | **100%** ⚡ |

### Real-World Cost Scenarios

| Scenario | Tests | Cost | Time |
|----------|-------|------|------|
| First run | 56 | ~$0.001 | ~2 min |
| Re-run (100% cached) | 56 | $0.00 | ~0.4s |
| Batch (2 files, first run) | 86 | ~$0.0016 | ~3 min |
| Batch (50% cached) | 86 | ~$0.0008 | ~1.5 min |

**Your $10 OpenAI budget can handle ~555,000 fresh analyses — or virtually unlimited re-runs with cache.**

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
│       ├── Program.cs                  # Main orchestration
│       ├── Configuration.cs            # App configuration class
│       ├── PromptConfig.cs             # AI prompt configuration
│       ├── TestCase.cs                 # Test case model
│       ├── FileSelector.cs             # Interactive menu system (NEW!)
│       ├── ExcelReader.cs              # Excel reading + validation
│       ├── ExcelWriter.cs              # Excel writing + formatting
│       ├── AIAnalyzer.cs               # OpenAI integration + retry
│       ├── ProgressTracker.cs          # Progress display
│       ├── SummaryDisplay.cs           # Console output
│       ├── TestCaseCache.cs            # Cache system
│       ├── ConfigurationValidator.cs   # Startup validation
│       ├── BatchProcessor.cs           # Multi-file batch processing
│       ├── appsettings.json            # App settings (API key)
│       └── PromptConfig.json           # Prompt templates
├── data/
│   ├── requirements_shopease.md        # Sample requirements
│   ├── test_cases_shopease.xlsx        # Sample test cases (56 tests)
│   ├── requirements_taskflow.md        # TaskFlow requirements
│   └── test_cases_taskflow.xlsx        # TaskFlow test cases (30 tests)
├── cache/                              # Cache storage (gitignored)
├── output/                             # Generated analysis reports
├── docs/
│   └── daily-logs/                     # Development progress logs
└── README.md
```

---

## 🎯 Roadmap

### Week 0 ✅ COMPLETE (Days 1-7)
- [x] Environment setup and dummy data creation
- [x] Excel reading with EPPlus
- [x] OpenAI API integration (GPT-4o-mini)
- [x] Cost optimization (84% token reduction)
- [x] Professional code architecture (68% refactoring)
- [x] Multi-sheet Excel output with color coding

### Week 1 ✅ COMPLETE (Days 8-9)
- [x] Real-time progress bar with ETA
- [x] Statistics dashboard with recommendations
- [x] Error handling with automatic retry logic
- [x] Excel validation before processing
- [x] Freeze panes, auto-filters, column auto-sizing
- [x] CLI arguments (--help, --version, --no-cache, --clear-cache)
- [x] Intelligent cache system with 30-day expiry
- [x] Content-aware caching (hash-based)

### Week 2 ✅ IN PROGRESS (Days 10-16)
- [x] Batch processing (multiple Excel files) ✅
- [x] TaskFlow test data (second test suite) ✅
- [x] **Interactive FileSelector menu** ✅ (replaces CLI argument parsing)
- [x] ConfigurationValidator cleanup (parameters from FileSelector) ✅
- [ ] Enhanced error reporting
- [ ] Export to PDF/HTML (optional)

### Weeks 5-8 — Advanced Features (Planned)
- [ ] Coverage Gap Analysis: Compare tests against requirements
- [ ] Flow Correctness Validation: Verify test steps match requirement flows
- [ ] Step Completeness Check: Ensure all validation steps present

### Future Enhancements
- [ ] Web interface (Blazor)
- [ ] Integration with JIRA/TestRail
- [ ] CI/CD pipeline integration
- [ ] Local LLM support (Ollama)

---

## 📊 Results

### Test Quality Distribution (Sample 56 Tests)
- **Good Quality**: 3 tests (5.4%)
- **Need Improvement**: 53 tests (94.6%)

### Common Issues Found
- Missing expected result details (42%)
- Vague test steps (31%)
- No error scenario coverage (18%)
- Unclear validation criteria (9%)

**AI correctly identified intentionally poor test cases with specific, actionable feedback.**

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

This is a personal learning project built as part of a 90-day commitment (January 20 - April 19, 2026) to build practical AI-powered tooling and demonstrate the ability to ship complete software from concept to production.

**Status (Day 11)**: Week 2 in progress. Interactive menu system complete. Tool is production-ready.

### Development Progress
- **Days 1-7 (Week 0)**: Foundation — setup, data, Excel processing, OpenAI integration, cost optimization
- **Days 8-9 (Week 1)**: Professional features — progress bar, caching, error handling, CLI, refactoring
- **Days 10-11 (Week 2)**: Scale — batch processing, second test suite, interactive FileSelector menu

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

*Last Updated: February 1, 2026 (Day 11)*