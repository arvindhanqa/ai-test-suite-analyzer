# GEN Mode — Requirements File Handling Design

**Decision date:** April 25, 2026  
**Decision:** No markdown auto-generation. Missing requirements = clear error. 

---

## Auto-Detection Logic

When user selects GEN Mode, the tool attempts to auto-detect
the requirements file using the same convention as BA Mode:

```
Test file:         data/test_cases_shopease.xlsx
Requirements file: data/requirements_shopease.md
                   (replace "test_cases_" with "requirements_", change to .md)
```

---

## Flow

```
User selects GEN Mode
        ↓
Ask: "Enter requirements file path (or press Enter to auto-detect):"
        ↓
    ┌───────────────────────────────┐
    │ User pressed Enter?           │
    └───────────────────────────────┘
         │ Yes                │ No
         ↓                    ↓
  Auto-detect path      Use provided path
         ↓                    ↓
    ┌─────────────────────────────┐
    │ File exists?                │
    └─────────────────────────────┘
         │ Yes                │ No
         ↓                    ↓
  Proceed to GEN         Show error
  Mode pipeline          and exit
```

---

## Error Messages

### Auto-detection failed (no file provided, auto-detect found nothing):
```
❌ Requirements file not found.
   Expected: data/requirements_shopease.md
   (auto-detected from test file name: test_cases_shopease.xlsx)

   GEN Mode requires a requirements markdown file.
   Please provide one using the prompt or create:
   data/requirements_shopease.md
```

### User provided path but file does not exist:
```
❌ Requirements file not found: path/you/entered.md
   Please check the path and try again.
```

### File exists but is empty:
```
❌ Requirements file is empty: requirements_shopease.md
   GEN Mode requires a requirements document with content.
```

### File exists but is not a .md or .txt file:
```
❌ Unsupported file type: requirements.xlsx
   GEN Mode accepts .md or .txt requirements files only.
```

---

## Supported File Types
- `.md` (markdown) — primary format, same as BA Mode
- `.txt` (plain text) — accepted, read as-is

---

## What Was Deliberately NOT Built
- Auto-generation of requirements from app name hint
- Fallback to generic requirements on missing file

Rationale: AI-generated requirements from a vague hint produce
low-quality test cases. A clear error is more useful than
silently generating bad output.