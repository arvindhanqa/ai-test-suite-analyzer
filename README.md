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
- ⚙️ **Configurable worksheet** selection (supports multiple Excel templates)
- 📝 **Clear error messages** for troubleshooting

---

## 📸 Screenshots

### Real-Time Progress Bar
```
[============>.......] 67.9% | 38/56 | TC-038 | ETA: 35s
```

### Excel Validation
```
🔍 Validating Excel structure...
   ✅ Excel structure is valid
```

### Interactive Test Count Prompt
```
📊 Found 56 test cases in Excel.
   How many tests to analyze? (Enter number or press Enter for all):
```

### Sample Output
Multi-sheet Excel file with:
- **AI Detailed Analysis** (color-coded feedback, freeze panes, auto-filters)
- **Quality Issues Summary** (actionable list with filters)
- **Statistics Dashboard** (executive metrics with recommendations)

---

## 🏗️ Architecture
```
Excel Input → ExcelReader → AIAnalyzer → OpenAI API (with retry)
                ↓              ↓              ↓
           Validation    ProgressTracker   Error Handling
                ↓              ↓              ↓
           ExcelWriter → [3 Professional Sheets] → Output
```

### Code Structure (Professional SOLID Architecture)
```
AITestAnalyzer/
├── Program.cs              # Main orchestration (209 lines)
├── Configuration.cs        # App configuration model
├── PromptConfig.cs         # AI prompt configuration
├── TestCase.cs            # Test case data model
├── ExcelReader.cs         # Excel reading + validation
├── ExcelWriter.cs         # Excel writing + formatting
├── AIAnalyzer.cs          # OpenAI integration + retry logic
├── ProgressTracker.cs     # Real-time progress display
└── SummaryDisplay.cs      # Console output formatting
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

2. **Configure API Key and Settings**
   
   Edit `src/AITestAnalyzer/appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "YOUR-API-KEY-HERE",
    "Model": "gpt-4o-mini"
  },
  "Excel": {
    "FilePath": "../../data/test_cases_shopease.xlsx",
    "WorksheetIndex": 1
  }
}
```

3. **Install Dependencies**
```bash
cd src/AITestAnalyzer
dotnet restore
```

4. **Build**
```bash
dotnet build
```

---

## 🚀 Usage

### Quick Start

1. **Prepare your Excel file** with columns:
   - Test ID | Feature | Scenario | Priority | Steps | Expected Result | Status

2. **Update configuration** in `appsettings.json`:
   - Set your OpenAI API key
   - Point to your Excel file path
   - Set worksheet index (0 = first sheet, 1 = second sheet, etc.)

3. **Run the analyzer**
```bash
dotnet run
```

   **OR with command-line arguments:**
```bash
dotnet run -- 10    # Analyze only 10 tests
```

4. **Interactive mode**: If no arguments provided, the tool will prompt:
```
📊 Found 56 test cases in Excel.
   How many tests to analyze? (Enter number or press Enter for all):
```

5. **Find results** in `output/` folder:
   - `analysis_results_YYYYMMDD_HHMMSS.xlsx`

### Sample Output
```
===============================================
AI Test Suite Analyzer - Week 1
===============================================

📋 Loading configuration...
   ✅ Model: gpt-4o-mini
   ✅ Max Tokens: 150

📁 Preparing output file...
   ✅ Output file: analysis_results_20260128_153349.xlsx
   ✅ Renamed sheet to 'AI Detailed Analysis'

🔍 Validating Excel structure...
   ✅ Excel structure is valid

📊 Found 56 test cases in Excel.
   How many tests to analyze? (Enter number or press Enter for all): 56
   → Analyzing all 56 test cases...

[====================] 100.0% | 56/56 | TC-056 | ETA: 0s
✅ Analysis complete!

📋 Creating Quality Issues Summary...
   ✅ Created 'Quality Issues Summary' sheet
📊 Creating Statistics Dashboard...
   ✅ Created 'Statistics Dashboard' sheet

===============================================
📊 ANALYSIS SUMMARY
===============================================
Tests analyzed: 56
✅ Good tests: 3 (5%)
⚠️  Tests with issues: 53 (95%)

Total tokens: 6,932
Total cost: $0.001040
Avg tokens/test: 123
⏱️  Time: 150.2 seconds

📁 Output: analysis_results_20260128_153349.xlsx
   Location: C:\Projects\...\output
===============================================
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

### Example Error Handling
```
🔍 Validating Excel structure...
   ❌ VALIDATION ERROR: Excel has only 3 columns, need at least 5
   Please check your Excel file and try again.
```
```
⚠️  API error (attempt 1/3): Rate limit exceeded
⏳ Retrying in 1 seconds...
```

---

## 💰 Cost Analysis

### Actual Performance (56 Tests)
- **Total tokens**: 6,932
- **Total cost**: $0.001040
- **Cost per test**: $0.000019
- **Time**: 150 seconds (~2.7 sec/test)

### Optimization Journey
| Iteration | Tokens/Test | Cost/56 Tests | Savings |
|-----------|-------------|---------------|---------|
| Initial   | 750         | $0.0063       | -       |
| Optimized | 184         | $0.0015       | 75%     |
| Final     | 123         | $0.001        | 84%     |

### Budget Projections
- **$10 budget**: ~555,000 test analyses
- **500 tests**: $0.0093
- **1,000 tests**: $0.0186
- **Annual (4 runs x 500 tests)**: $0.037

**Result**: Cost is negligible for realistic usage.

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
│       ├── Program.cs                 # Main orchestration (209 lines)
│       ├── Configuration.cs           # App configuration class
│       ├── PromptConfig.cs           # AI prompt configuration
│       ├── TestCase.cs               # Test case model
│       ├── ExcelReader.cs            # Excel reading + validation
│       ├── ExcelWriter.cs            # Excel writing + formatting
│       ├── AIAnalyzer.cs             # OpenAI integration + retry
│       ├── ProgressTracker.cs        # Progress display
│       ├── SummaryDisplay.cs         # Console output
│       ├── appsettings.json          # App settings (API key)
│       └── PromptConfig.json         # Prompt templates
├── data/
│   ├── requirements_shopease.md      # Sample requirements
│   └── test_cases_shopease.xlsx      # Sample test cases (56 tests)
├── output/                            # Generated analysis reports
├── docs/
│   └── daily-logs/                   # Development progress logs
└── README.md
```

---

## 🎯 Roadmap

### Week 1 ✅ COMPLETE (Days 1-9)
- [x] Multi-sheet Excel output with professional formatting
- [x] Real-time progress bar with ETA
- [x] Statistics dashboard with recommendations
- [x] Cost optimization (84% token reduction)
- [x] Error handling with automatic retry logic
- [x] Excel validation before processing
- [x] Freeze panes, auto-filters, column auto-sizing
- [x] Interactive test count selection
- [x] Configurable worksheet index
- [x] Professional code architecture (68% refactoring)

### Week 2 (Days 10-16) - Planned
- [ ] Enhanced error reporting
- [ ] Batch processing (multiple Excel files)
- [ ] Test case generation from requirements
- [ ] Export to PDF/HTML

### Weeks 5-8 - Advanced Features
- [ ] **Coverage Gap Analysis**: Compare tests against requirements
- [ ] **Flow Correctness Validation**: Verify test steps match requirement flows
- [ ] **Step Completeness Check**: Ensure all validation steps present

### Future Enhancements
- [ ] Web interface (Blazor)
- [ ] Integration with JIRA/TestRail
- [ ] CI/CD pipeline integration
- [ ] Local LLM support (Ollama)
- [ ] Vector database for semantic search

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

This is a personal learning project built as part of a 90-day commitment (January 20 - April 19, 2026) to:
1. Break a 20-year pattern of unfinished projects
2. Build practical AI-powered tooling
3. Demonstrate ability to ship complete software from concept to production

**Status (Day 9)**: Week 1 complete. Tool is production-ready with professional-grade features.

### Development Progress
- **Days 1-7**: Foundation (setup, data, basic processing)
- **Day 8**: Major refactoring (68% code reduction), professional Git workflow
- **Day 9**: Excel polish (freeze panes, auto-filters, column optimization)
- **Next**: Week 2 enhancements and advanced features

---

## 📝 License

MIT License - Feel free to use and modify for your own projects.

---

## 🙏 Acknowledgments

Built with:
- OpenAI GPT-4o-mini for intelligent test analysis
- EPPlus for Excel manipulation
- Visual Studio 2022 for development

Special thanks to the open-source community for the excellent libraries.

---

**⭐ If you find this useful, please star the repository!**

---

*Last Updated: January 28, 2026 (Day 9)*