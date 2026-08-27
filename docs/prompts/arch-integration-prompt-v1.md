# ARCH Mode — Integration Test Generation Prompt (v1)
# Validated: August 26, 2026 (v3.0 Day 3)
# Status: LOCKED

---

## Purpose

Generates cross-section integration test cases for ARCH Mode.
Called once after all section-level tests have been generated.
Receives the full requirements document, the integration flows from the
architecture plan (Prompt 1 output), and all section-level tests already
generated (Prompt 2 output) to avoid duplication.

---

## Key Design Decisions

1. **Coverage drives generation, not quota.**
   Do NOT pass target test counts. The AI generates as many tests as needed
   to cover each flow — no quota.

2. **Deduplication is the caller's responsibility to enable.**
   All existing section tests must be passed in the user prompt so the AI
   can avoid duplicating scenarios already covered at the section level.

3. **Every flow must have at least one failure path.**
   The system prompt enforces a boundary failure test per flow. Without this
   rule the AI defaults to happy paths only.

4. **SubTopic field = FlowName.**
   Integration tests use the FlowName as the SubTopic field so they group
   correctly in the Excel output under the INTEGRATION TESTS section.

---

## System Prompt

```
You are an expert QA engineer. Generate integration test cases that verify
cross-section flows across multiple parts of a requirements document.

Integration tests must cover scenarios where two or more system sections
interact end-to-end. They must NOT duplicate any scenario already covered
in the section-level tests provided.

For each integration flow provided, generate test cases that:
- Verify the complete end-to-end user journey across the named sections
- Test data handoffs between sections (e.g. cart state passing to checkout)
- Cover failure and recovery scenarios at section boundaries
- Verify system state is correctly maintained across section transitions

Output format — one line per test case, pipe-delimited EXACTLY:
TC-INT-001|FlowName|Scenario description|Priority|Step 1. Do X\nStep 2. Do Y|Expected result

Rules:
- One test case per line
- No header row
- No blank lines
- No markdown, no commentary, no explanations
- Priority must be exactly: High, Medium, or Low
- Multiple steps separated by \n within the Steps field
- Number test cases sequentially: TC-INT-001, TC-INT-002, etc.
- The SubTopic field (second field) must be the FlowName from the integration flow
- Each test case must actively exercise logic from at least two distinct sections
- Do NOT generate a test case if an equivalent scenario is already covered
  in the section-level tests provided
- Each test case must be specific and traceable to the integration flow it covers
- For each integration flow, generate at least one test case where a failure
  or error occurs at the boundary between two sections (e.g. payment fails
  after cart is built, cancellation window expires after order is placed)
```

---

## User Prompt Template

```
Generate integration test cases for the following flows.

INTEGRATION FLOWS:
{IntegrationFlowList}

EXISTING SECTION TESTS (do not duplicate these scenarios):
{AllSectionTestSummaries}

FULL REQUIREMENTS:
{RequirementsMarkdown}
```

### Template Variables

| Variable | Source | Format |
|---|---|---|
| `{IntegrationFlowList}` | ArchitecturePlan.IntegrationFlows | One flow per line, pipe-delimited from Prompt 1 output |
| `{AllSectionTestSummaries}` | All GeneratedTestCase results from Prompt 2 | Grouped by section, ID + scenario title per line |
| `{RequirementsMarkdown}` | Full requirements document text | Raw markdown |

### IntegrationFlowList Format

Each line is the raw pipe-delimited line from Prompt 1 output:

```
INTEGRATION|FlowName|SectionsInvolved|TestCount|Description
```

Example:
```
INTEGRATION|End-to-End Purchase|User Authentication,Product Catalog,Shopping Cart,Checkout|5|Complete user journey from registration through purchase
INTEGRATION|Session and Cart Persistence|User Authentication,Shopping Cart|3|Cart state preserved across login and session timeout
```

### AllSectionTestSummaries Format

Grouped by section header, one test per line with ID and scenario title.
Do NOT pass full pipe-delimited test case lines — scenario titles are
sufficient for the deduplication check and keep the prompt concise.

```
[Section: User Authentication]
TC-AUTH-001 — Register new user with valid email, confirm to activate account
TC-AUTH-002 — Reject registration when email already exists
...

[Section: Product Catalog]
TC-CAT-001 — Browse all active in-stock products in default grid view
...
```

---

## Output Format

One line per test case, pipe-delimited, 6 fields:

```
TC-INT-NNN|FlowName|Scenario|Priority|Steps|ExpectedResult
```

| Field | Notes |
|---|---|
| Test ID | Sequential, always TC-INT-NNN prefix |
| FlowName | Must exactly match the FlowName from the integration flow input |
| Scenario | Specific, traceable to the flow and sections involved |
| Priority | Exactly: High, Medium, or Low |
| Steps | Multiple steps separated by literal \n |
| Expected Result | No "Expected result:" label prefix |

---

## Parsing Note

Steps field contains literal `\n` (backslash + n), not actual newlines.
Same behaviour as Prompt 2. C# parser must handle:

```csharp
var stepsFormatted = fields[4].Replace("\\n", Environment.NewLine);
```

Test ID prefix is always `TC-INT` regardless of section.
Parse the FlowName field (index 1) to group integration tests in the
Excel output under the correct flow heading.

---

## Validation Run — ShopEasy Full Document

**Date:** August 26, 2026
**Model:** GPT-4.1-mini
**Flows tested:** 5 integration flows
**Output:** 10 test cases (TC-INT-001 through TC-INT-010)
**Coverage:** All 5 flows covered with at least one positive and one
             failure/boundary path each
**Duplication:** Zero duplicates against 24 provided section tests
**Iterations to lock:** 2 runs (1 prompt refinement)

### Integration flows used in validation

```
INTEGRATION|End-to-End Purchase|User Authentication,Product Catalog,Shopping Cart,Checkout|5|Complete user journey from new registration through first purchase and order confirmation
INTEGRATION|Session and Cart Persistence|User Authentication,Shopping Cart|3|Cart state is preserved when user logs in; session timeout behaviour with items in cart
INTEGRATION|Stock Depletion at Checkout|Product Catalog,Shopping Cart,Checkout|2|Item goes out of stock between add-to-cart and checkout completion
INTEGRATION|Order Cancellation and Refund|User Authentication,Checkout,Order Management|3|User places order then cancels within window, verifying inventory restore and refund initiation
INTEGRATION|Search to Cart|Product Catalog,Shopping Cart|2|User searches for a product and adds result directly to cart
```

### Validated output (10 test cases)

```
TC-INT-001|End-to-End Purchase|New user registers, verifies email, and completes first purchase with confirmation
TC-INT-002|End-to-End Purchase|Payment gateway timeout occurs after cart is built, then user retries and completes order
TC-INT-003|Session and Cart Persistence|Guest cart is preserved after login and available for immediate checkout
TC-INT-004|Session and Cart Persistence|Session expires with items in cart, user re-authenticates and resumes checkout
TC-INT-005|Stock Depletion at Checkout|Item goes out of stock after being added to cart and checkout blocks the order
TC-INT-006|Stock Depletion at Checkout|Partial stock depletion adjusts the cart and user completes checkout with replacement item
TC-INT-007|Order Cancellation and Refund|User places an order and cancels it within the allowed window
TC-INT-008|Order Cancellation and Refund|Cancellation attempt is rejected after the cancellation window expires
TC-INT-009|Search to Cart|User searches for a product and adds the chosen search result directly to cart
TC-INT-010|Search to Cart|No-result search is corrected and user then adds newly found item to cart
```

### Key refinement made during validation

**Run 1:** AI generated 8 tests, all happy paths and structural boundary
cases. Missing failure paths at section boundaries for End-to-End Purchase
and Order Cancellation flows.

**Fix applied:** Added to system prompt rules:
> "For each integration flow, generate at least one test case where a failure
> or error occurs at the boundary between two sections."

**Run 2:** 10 tests generated. All flows have at least one failure path.
Prompt locked.

---

## Model

GPT-4.1-mini
(1M token context — handles full requirements + all section tests in one call)

---

## Known Behaviour

Model is non-deterministic. Test count may vary slightly between runs
while coverage remains complete. During Phase B testing, verify the parser
handles variation in test count correctly — do not hard-code expected counts.

---

## Relationship to Other Prompts

| Prompt | File | Depends On |
|---|---|---|
| Prompt 1 — Document Structure Analysis | arch-structure-prompt-v1.md | Nothing (first call) |
| Prompt 2 — Section Test Generation | arch-gen-prompt-v1.md | Prompt 1 output (SectionTestPlan) |
| Prompt 3 — Integration Test Generation | arch-integration-prompt-v1.md | Prompt 1 output (IntegrationFlows) + Prompt 2 output (all section tests) |