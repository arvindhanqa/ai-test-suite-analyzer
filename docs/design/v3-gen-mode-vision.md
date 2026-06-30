# v3.0 GEN Mode Vision — AI as Test Architect

**Document created:** June 28, 2026 (Day 139)  
**Status:** Design only — not yet scheduled for implementation  
**Author:** Aravindhan Rajasekaran  
**Prerequisite:** v2.0.0 shipped and tagged

---

## The Problem with v2.0 GEN Mode

v2.0 GEN Mode works, but it puts the wrong person in charge of thinking.

The user has to decide:
- How many tests to generate (default: 10)
- How many refinement passes (default: 3)

The AI just fills the quota. If you ask for 10 tests from a 500-line requirements
document covering 6 feature areas, the AI picks 10 arbitrarily. Some features
get 3 tests, some get 1, some get none. Coverage is random, not reasoned.

**The fundamental flaw:** The user is doing the test architect's job.
The AI should be doing it.

---

## The v3.0 Vision

The AI reads the requirements document, understands its structure, identifies
the distinct feature areas, and recommends how many tests each feature needs —
with a positive/negative split — based on its complexity and risk profile.

The user steers at a high level (accept, adjust, or override per feature).
The AI executes with intent.

### What it looks like in practice

```
📋 Requirements loaded: requirements_shopease.md (9,058 chars)

🤖 Analyzing requirements structure...

╔══════════════════════════════════════════════════════════════════╗
║  AI TEST PLAN RECOMMENDATION                                     ║
╠══════════════════════════════════════════════════════════════════╣
║  Feature                  Total   Positive  Negative  Rationale ║
╠══════════════════════════════════════════════════════════════════╣
║  User Registration          8        5         3      High risk  ║
║  User Login                 6        3         3      Medium     ║
║  Password Reset             4        2         2      Medium     ║
║  Product Catalog            5        3         2      Low risk   ║
║  Shopping Cart              7        4         3      High risk  ║
║  Checkout Process           8        4         4      High risk  ║
╠══════════════════════════════════════════════════════════════════╣
║  TOTAL                     38       21        17                 ║
╚══════════════════════════════════════════════════════════════════╝

  [A] Accept all recommendations
  [E] Edit counts per feature
  [B] Back to menu
```

User presses `A` → generation proceeds with those exact numbers, per feature.
User presses `E` → edits individual rows before proceeding.

---

## Architecture

### New AI Method

```csharp
Task<RequirementsAnalysis> AnalyzeRequirementsStructureAsync(
    string requirementsMarkdown)
```

This is a new method on `IAIAnalyzer`. It reads the full requirements document
and returns a structured analysis of features and recommended test counts.

### New Data Models

```csharp
/// <summary>
/// Full analysis of a requirements document — feature breakdown
/// with recommended test counts and positive/negative splits.
/// </summary>
public class RequirementsAnalysis
{
    public List<FeatureTestPlan> Features { get; set; } = new();
    public int TotalRecommendedTests { get; set; }
    public string RequirementsSource { get; set; } = "";
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Recommended test plan for a single feature area.
/// </summary>
public class FeatureTestPlan
{
    /// <summary>Feature name as identified in the requirements document.</summary>
    public string FeatureName { get; set; } = "";

    /// <summary>Brief description of what this feature covers.</summary>
    public string FeatureDescription { get; set; } = "";

    /// <summary>Total recommended tests for this feature.</summary>
    public int RecommendedTotal { get; set; }

    /// <summary>Recommended positive (happy path) test count.</summary>
    public int PositiveTests { get; set; }

    /// <summary>Recommended negative (error/edge case) test count.</summary>
    public int NegativeTests { get; set; }

    /// <summary>
    /// AI rationale for the recommendation (e.g. "High risk — payment processing,
    /// multiple validation rules, error handling paths").
    /// </summary>
    public string Rationale { get; set; } = "";

    /// <summary>Risk level: High, Medium, or Low.</summary>
    public string RiskLevel { get; set; } = "Medium";
}
```

### Updated Orchestrator Flow

```
AnalyzeRequirementsStructureAsync(requirements)
        ↓
Display recommendation table to user
        ↓
User: [A] Accept / [E] Edit / [B] Back
        ↓
For each FeatureTestPlan (in parallel or sequential):
    GenerateTestCasesAsync(
        featureRequirements,
        plan.RecommendedTotal,
        plan.PositiveTests,
        plan.NegativeTests)
    → CritiqueTestCasesAsync()
    → RefineTestCasesAsync() (up to MaxPasses)
    → Accumulate results
        ↓
Auto QA Mode scoring on all generated tests
        ↓
Excel output (tests grouped by feature)
JSON output
```

### Key difference from v2.0

v2.0: One generation call for the whole document.
v3.0: One generation call per feature, with intentional positive/negative counts.

Per-feature generation produces better quality test cases because:
- Smaller, focused prompts → less hallucination
- AI knows exactly how many positive vs negative tests to write
- Each feature's requirements are passed as context, not the whole document
- Critique and refine are also per-feature → faster convergence

---

## Prompt Design (to be validated in Playground)

### Pre-analysis System Message

```
You are an expert test architect. Analyze requirements documents and recommend
test coverage plans.

Given a requirements document, identify distinct feature areas and recommend
how many test cases each needs — with a positive/negative split — based on
complexity, risk level, and number of validation rules.

Output format — one line per feature, pipe-delimited, EXACTLY:
FeatureName|Description|Total|Positive|Negative|RiskLevel|Rationale

Rules:
- One line per feature
- No blank lines, no markdown, no commentary
- RiskLevel must be exactly: High, Medium, or Low
- Total must equal Positive + Negative
- Rationale must be under 80 characters
- Number of features: 3-10 (consolidate tiny features, split large ones)
```

### Pre-analysis User Template

```
Analyze this requirements document and recommend a test coverage plan.

REQUIREMENTS:
{requirementsMarkdown}

Identify the distinct feature areas and recommend test counts with
positive/negative splits based on complexity and risk.
```

### Updated Generate System Message

The generate prompt needs two new placeholders:

```
Generate exactly {positiveCount} positive (happy path) test cases and
{negativeCount} negative (error/edge case) test cases for this specific feature.

Feature: {featureName}

[existing pipe-delimited format rules...]
```

---

## User Steering — Edit Mode

When user presses `E`:

```
  Edit test counts per feature (Tab to move, Enter to confirm):

  Feature                  Total   Positive  Negative
  User Registration        [8]     [5]       [3]
  User Login               [6]     [3]       [3]
  Password Reset           [4]     [2]       [2]
  Product Catalog          [5]     [3]       [2]
  Shopping Cart            [7]     [4]       [3]
  Checkout Process         [8]     [4]       [4]

  Note: Total must equal Positive + Negative.
  Press Enter when done.
```

Validation: if Total ≠ Positive + Negative, show error and re-prompt.
If user sets a feature to 0, skip it entirely.

---

## Excel Output Changes

### Generated Tests sheet — add Feature grouping

Tests are grouped by feature with a feature header row:

```
[USER REGISTRATION]           ← feature header row (dark green, merged)
TC-GEN-001  User Registration  Valid registration...  High  Pass 1  GOOD
TC-GEN-002  User Registration  Duplicate email...     High  Pass 1  GOOD
...

[USER LOGIN]
TC-GEN-009  User Login  Valid credentials...  High  Pass 1  GOOD
...
```

### Gen Statistics Dashboard — add per-feature breakdown

New section: **FEATURE COVERAGE SUMMARY**

| Feature | Tests | GOOD | Issues | Errors | Coverage % |
|---|---|---|---|---|---|
| User Registration | 8 | 7 | 1 | 0 | 87.5% |
| User Login | 6 | 6 | 0 | 0 | 100% |
| ... | | | | | |

---

## What's NOT changing from v2.0

- Generate → Critique → Refine loop — same mechanics, applied per feature
- QA Mode auto-scoring — same, applied to all tests after all features done
- Caching — same hash strategy, cache per requirements + plan
- JSON export — same structure, add feature field to each test case
- CLI flag — `--gen-mode` still works, shows the new recommendation table

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|---|---|---|
| Pre-analysis prompt produces inconsistent feature counts | Medium | Validate in Playground with 3+ different requirement docs before writing code |
| Pipe-delimited parsing fails on feature names with pipes | Low | Strip pipes from feature names before parsing |
| Per-feature generation produces duplicate test IDs | Medium | Renumber all IDs after all features complete: TC-GEN-001 through TC-GEN-N |
| User edit mode is complex to implement in console | Medium | Start with simple accept/reject, add edit mode in v3.1 |
| Token cost increases significantly | Low | Per-feature prompts are shorter than whole-doc prompts, cost likely similar |

---

## Implementation Plan (draft — not yet scheduled)

**Phase A — Prompt Design (Days 1-3)**
- Day 1: Design and validate pre-analysis prompt in Playground (3+ docs)
- Day 2: Update generate prompt to accept positiveCount/negativeCount
- Day 3: Document all prompts, add to PromptConfig.json

**Phase B — Core Engine (Days 4-12)**
- Day 4-5: New data models (RequirementsAnalysis, FeatureTestPlan)
- Day 6-7: AnalyzeRequirementsStructureAsync + parser + unit tests
- Day 8-9: Update GenerateTestCasesAsync signature for per-feature use
- Day 10-12: Update GenModeOrchestrator for per-feature loop

**Phase C — UI + Output (Days 13-20)**
- Day 13-14: FileSelector recommendation table display
- Day 15-16: User steering (Accept / Edit / Back)
- Day 17-18: Excel output with feature grouping
- Day 19-20: Per-feature Stats Dashboard section

**Phase D — Tests + Ship (Days 21-28)**
- Day 21-23: Integration tests for new flow
- Day 24-25: Code review + docs
- Day 26-27: v3.0.0 tag, video, LinkedIn
- Day 28: Done

**Total: ~28 days for v3.0.0**

---

## Decision Log

| Date | Decision | Rationale |
|---|---|---|
| June 28, 2026 | Design v3.0 now, build after v2.0.0 ships | v2.0 must finish first — pattern breaking is the goal |
| June 28, 2026 | Per-feature generation (Option A) | Most reliable feature detection from markdown headers |
| June 28, 2026 | Accept/Edit/Back steering UI | Balances simplicity with user control |
| June 28, 2026 | Start with Accept only, add Edit in v3.1 | Reduces Phase C risk — ship working v3.0, iterate |

---

## Open Questions (to answer before building)

1. Should per-feature generation run sequentially or in parallel?
   - Sequential is safer (rate limits, easier debugging)
   - Parallel would be 3-6x faster for large documents
   - Decision: start sequential, add parallel in v3.1

2. What if the AI identifies 15 features in a large document?
   - Cap at 10 features max
   - Consolidate small features (< 2 requirements) into "Other"

3. Should the pre-analysis be cached?
   - Yes — hash of requirementsMarkdown, cache for 7 days
   - User may run GEN Mode multiple times with same requirements

4. What's the test ID format when generating per-feature?
   - Option A: TC-GEN-001 through TC-GEN-N (global sequential)
   - Option B: TC-AUTH-001, TC-CART-001 (feature-prefixed)
   - Decision: Option A for v3.0 (consistent with v2.0), Option B in v3.1

---

*This document is the source of truth for v3.0 GEN Mode design.*
*Update the Decision Log when design decisions are made.*
*Do not start implementation until v2.0.0 is tagged and shipped.*
