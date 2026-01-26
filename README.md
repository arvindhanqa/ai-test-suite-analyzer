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

### Output Sheets
1. **AI Detailed Analysis**: Original test cases with AI feedback column
2. **Quality Issues Summary**: Filtered list of tests needing improvement
3. **Statistics Dashboard**: Executive-level metrics and recommendations

### Quality Checks
- ✅ Test completeness (preconditions, steps, expected results)
- ✅ Clarity and specificity
- ✅ Best practices compliance
- ✅ Missing error scenarios
- ✅ Boundary condition coverage

---

## 📸 Screenshots

### Real-Time Progress Bar
```
[============>.......] 67.9% | 38/56 | TC-038 | ETA: 35s
```

### Sample Output
Multi-sheet Excel file with:
- AI Detailed Analysis (color-coded feedback)
- Quality Issues Summary (actionable list)
- Statistics Dashboard (executive metrics)

---

## 🏗️ Architecture
```
Excel Input → AI Test Analyzer → OpenAI API → Analysis Engine → Excel Output
                                                                      ↓
                                                          [3 Formatted Sheets]
```

**Processing Flow:**
1. Read test cases from Excel (EPPlus)
2. Send to OpenAI GPT-4o-mini for analysis
3. Parse and categorize AI feedback
4. Write results to multiple Excel sheets
5. Apply formatting and color coding
6. Generate statistics and recommendations

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
   
   Edit `src/AITestAnalyzer/appsettings.json`:
```json
   {
     "OpenAI": {
       "ApiKey": "YOUR-API-KEY-HERE",
       "Model": "gpt-4o-mini"
     },
     "Excel": {
       "FilePath": "../../data/test_cases_shopease.xlsx"
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

3. **Run the analyzer**
```bash
   dotnet run
```

4. **Find results** in `output/` folder:
   - `analysis_results_YYYYMMDD_HHMMSS.xlsx`

### Sample Output
```
AI Test Suite Analyzer - Week 1
===============================================

📋 Loading configuration...
   ✅ Model: gpt-4o-mini
   ✅ Max Tokens: 150

📁 Preparing output file...
   ✅ Output file: analysis_results_20260126_133111.xlsx

📊 Analyzing 56 test cases...
   [====================] 100.0% | 56/56 | TC-056 | ETA: 0s
   ✅ Analysis complete!

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

📁 Output: analysis_results_20260126_133111.xlsx
===============================================
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
- **AI Model**: OpenAI GPT-4o-mini
- **Configuration**: Microsoft.Extensions.Configuration
- **Platform**: Windows 10/11

---

## 📋 Project Structure
```
ai-test-suite-analyzer/
├── src/
│   └── AITestAnalyzer/
│       ├── Program.cs                 # Main application logic
│       ├── Configuration.cs           # App configuration class
│       ├── PromptConfig.cs           # AI prompt configuration
│       ├── TestCase.cs               # Test case model
│       ├── appsettings.json          # App settings (API key)
│       └── PromptConfig.json         # Prompt templates
├── data/
│   ├── requirements_shopease.md      # Sample requirements
│   └── test_cases_shopease.xlsx      # Sample test cases
├── output/                            # Generated analysis reports
└── README.md
```

---

## 🎯 Roadmap

### Week 1 (Current) ✅
- [x] Multi-sheet Excel output
- [x] Real-time progress bar
- [x] Statistics dashboard
- [x] Cost optimization (84% reduction)

### Weeks 5-8 (Planned)
- [ ] **Coverage Gap Analysis**: Compare tests against requirements
- [ ] **Flow Correctness Validation**: Verify test steps match requirement flows
- [ ] **Step Completeness Check**: Ensure all validation steps present

### Future Enhancements
- [ ] Batch processing (multiple Excel files)
- [ ] Command-line arguments
- [ ] Configurable test count
- [ ] Export to PDF/HTML
- [ ] Integration with JIRA/TestRail

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

**Status (Day 8)**: On schedule. Tool is production-ready and delivering value.

---

## 📝 License

MIT License - Feel free to use and modify for your own projects.

---

**⭐ If you find this useful, please star the repository!**