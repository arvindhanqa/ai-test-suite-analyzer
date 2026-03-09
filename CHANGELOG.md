# Changelog

All notable changes to AI Test Suite Analyzer will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-02-04

### Added
- **Core Analysis Engine**: AI-powered test case quality analysis using OpenAI GPT-4o-mini
- **Excel Processing**: Read test cases from Excel files using EPPlus library
- **Multi-Sheet Output**: Generate comprehensive reports with 4 sheets (analysis, quality issues, statistics dashboard, improvement suggestions)
- **Batch Processing**: Process multiple Excel files in sequence with aggregate statistics
- **Interactive File Selector**: Menu-driven file selection (single file, batch mode, or exit)
- **Caching System**: 30-day content-based cache prevents redundant API calls, significant cost savings
- **Progress Tracking**: Real-time progress bar with completion percentage and remaining tests
- **Color-Coded Console**: Visual feedback (green=success, yellow=warning, red=error, cyan=info)
- **Configuration Management**: Secure API key storage in appsettings.json with validation
- **Cost Optimization**: 84% token reduction through prompt engineering (750→123 tokens/test)
- **Professional Code Quality**: 
  - XML documentation for 22 public methods
  - Zero compiler warnings (C# nullable reference types fully handled)
  - Clean architecture with 9 core classes
  - Comprehensive error handling throughout

### Technical Details
- **Model**: GPT-4o-mini with temperature=0.2 for consistent analysis
- **Cost**: ~$0.000028 per test case analysis
- **Performance**: ~2 seconds per test case, 100% cache hit rate on repeated runs
- **Output**: Professional Excel reports with color coding, text wrapping, borders, auto-sizing

### Development Timeline
- Days 1-7: Foundation (setup, OpenAI integration, Excel I/O, optimization)
- Days 8-9: Professional features (progress tracking, caching, refactoring)
- Days 10-11: Scalability (batch processing, interactive menu, second test suite)
- Days 12-14: Code quality (XML documentation, nullable warning fixes)

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

## [Unreleased]

## [Unreleased]

### Planned
- JSON export option (`--format json`)
- Parallel batch processing with rate limit throttling
- Dependency injection via Microsoft.Extensions.DI
- Interfaces on service classes for testability
- Integration tests with mocked API
- GitHub release v1.0.0 tag

---

**Version Format**: MAJOR.MINOR.PATCH
- **MAJOR**: Incompatible API changes
- **MINOR**: New functionality (backward compatible)
- **PATCH**: Bug fixes (backward compatible)