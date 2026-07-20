# v3.0 ARCH Mode Vision — AI as Test Architect

**Document created:** June 28, 2026 (Day 139)
**Document updated:** July 19, 2026 (Day 152)
**Status:** Design locked — implementation starts after v2.1 complete
**Author:** Aravindhan Rajasekaran
**Prerequisite:** All 12 v2.1 issues closed

---

## The Problem with v2.0 GEN Mode

v2.0 GEN Mode generates test cases, but the user has to decide
how many tests to generate. The AI just fills the quota.

The result: arbitrary coverage. Some sub-topics get 3 tests,
some get 1, some get none. No one knows if the document is
actually 100% covered.

**The fundamental flaw:** The user is doing the architect's job.
The AI should be doing it.

---

## The v3.0 Vision — ARCH Mode

The AI reads the entire requirements document, identifies every
section and sub-topic, and recommends exactly how many tests are
needed to reach 100% coverage — at two levels:

**Level 1 — Per-section coverage (100% each)**
Every section of the document is analysed independently. The AI
recommends test counts per sub-topic based on the number of
functional requirements, business rules, validation rules,
and error handling paths.

**Level 2 — Cross-section integration coverage**
Even when every section is 100% covered individually, interactions
between sections create gaps. A user flow of
Register → Browse → Add to Cart → Checkout is not covered by
any single section's tests. ARCH Mode identifies these flows
and generates additional integration tests to close the gap.

The user reviews the plan, adjusts if needed, approves, and
the AI generates everything in one uninterrupted run.

---

## Example — ShopEasy Requirements

```
ARCH MODE — COVERAGE ANALYSIS
ShopEasy E-Commerce Platform
═══════════════════════════════════════════════════════

SECTION COVERAGE (Level 1)
───────────────────────────────────────────────────────
Section                    Sub-topics  Tests  Coverage
User Authentication            3        19     100%
  └─ User Registration                   8
  └─ User Login                          6
  └─ Password Reset                      5
Product Catalog                3        14     100%
  └─ Browse Products                     4
  └─ Search Products                     5
  └─ Filter Products                     5
Shopping Cart                  3        13     100%
  └─ Add to Cart                         5
  └─ Update Cart                         4
  └─ View Cart                           4
Checkout Process               3        16     100%
  └─ Shipping Information                5
  └─ Payment Information                 6
  └─ Order Review                        5
Order Management               3        12     100%
  └─ View Order History                  4
  └─ Track Order Status                  4
  └─ Cancel Order                        4
───────────────────────────────────────────────────────
Section tests total:          74 tests

CROSS-SECTION INTEGRATION (Level 2)
───────────────────────────────────────────────────────
Flow                                              Tests
Register → Browse → Add to Cart → Checkout          2
Login → Resume abandoned cart → Checkout            1
Place order → View history → Cancel → Refund        1
Search → Filter → Add to Cart → Checkout            1
───────────────────────────────────────────────────────
Integration tests needed:                           5

═══════════════════════════════════════════════════════
TOTAL for 100% overall coverage:                   79
Section coverage:    100% (74 tests)
Overall coverage:    100% (74 + 5 integration tests)
═══════════════════════════════════════════════════════

[A] Accept all  [E] Edit counts  [B] Back
```

---

## Design Decisions (locked)

| Decision | Choice | Rationale |
|---|---|---|
| Input | Full requirements document | User uploads one doc, AI does all the thinking |
| Analysis levels | Per-section + cross-section integration | Catches gaps that per-section alone misses |
| Generation order | One uninterrupted sequential run | User approves once upfront, walks away |
| Output | Single Excel file, sections grouped | Consistent with existing QA/BA output pattern |
| Test IDs | AI-generated (section-based prefix) | User can rename later in their test tool |
| User steering | Accept/Edit plan before generation | User is architect, AI is implementer |
| Edit mode | Available in v3.0 from day one | Not deferred to v3.1 |
| Mode name | ARCH Mode | AI as test architect |
| CLI flag | `--arch-mode` | Consistent with `--gen-mode` pattern |

---

## Architecture

### New AI Methods

```csharp
/// <summary>
/// ARCH MODE — Step 1: Analyse full requirements document.
/// Identifies sections, sub-topics, recommended test counts,
/// and cross-section integration flows.
/// Returns a complete ArchitecturePlan for user review.
/// </summary>
Task<ArchitecturePlan> AnalyzeDocumentStructureAsync(
    string requirementsMarkdown)

/// <summary>
/// ARCH MODE — Step 2: Generate test cases for one section.
/// Uses section-specific context including sub-topic breakdown
/// and target counts from the approved ArchitecturePlan.
/// </summary>
Task<(List<GeneratedTestCase> TestCases, int Tokens)>
    GenerateTestCasesForSectionAsync(
        string sectionMarkdown,
        SectionTestPlan plan)

/// <summary>
/// ARCH MODE — Step 3: Generate cross-section integration tests.
/// Uses the full list of already-generated section tests as context
/// to identify and fill coverage gaps between sections.
/// </summary>
Task<(List<GeneratedTestCase> TestCases, int Tokens)>
    GenerateIntegrationTestsAsync(
        string requirementsMarkdown,
        List<IntegrationFlow> flows,
        List<GeneratedTestCase> allSectionTests)
```

### New Data Models

```csharp
/// <summary>
/// Full architecture plan for a requirements document.
/// Produced by AnalyzeDocumentStructureAsync, reviewed by user,
/// then passed to the generation pipeline.
/// </summary>
public class ArchitecturePlan
{
    public List<SectionTestPlan> Sections { get; set; } = new();
    public List<IntegrationFlow> IntegrationFlows { get; set; } = new();
    public int TotalSectionTests { get; set; }
    public int TotalIntegrationTests { get; set; }
    public int TotalTests => TotalSectionTests + TotalIntegrationTests;
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Recommended test plan for one section of the requirements document.
/// </summary>
public class SectionTestPlan
{
    public string SectionName { get; set; } = "";
    public string TestIdPrefix { get; set; } = "";  // AI-generated e.g. "AUTH", "CART"
    public List<SubTopicTestPlan> SubTopics { get; set; } = new();
    public int TotalRecommended { get; set; }
    public string RiskLevel { get; set; } = "Medium";
}

/// <summary>
/// Recommended test count for one sub-topic within a section.
/// </summary>
public class SubTopicTestPlan
{
    public string SubTopicName { get; set; } = "";
    public int RecommendedTests { get; set; }
    public int PositiveTests { get; set; }
    public int NegativeTests { get; set; }
    public string Rationale { get; set; } = "";
}

/// <summary>
/// A cross-section integration flow that requires dedicated tests.
/// </summary>
public class IntegrationFlow
{
    public string FlowName { get; set; } = "";
    public List<string> SectionsInvolved { get; set; } = new();
    public int RecommendedTests { get; set; }
    public string Description { get; set; } = "";
}

/// <summary>
/// Full result of an ARCH Mode run.
/// </summary>
public class ArchModeResult
{
    public ArchitecturePlan Plan { get; set; } = new();
    public List<GeneratedTestCase> AllTestCases { get; set; } = new();
    public int TotalPasses { get; set; }
    public int TotalTokens { get; set; }
    public string RequirementsSource { get; set; } = "provided";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
```

### Orchestrator Flow

```
ArchModeOrchestrator.RunAsync(requirementsMarkdown)
        ↓
1. AnalyzeDocumentStructureAsync()
   → Returns ArchitecturePlan
        ↓
2. Display plan to user (sections + sub-topics + integration flows)
   User: [A] Accept / [E] Edit / [B] Back
        ↓
3. For each SectionTestPlan (sequential):
   a. GenerateTestCasesForSectionAsync()
   b. CritiqueTestCasesAsync()
   c. RefineTestCasesAsync() (up to MaxPasses)
   d. Accumulate into AllTestCases
        ↓
4. GenerateIntegrationTestsAsync()
   (uses all section tests as context)
   → Critique → Refine
   → Accumulate into AllTestCases
        ↓
5. Auto QA scoring on ALL test cases
        ↓
6. Write Excel (sections grouped + integration section)
7. Write JSON
8. Cache result
```

---

## User Steering — Edit Mode

When user presses `[E]`:

```
Edit test counts (Enter to confirm, 0 to skip a sub-topic):

USER AUTHENTICATION
  User Registration        [8]   (5 positive, 3 negative)
  User Login               [6]   (3 positive, 3 negative)
  Password Reset           [5]   (3 positive, 2 negative)

PRODUCT CATALOG
  Browse Products          [4]   (2 positive, 2 negative)
  Search Products          [5]   (2 positive, 3 negative)
  Filter Products          [5]   (2 positive, 3 negative)

CROSS-SECTION INTEGRATION
  Total integration tests  [5]

Press Enter when done.
```

Validation: if any count is negative, show error. If 0, skip that sub-topic.

---

## Excel Output Structure

```
Sheet 1: All Generated Tests
  [USER AUTHENTICATION]     ← section header (dark green, merged)
  TC-AUTH-001  ...
  TC-AUTH-002  ...

  [PRODUCT CATALOG]
  TC-CAT-001   ...

  [SHOPPING CART]
  TC-CART-001  ...

  [CHECKOUT PROCESS]
  TC-CHECK-001 ...

  [ORDER MANAGEMENT]
  TC-ORDER-001 ...

  [INTEGRATION TESTS]       ← dark purple header
  TC-INT-001   ...

Sheet 2: Coverage Summary Dashboard
  - Per-section coverage % (all should be 100%)
  - Integration coverage
  - Overall coverage %
  - QA Score breakdown per section
  - Total tokens, cost, elapsed time

Sheet 3: ARCH Statistics
  - Plan vs actual (recommended vs generated per section)
  - Pass statistics per section
  - Cost breakdown
```

---

## Prompts to Design and Validate (Playground first)

### Prompt 1 — Document Structure Analysis

System:
```
You are an expert test architect. Analyse requirements documents and
create comprehensive test coverage plans.

Given a requirements document, identify all sections and sub-topics,
then recommend exactly how many tests each sub-topic needs for 100%
coverage. Also identify cross-section integration flows.

Output format — pipe-delimited, one line per sub-topic:
SECTION|SubTopic|TotalTests|Positive|Negative|RiskLevel|Rationale

Then a blank line followed by integration flows:
INTEGRATION|FlowName|SectionsInvolved|TestCount|Description

Rules:
- One line per sub-topic
- No blank lines within sections
- RiskLevel: High, Medium, or Low
- TotalTests = Positive + Negative
- Integration flows: 3-7 flows covering the most important
  cross-section interactions
- No markdown, no commentary
```

### Prompt 2 — Section Generation

Same as v2.0 generate prompt but with sub-topic context added:
```
Generate test cases for the {sectionName} section.

Sub-topic breakdown:
{subTopicList}

Generate exactly:
{perSubTopicCounts}

Requirements for this section:
{sectionRequirements}
```

### Prompt 3 — Integration Test Generation

```
Generate cross-section integration tests.

Already generated section tests (for context — do not duplicate):
{allSectionTestsSummary}

Integration flows to cover:
{integrationFlows}

Full requirements document:
{requirementsMarkdown}

Generate tests that cover user journeys spanning multiple sections.
Each test must reference at least 2 different sections.
```

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|---|---|---|
| Document structure prompt inconsistent | Medium | Validate with 3+ docs in Playground before coding |
| Section extraction misses sub-topics | Medium | Show extracted structure to user for approval |
| Integration flows are generic/obvious | Low | Prompt explicitly requires flows spanning 2+ sections |
| Per-section generation loses context | Low | Pass full section requirements not just sub-topic names |
| Token cost high for large documents | Medium | Cache ArchitecturePlan separately — re-use on retry |
| Edit mode console UI complex | Medium | Start with simple number input, not inline editing |

---

## Implementation Plan (draft)

### Phase A — Prompt Design (Days 1-4)
- Day 1: Design and validate document structure analysis prompt
  (ShopEasy + TaskFlow + one other doc)
- Day 2: Design and validate section generation prompt
- Day 3: Design and validate integration test generation prompt
- Day 4: Add all new prompts to PromptConfig.json + PromptConfig.cs

### Phase B — Data Models + AI Methods (Days 5-10)
- Day 5-6: New data models
  (ArchitecturePlan, SectionTestPlan, SubTopicTestPlan,
   IntegrationFlow, ArchModeResult)
- Day 7-8: AnalyzeDocumentStructureAsync + parser + unit tests
- Day 9: GenerateTestCasesForSectionAsync
- Day 10: GenerateIntegrationTestsAsync

### Phase C — Orchestrator (Days 11-16)
- Day 11-12: ArchModeOrchestrator skeleton + generation loop
- Day 13: Integration test generation step
- Day 14: Auto QA scoring
- Day 15: Caching (ArchitecturePlan cached separately)
- Day 16: Manual end-to-end test

### Phase D — UI (Days 17-22)
- Day 17-18: FileSelector ARCH Mode screen
  (requirements file selection — same auto-detect pattern)
- Day 19-20: Coverage plan display table
- Day 21-22: Edit mode (accept/edit counts per sub-topic)

### Phase E — Output (Days 23-28)
- Day 23-24: Excel output with section grouping
- Day 25: Coverage Summary Dashboard sheet
- Day 26: JSON export for ARCH Mode
- Day 27: `--arch-mode` CLI flag
- Day 28: Wire into main menu as option [4]

### Phase F — Tests + Ship (Days 29-35)
- Day 29-31: Integration tests
  (full pipeline, edit mode, cache hit, section grouping)
- Day 32-33: Code review + docs
- Day 34: v3.0.0 tag + GitHub Release
- Day 35: Video + LinkedIn

**Total: ~35 days for v3.0.0**

---

## Menu — v3.0.0

```
AI TEST SUITE ANALYZER

What would you like to do?

  [1] Analyze a single Excel file — QA Mode
  [2] Analyze a single Excel file — BA Mode
  [3] Generate test cases — GEN Mode
  [4] Generate full test suite — ARCH Mode  🆕
  [5] Batch analyze all Excel files in a folder
  [6] Exit
```

---

## Decision Log

| Date | Decision | Rationale |
|---|---|---|
| June 28, 2026 | Start after v2.1 complete | v2.1 fixes first, then v3.0 |
| July 19, 2026 | Full document input (not per-feature) | User uploads one doc, AI does all thinking |
| July 19, 2026 | Two-level coverage model | Per-section alone misses cross-section gaps |
| July 19, 2026 | One uninterrupted sequential run | User approves once, walks away |
| July 19, 2026 | Single Excel file, sections grouped | Consistent with existing output pattern |
| July 19, 2026 | AI-generated test IDs | User can rename later in their test tool |
| July 19, 2026 | Edit mode in v3.0 (not deferred) | Worth building right from day one |
| July 19, 2026 | Mode name: ARCH Mode | AI as test architect |
| July 19, 2026 | CLI flag: --arch-mode | Consistent with --gen-mode |

---

## Open Questions (resolved)

~~1. Per-feature or full document?~~ → Full document
~~2. Edit mode in v3.0 or v3.1?~~ → v3.0 from day one
~~3. Sequential or parallel?~~ → Sequential
~~4. One file or per-section files?~~ → One file, sections grouped
~~5. Test ID format?~~ → AI-generated section-based prefix

---

*This document is the source of truth for v3.0 ARCH Mode design.*
*Do not start implementation until all 12 v2.1 issues are closed.*
