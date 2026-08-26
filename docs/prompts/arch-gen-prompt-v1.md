# ARCH Mode — Section Test Generation Prompt (v1)
# Validated: August 26, 2026 (v3.0 Day 2)
# Status: LOCKED

---

## Purpose

Generates test cases for 100% coverage of a single requirements section.
Used in ARCH Mode for each SectionTestPlan after the architecture plan is approved.
Called once per section in sequential order.

---

## Key Design Decision

Do NOT pass target test counts to this prompt.
The AI generates as many tests as needed for complete coverage — no quota.
Coverage is the goal, not a number.

---

## System Prompt

You are an expert QA engineer. Generate test cases to achieve 100%
coverage for a specific section of a requirements document.

Given a section name, its sub-topics, and the requirements text,
generate exactly the test cases needed to cover:

All functional requirements (happy paths)
All business rules (boundary conditions)
All validation rules (invalid inputs)
All error handling paths (error messages and system responses)

No gaps. No duplicates. Every requirement path must have at least
one test case.

Output format — one line per test case, pipe-delimited EXACTLY:
TC-{PREFIX}-001|SubTopic|Scenario description|Priority|Step 1. Do X\nStep 2. Do Y|Expected result

Rules:

One test case per line
No header row
No blank lines
No markdown, no commentary, no explanations
Priority must be exactly: High, Medium, or Low
Multiple steps separated by \n within the Steps field
Number test cases sequentially: TC-{PREFIX}-001, TC-{PREFIX}-002, etc.
Each test case must be specific and directly traceable to a requirement
Positive tests: cover happy paths and valid inputs
Negative tests: cover error conditions, boundary values, and invalid inputs
For each validation rule with a minimum and maximum length constraint,
generate one dedicated test case for below-minimum input and one dedicated
test case for above-maximum input. These must be separate test cases with
accurate scenario descriptions.
When a validation rule applies in multiple contexts (e.g., password
complexity applies to both registration and password reset), generate a
negative test case in each context separately.

---

## User Prompt Template

Generate test cases for 100% coverage of this section.

SECTION: {SectionName}
PREFIX: {TestIdPrefix}

SUB-TOPICS TO COVER:
{SubTopicList}

REQUIREMENTS:
{SectionRequirementsMarkdown}


### Template Variables

| Variable | Source | Example |
|---|---|---|
| `{SectionName}` | SectionTestPlan.SectionName | User Authentication |
| `{TestIdPrefix}` | SectionTestPlan.TestIdPrefix | AUTH |
| `{SubTopicList}` | SectionTestPlan.SubTopics (one bullet per line) | - User Registration |
| `{SectionRequirementsMarkdown}` | Extracted section text from requirements doc | ## 1. USER AUTH... |

---

## Output Format

One line per test case, pipe-delimited, 6 fields:

TC-{PREFIX}-NNN|SubTopic|Scenario|Priority|Steps|ExpectedResult


| Field | Notes |
|---|---|
| Test ID | Sequential, prefixed e.g. TC-AUTH-001 |
| SubTopic | Must match one of the sub-topics passed in |
| Scenario | Specific, traceable to a requirement |
| Priority | Exactly: High, Medium, or Low |
| Steps | Multiple steps separated by literal \n |
| Expected Result | No "Expected result:" label prefix |

---

## Parsing Note

Steps field contains literal `\n` (backslash + n), not actual newlines.
C# parser must handle:

```csharp
var stepsFormatted = fields[4].Replace("\\n", Environment.NewLine);
```

---

## Validation Run — ShopEasy User Authentication

**Date:** August 26, 2026
**Model:** GPT-4.1-mini
**Section:** User Authentication (3 sub-topics, 14 requirements)
**Output:** 14 test cases (TC-AUTH-001 through TC-AUTH-014)
**Coverage:** All 14 requirement entries mapped, zero gaps
**Iterations to lock:** 4 runs (3 prompt refinements)

### Key refinements made during validation

1. Boundary testing instruction added to system prompt (run 2)
   — reason: below-max password test was missing
2. Boundary instruction moved from user prompt to system prompt (run 3)
   — reason: universal rule, not per-run instruction
3. Context-specific validation rule added (run 4)
   — reason: VR-AUTH-003 (password complexity) was only tested
     in registration context, not in password reset context

---

## Model

GPT-4.1-mini
(1M token context — handles large requirements documents)

---

## Known Behaviour

Model is non-deterministic. Output structure may vary slightly between runs
while still being correct. During Phase B testing, run the prompt 2-3 times
to verify the parser handles structural variation correctly.