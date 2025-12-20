# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-12-20

### Added

- **Core Synchronization Engine**
  - One-way directory synchronization (source → replica)
  - SHA-256 file hashing for accurate change detection
  - Tiered comparison (size → timestamp → hash) for performance
  - Atomic file operations with temp file + move pattern
  - Retry logic with exponential backoff for transient errors
  - Creates, updates, and deletes files/directories to maintain perfect replica

- **CLI Commands**
  - `sync` - One-time synchronization
  - `daemon` - Continuous sync with configurable interval (1-86400 seconds)
  - `plan` - Preview changes without making modifications (dry-run)
  - `verify` - Confirm replica matches source with exit codes

- **Safety Features**
  - Path validation (prevents source == replica, nested paths)
  - Path traversal protection for security
  - Symbolic link detection and rejection
  - Configurable deletion with `--no-delete` flag
  - Cancellation support (Ctrl+C graceful shutdown)
  - Disk space validation before sync (20% safety margin)
  - Comprehensive exit codes for scripting

- **Configuration & Logging**
  - JSON configuration file support with CLI override
  - Serilog logging to console + rolling file logs (7-day retention)
  - Configurable log levels (Verbose, Debug, Information, Warning, Error)
  - Failure tracking with detailed error reporting
  - Correlation IDs for tracking sync operations

- **Performance Optimizations**
  - Smart hashing (only when needed)
  - Parallel-safe file system operations
  - `--no-checksum` flag for speed
  - Configurable timestamp tolerance (default 2 seconds for FAT32)
  - Memory-efficient chunked hash computation (1MB chunks)
  - Real-time progress reporting with task/byte percentages

- **Testing & Quality**
  - 42 comprehensive unit tests (xUnit + FluentAssertions)
  - Integration tests for end-to-end scenarios
  - Edge case tests (empty directories, special characters, etc.)
  - Complete XML documentation for all public APIs

- **CI/CD & Infrastructure**
  - GitHub Actions workflows (CI, Release, Nightly, Docs)
  - Multi-platform builds (Ubuntu, Windows, macOS)
  - CodeQL security scanning
  - Dependency vulnerability checks
  - Code formatting verification
  - Automated releases with binaries for 4 platforms
  - Performance benchmarking

- **Documentation**
  - Comprehensive README with examples
  - Contributing guidelines
  - Security policy
  - Issue templates (bug reports, feature requests)
  - CI/CD documentation

### Technical Details

- **Target Framework**: .NET 10.0
- **Architecture**: Engine library + CLI tool (thin wrapper)
- **Dependencies**:
  - Serilog (logging)
  - Spectre.Console.Cli (CLI framework)
  - xUnit (testing)
  - FluentAssertions (test assertions)

### Platforms

- Windows x64 (single-file executable)
- Linux x64 (single-file executable)
- macOS x64 (single-file executable)
- macOS ARM64 (single-file executable)

### Breaking Changes

None (initial release)

### Known Issues

None

---

## [Unreleased]

### Planned Features

- Exclusion patterns (glob/regex) for filtering
- JSON output format for scripting integration
- Bandwidth throttling for network shares
- Enhanced progress bars with ETAs
- Bidirectional sync mode
- Incremental backup with versioning
- Web UI for monitoring daemon mode

---

[1.0.0]: https://github.com/daniel-kindl/directory-sync/releases/tag/v1.0.0
[Unreleased]: https://github.com/daniel-kindl/directory-sync/compare/v1.0.0...HEAD
