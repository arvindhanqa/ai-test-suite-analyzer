# AI Test Suite Analyzer v2.0 — Demo Script

**Duration:** ~7 minutes  
**Date recorded:** June 2026  
**Version:** v2.0.0

---

## Setup (before hitting record)

- [ ] Clear `output/` folder
- [ ] Clear `cache/` folder  
- [ ] Terminal open at `src/AITestAnalyzer`
- [ ] Font size 20+ in terminal
- [ ] Excel closed
- [ ] Browser open at github.com/arvindhanqa/ai-test-suite-analyzer

---

## Scene 1 — GitHub (30 seconds)

Show the repo briefly:
- README with build badge
- v1.0.0 and v2.0.0 release tags
- 43 tests, clean CI

---

## Scene 2 — QA Mode (90 seconds)

```bash
dotnet run
```

- Select `[1] Analyze a single Excel file — QA Mode`
- Pick `test_cases_shopease.xlsx`
- Run ALL tests
- Show progress bar running
- Open output Excel → AI Detailed Analysis sheet
- Show Quality Issues Summary sheet
- Show Statistics Dashboard

---

## Scene 3 — BA Mode (60 seconds)

```bash
dotnet run
```

- Select `[2] Analyze a single Excel file — BA Mode`
- Pick `test_cases_shopease.xlsx`
- Auto-detects `requirements_shopease.md`
- Open output Excel → Coverage Gap Analysis sheet
- Show BA Statistics Dashboard
- Point out ❌ NOT COVERED requirements

---

## Scene 4 — GEN Mode (3 minutes)

```bash
dotnet run -- --gen-mode
```

- Auto-detects `requirements_shopease.md` from list
- Enter: 10 tests, 3 passes
- Show pipeline running:
  - Pass 1 generating
  - Critique summary (KEEP/REVISE/DROP counts)
  - Pass 2 refining
  - Auto QA scoring
- Open output Excel:
  - Generated Tests sheet — color-coded QA scores, Pass column
  - Gen Statistics Dashboard — 4 sections

---

## Scene 5 — JSON export (30 seconds)

```bash
dotnet run -- --gen-mode --format json
```

Open the `.json` file — show metadata, summary, testCases array.

---

## Scene 6 — Cache demo (30 seconds)

Run `--gen-mode` again with same requirements and settings.
Show: `⚡ Cache hit — returning cached GEN Mode result.`
Point out: $0.00, instant.

---

## Talking points

- 43 tests passing (31 unit + 12 integration)
- $0.003 to generate 10 test cases with 3-pass refinement
- Cache makes re-runs free
- Built in 150 consecutive daily commits