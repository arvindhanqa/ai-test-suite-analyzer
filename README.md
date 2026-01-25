# AI Test Suite Analyzer

Automated test case quality analysis using OpenAI GPT-4o-mini.

## What It Does

Analyzes test cases in Excel format and provides:
- Quality assessment (clear steps, specific scenarios, complete expected results)
- Issue identification (vague descriptions, missing details)
- Color-coded feedback (green = good, orange = issues)

## Current Status

✅ **Week 0 Complete** (January 20-25, 2026)
- 56 test cases analyzed in 104 seconds
- $0.001 per complete analysis
- 93% of test quality issues correctly identified

## How to Run
```bash
# Update appsettings.json with your OpenAI API key
dotnet run
```

## Results

- Input: Excel file with test cases
- Output: Same Excel + "AI Analysis" column with feedback
- Location: `output/analysis_results_[timestamp].xlsx`

## Tech Stack

- C# / .NET 10.0
- EPPlus (Excel processing)
- OpenAI API (GPT-4o-mini)
- Cost: ~$0.000018 per test

## Project Timeline

90-day commitment (January 20 - April 19, 2026)
- Week 0: ✅ Complete
- Week 1-4: Core functionality
- Week 5-8: Advanced features
- Week 9-12: Polish & launch

---

*Part of a 90-day journey to break a 20-year pattern of starting but not finishing projects.*
