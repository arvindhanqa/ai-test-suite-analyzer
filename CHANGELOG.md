# Changelog

All notable changes to AI Test Suite Analyzer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.0] - 2026-06-19

### Added
- **GEN Mode**: AI-powered test case generation from requirements documents
- **Generate → Critique → Refine loop**: Up to 3 refinement passes with early exit when all critiques are KEEP
- **Auto QA Mode scoring**: Generated test cases automatically scored after generation
- **GEN Mode Excel output**: Generated Tests sheet with color-coded QA scores and pass number tracking
- **Gen Statistics Dashboard**: Generation summary, QA score breakdown, cost and performance metrics
- **JSON export for GEN Mode**: Full export of generated test cases with metadata and summary
- **GEN Mode caching**: SHA256 hash of requirements + targetCount + maxPasses — repeat runs are instant and free
- **`--gen-mode` CLI flag**: Launch GEN Mode directly without navigating the menu
- **FileSelector GEN Mode flow**: Auto-detects requirements files from known locations, same UX pattern as QA/BA modes
- **17 new tests**: 7 unit tests (parsing, interface contract) + 5 integration tests (full pipeline, critique loop, cache hit, missing requirements guard) — total: 43 tests passing

### Changed
- Main menu restructured: QA Mode and BA Mode are now separate menu options [1] and [2], GEN Mode is [3], Batch is [4]
- Version bumped to 2.0.0-beta during development, finalised to 2.0.0 on release

### Removed
- Markdown auto-generation from app name hint — replaced with clear error handling for missing requirements files (better UX than silent fallback to low-quality generated requirements)

### Technical
- `GenModeOrchestrator` — orchestrates the full Generate → Critique → Refine → QA Score pipeline
- `GenModeExcelWriter` — creates timestamped output files with two GEN Mode sheets
- `CritiqueResult` model — KEEP / REVISE / DROP actions with actionable reasons
- `GeneratedTestCase` model — includes PassNumber and QAScore fields
- `GenModeResult` model — full pipeline result with token tracking and requirements source
- GEN Mode uses `gpt-4.1-mini` (1M token context, better instruction following than gpt-4o-mini)
- GEN Mode constants added: `GEN_DEFAULT_TEST_COUNT`, `GEN_MAX_PASSES`, `GEN_CACHE_PREFIX`, `CRITIQUE_KEEP`, `CRITIQUE_REVISE`, `CRITIQUE_DROP`
- Separate `gen_mode_cache.json` cache file alongside existing `test_analysis_cache.json`

## [1.0.0] - 2026-04-19

### Added
- **JSON Export**: `--format json` flag exports full analysis results with metadata,
  summary statistics, and per-test results to `.json` file
- **Interface-Driven Architecture**: Five service interfaces extracted
  (`IAIAnalyzer`, `IExcelReader`, `IExcelWriter`, `IRequirementExtractor`, `ITestCaseCache`)
- **Dependency Injection**: Full DI container via `Microsoft.Extensions.DependencyInjection`
- **Integration Test Suite**: End-to-end pipeline tests covering QA mode, BA mode,
  cache hit behaviour, and JSON export (5 integration tests)
- **Mock-Based Unit Tests**: Moq-based tests for `IAIAnalyzer` and `IExcelReader`
- **Async Cache Save**: `SaveCacheAsync()` replaces synchronous cache writes throughout

### Changed
- Total test count: 26 passing (21 unit + 5 integration)
- All async methods consistent throughout codebase (TD-2)
- Error messages across all major classes now include context
  (test ID, retry count, file name) for faster debugging (TD-4)

### Fixed
- Cache load errors now logged to console instead of silently swallowed
- CI pipeline now runs all 26 tests on every push and PR
- `System.Security.Cryptography.Xml` vulnerability patched to 10.0.6
  in both main and integration test projects

### Refactored
- Codebase reorganised into subfolders:
  `Config/`, `Infrastructure/`, `Models/`, `Services/`, `UI/`
- `ConfigurationValidator` and `JsonExporter` moved to `Services/`
- All interfaces co-located with their implementations
- Comment numbering fixed in `ValidateAllAsync`
- "Week 1" development artefact removed from console header

## [1.2.0] - 2026-03-09

### Added
- Checkpoint/resume capability (`--resume` flag) for interrupted batch runs
- Dry-run preview mode showing estimated cost before processing
- Graceful shutdown on Ctrl+C with cache save
- Batch progress indicators showing "X of Y files" during processing
- Centralized constants in `Constants.cs` — eliminated magic numbers/strings
- Centralized cost-per-token configuration
- CI/CD pipeline via GitHub Actions with build status badge
- Unit test suite (xUnit + FluentAssertions, 13 passing tests)

### Fixed
- BA Mode blank requirements bug (silent critical failure)
- Path normalization bug causing duplicate cache entries
- Coverage data lost on cache reads (root cause fix)
- Reduced file I/O from ~110 operations to ~4 per run (BUG-2, BUG-3)

### Changed
- Async suffix applied consistently to all async methods (C# convention)
- Stale dev comments removed throughout codebase

## [1.1.0] - 2026-02-16

### Added
- **BA Mode**: Requirement coverage analysis and gap reporting
- Requirement extraction and caching (`RequirementExtractor`, `RequirementCache`)
- Coverage Gap Analysis sheet with color-coded requirement status
- BA Mode Statistics Dashboard sheet
- Single-pass AI design — quality analysis + coverage in one API call (major cost reduction)
- `RetryHelper` for resilient API calls with exponential backoff
- Second test dataset (TaskFlow) for broader testing coverage
- XML documentation expanded to all public APIs
- README with screenshots

### Changed
- 93%+ API call reduction via intelligent caching in BA Mode

## [1.0.0-foundation] - 2026-02-04

### Added
- **Core Analysis Engine**: AI-powered test case quality analysis using OpenAI GPT-4o-mini
- **Excel Processing**: Read test cases from Excel files using EPPlus library
- **Multi-Sheet Output**: Generate reports with 4 sheets
- **Batch Processing**: Process multiple Excel files in sequence
- **Interactive File Selector**: Menu-driven file selection
- **Caching System**: 30-day content-based cache prevents redundant API calls
- **Progress Tracking**: Real-time progress bar
- **Color-Coded Console**: Visual feedback throughout
- **Configuration Management**: Secure API key storage with validation
- **Cost Optimization**: 84% token reduction through prompt engineering

### Technical Details
- Model: GPT-4o-mini, temperature=0.2
- Cost: ~$0.000028 per test case
- Performance: ~2 seconds per test case, 100% cache hit rate on repeated runs

---

**Version Format**: MAJOR.MINOR.PATCH
- **MAJOR**: Incompatible API changes
- **MINOR**: New functionality (backward compatible)
- **PATCH**: Bug fixes (backward compatible)