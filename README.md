# Directory Synchronization Tool (dirsync)

[![CI](https://github.com/daniel-kindl/directory-sync/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/daniel-kindl/directory-sync/actions/workflows/ci.yml)
[![Release](https://github.com/daniel-kindl/directory-sync/actions/workflows/release.yml/badge.svg)](https://github.com/daniel-kindl/directory-sync/actions/workflows/release.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download)

Production-ready one-way directory synchronization CLI utility written in C#.  
Keeps a **replica folder** identical to a **source folder** with intelligent change detection, safety rails, and flexible operation modes.

---

## Features

### Core Synchronization
- **One-way sync**: source → replica with perfect mirroring
- **SHA-256 file hashing** for accurate change detection
- **Tiered comparison**: size/timestamp checks before expensive hashing
- **Atomic file operations**: prevent corruption during copy (temp file + move)
- **Retry logic**: automatic retry with exponential backoff for transient errors
- Creates, updates, and deletes files/directories to maintain perfect replica

### CLI Commands
- `sync` - One-time synchronization
- `daemon` - Continuous sync with configurable interval
- `plan` - Preview changes without making modifications (dry-run)
- `verify` - Confirm replica matches source (returns exit code)

### Safety & Reliability
- **Path validation**: prevents dangerous operations (source == replica, nested paths)
- **Path traversal protection**: validates all file paths to prevent directory traversal attacks
- **Symbolic link rejection**: automatically detects and skips symbolic links
- **Configurable deletion**: `--no-delete` flag for safe mode
- **Cancellation support**: Ctrl+C gracefully cancels operations
- **Disk space validation**: checks available space before starting sync (with 20% safety margin)
- **Exit codes**: scriptable with proper error reporting (see below)
- **Comprehensive logging**: Serilog to console + rolling file logs (7-day retention)
- **Failure tracking**: detailed reporting of failed operations with error types
- **Configuration file support**: JSON config with CLI override capability

### Performance
- **Smart hashing**: only computes SHA-256 when size/timestamp differ
- **Optimized scanning**: parallel-safe file system operations
- **Configurable checksums**: `--no-checksum` for speed when timestamp is sufficient
- **Configurable timestamp tolerance**: default 2 seconds for FAT32 compatibility
- **Memory-efficient**: chunked hash computation (1MB chunks)
- **Progress reporting**: real-time progress updates with task/byte percentages

### Quality & Documentation
- **Complete XML documentation** for all public APIs
- **42 passing unit tests** with xUnit and FluentAssertions
- **Comprehensive CI/CD**: GitHub Actions with multi-platform builds
- **Security scanning**: CodeQL and vulnerability checks
- **Code coverage**: tracked and reported

---

## Installation

### As .NET Global Tool (Recommended)

```bash
dotnet tool install --global DirectorySync.Cli
```

After installation, the `dirsync` command is available globally.

### From Source

```bash
git clone https://github.com/daniel-kindl/directory-sync.git
cd directory-sync
dotnet build
cd Cli
dotnet run -- --help
```

### Download Binary

Download pre-built binaries from [GitHub Releases](https://github.com/daniel-kindl/directory-sync/releases):
- Windows x64 (single-file executable)
- Linux x64 (single-file executable)
- macOS x64 (single-file executable)
- macOS ARM64 (single-file executable)

---

## Usage

### Quick Start

```bash
# One-time sync
dirsync sync -s C:\Source -r C:\Replica

# Continuous sync every 60 seconds
dirsync daemon -s C:\Source -r C:\Replica -i 60

# Preview changes without syncing
dirsync plan -s C:\Source -r C:\Replica

# Verify replica matches source
dirsync verify -s C:\Source -r C:\Replica
```

### Command Reference

#### `dirsync sync` - One-time synchronization

```bash
dirsync sync -s <source> -r <replica> [options]

Options:
  -c, --config <PATH>            Configuration file path (default: dirsync.json)
  -s, --source <PATH>            Source directory (required)
  -r, --replica <PATH>           Replica directory (required)
  --log-dir <PATH>               Log directory (default: current directory)
  --log-level <LEVEL>            Verbose|Debug|Information|Warning|Error (default: Information)
  --dry-run                      Show what would be done without making changes
  --no-delete                    Don't delete files/directories from replica
  --no-checksum                  Skip SHA-256 hashing (faster, less accurate)
  --timestamp-tolerance <SEC>    Timestamp comparison tolerance in seconds (default: 2)
```

#### `dirsync daemon` - Continuous synchronization

```bash
dirsync daemon -s <source> -r <replica> [options]

Options:
  -i, --interval <SECONDS>       Sync interval in seconds (default: 60, range: 1-86400)
  (plus all options from sync command)
```

#### `dirsync plan` - Preview changes

```bash
dirsync plan -s <source> -r <replica> [options]

Shows a table of operations that would be performed without executing them.
Uses all sync options except --dry-run (implicitly enabled).
```

#### `dirsync verify` - Verify replica

```bash
dirsync verify -s <source> -r <replica> [--log-dir <PATH>]

Performs full verification with checksum comparison.
Exit codes:
  0  - Replica matches source perfectly
  20 - Replica does not match source
  2  - Validation error
  3  - Directory not found
  4  - Permission denied
```

### Configuration File

Create `dirsync.json` for persistent configuration:

```json
{
  "source": "C:\\Source",
  "replica": "D:\\Replica",
  "interval": 300,
  "allowDelete": true,
  "useChecksum": true,
  "useTimestampOptimization": true,
  "timestampTolerance": 2.0,
  "logDirectory": "C:\\Logs",
  "dryRun": false
}
```

CLI arguments override configuration file values.

---

## Examples

### Basic Sync with Safety

```bash
# Safe sync (no deletes)
dirsync sync -s C:\Important -r D:\Backup --no-delete

# Full mirror (with deletes)
dirsync sync -s C:\Source -r D:\Replica

# Dry run first to preview
dirsync sync -s C:\Source -r D:\Replica --dry-run
```

### Planning Before Sync

```bash
# See what would change
dirsync plan -s C:\Source -r D:\Replica

# If looks good, execute
dirsync sync -s C:\Source -r D:\Replica
```

### Continuous Daemon

```bash
# Run forever, sync every 5 minutes
dirsync daemon -s C:\Source -r D:\Replica -i 300

# With configuration file
dirsync daemon -c dirsync.json

# Stop with Ctrl+C (graceful shutdown)
```

### Scripting & Automation

```powershell
# PowerShell: Sync and check exit code
dirsync sync -s C:\Source -r D:\Replica
if ($LASTEXITCODE -eq 0) {
    Write-Host "Sync successful"
} else {
    Write-Host "Sync failed with code $LASTEXITCODE"
    exit $LASTEXITCODE
}

# Verify before critical operation
dirsync verify -s C:\Source -r D:\Replica
if ($LASTEXITCODE -ne 0) {
    Write-Error "Replica out of sync!"
    exit 1
}
```

```bash
# Bash: Run as cron job
0 */6 * * * /usr/local/bin/dirsync sync -s /source -r /replica >> /var/log/dirsync.log 2>&1
```

---

## Architecture

### Project Structure

```
directory-sync/
├── Engine/                   # Core sync logic (reusable library)
│   ├── SyncEngine.cs         # Main orchestration with telemetry
│   ├── SyncPlanner.cs        # Computes sync operations (3-phase)
│   ├── SyncExecutor.cs       # Executes operations with atomic copy & retry
│   ├── FileScanner.cs        # Scans directories with tiered comparison
│   ├── PathValidator.cs      # Security validation (path traversal protection)
│   ├── FormatStrings.cs      # Formatting utilities
│   ├── Models.cs             # Data structures (records/enums)
│   └── ILogger.cs            # Logging abstraction
├── Cli/                      # CLI frontend (thin wrapper)
│   ├── Commands/             # Spectre.Console.Cli commands
│   │   ├── SyncCommand.cs
│   │   ├── DaemonCommand.cs
│   │   ├── PlanCommand.cs
│   │   └── VerifyCommand.cs
│   ├── Program.cs            # CLI entry point
│   ├── ConfigFile.cs         # JSON configuration support
│   ├── Logger.cs             # Serilog initialization
│   ├── PathValidation.cs     # CLI input validation
│   ├── ExitCodes.cs          # Exit code constants
│   └── SerilogAdapter.cs     # ILogger → Serilog adapter
├── Tests/                    # Unit tests (42 tests, all passing)
├── .github/                  # GitHub Actions CI/CD
│   ├── workflows/
│   │   ├── ci.yml            # Build, test, security scan
│   │   ├── release.yml       # Automated releases
│   │   ├── nightly.yml       # Extended tests & benchmarks
│   │   └── docs.yml          # Documentation generation
│   ├── ISSUE_TEMPLATE/       # Bug reports & feature requests
│   └── dependabot.yml        # Automated dependency updates
├── LICENSE                   # MIT License
├── CHANGELOG.md              # Version history
├── CONTRIBUTING.md           # Contribution guidelines
├── SECURITY.md               # Security policy
└── README.md                 # This file
```

### Key Design Decisions

**Engine Separation**: Sync logic is a standalone library, CLI is just a thin wrapper. This enables:
- Testing without CLI overhead
- Future GUIs, services, or libraries without code duplication
- NuGet package reusability (DirectorySync.Engine)

**Tiered Comparison**: Avoids expensive SHA-256 hashing when possible:
1. Check file size (fast, eliminates 90% of differences)
2. Check LastWriteTimeUtc within tolerance (fast, catches most updates)
3. Only then compute SHA-256 hash (slow but cryptographically accurate)

**Atomic Copy**: Files are copied to `.tmp.{guid}` then moved to final location. Prevents half-written files if process is killed. Temporary files older than 5 minutes are cleaned up automatically.

**Retry Logic**: Transient errors (file locks, network issues) are retried up to 3 times with exponential backoff (100ms, 200ms, 400ms). Permanent errors are reported immediately.

**Exit Codes**: Enable scripting and CI/CD integration:
- `0` = Success (all operations completed)
- `1` = General error (validation, invalid arguments)
- `2` = Path validation error (source == replica, nested paths)
- `3` = Directory not found
- `4` = Permission denied / unauthorized access
- `5` = Insufficient disk space
- `10` = Partial sync failure (some operations failed, see logs)
- `20` = Verify mismatch (replica differs from source)
- `130` = Operation cancelled by user (Ctrl+C, follows Bash SIGINT convention)

**Security**: Multiple layers of protection:
- Path validation against traversal attacks
- Symbolic link detection and rejection
- Safe path normalization and comparison
- Replica path sandboxing (operations stay within replica directory)

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) for building from source
- [.NET 10 Runtime](https://dotnet.microsoft.com/download) for running pre-built binaries
- Supported platforms: Windows, Linux, macOS (x64 and ARM64)

---

## Development

### Build

```bash
dotnet restore
dotnet build directory-sync.sln --configuration Release
```

### Run Tests

```bash
dotnet test --verbosity normal
```

### Test Locally

```bash
cd Cli
dotnet run -- sync -s ../test-source -r ../test-replica
```

### Package for NuGet

```bash
# Engine library
dotnet pack Engine/DirectorySync.Engine.csproj --configuration Release

# CLI tool
dotnet pack Cli/DirectorySync.Cli.csproj --configuration Release
```

### Install Local Build as Global Tool

```bash
dotnet tool install --global --add-source ./Cli/bin/Release DirectorySync.Cli
```

### Code Quality

```bash
# Format code
dotnet format

# Check formatting
dotnet format --verify-no-changes
```

---

## CI/CD

Automated GitHub Actions workflows:

- **CI**: Multi-platform builds (Ubuntu, Windows, macOS), tests, code quality, security scanning
- **Release**: Automatic releases with single-file executables for 4 platforms
- **Nightly**: Extended tests, dependency health checks, performance benchmarks
- **Documentation**: API docs generation with DocFX

See [.github/CI_CD_GUIDE.md](.github/CI_CD_GUIDE.md) for details.

---

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Development setup
- Coding standards
- Pull request process
- Commit message conventions

See [SECURITY.md](SECURITY.md) for reporting security vulnerabilities.

### Ideas for Enhancement
- Exclusion patterns (glob/regex) for filtering
- JSON output format for scripting integration
- Bandwidth throttling for network shares
- Enhanced progress bars with ETAs
- Bidirectional sync mode
- Incremental backup with versioning
- Web UI for monitoring daemon mode

---

## License

MIT License - see [LICENSE](LICENSE) file for details.

Copyright (c) 2025 Daniel Kindl

---

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history and release notes.

---

## Support

- 📖 [Documentation](https://github.com/daniel-kindl/directory-sync#readme)
- 🐛 [Report Bug](https://github.com/daniel-kindl/directory-sync/issues/new?template=bug_report.yml)
- 💡 [Request Feature](https://github.com/daniel-kindl/directory-sync/issues/new?template=feature_request.yml)
- 💬 [Discussions](https://github.com/daniel-kindl/directory-sync/discussions)

---

## Output Examples

### Plan Command

```
Found 4 operations needed:

┌────────────┬───────────────────────────────────┐
│ Action     │ Path                              │
├────────────┼───────────────────────────────────┤
│ Create Dir │ D:\Replica\subdir                 │
│ Copy File  │ D:\Replica\file1.txt              │
│ Copy File  │ D:\Replica\file2.txt              │
│ Copy File  │ D:\Replica\subdir\nested.txt      │
└────────────┴───────────────────────────────────┘
```

### Sync Command

```
[17:29:37 INF] Starting directory synchronization
[17:29:37 INF] Source: C:\Source
[17:29:37 INF] Replica: D:\Replica

Progress: 4/4 tasks (100.0%), 100.0% bytes - Complete: null

┌──────────────┬──────────┐
│ Metric       │ Value    │
├──────────────┼──────────┤
│ Created      │ 4        │
│ Updated      │ 0        │
│ Deleted      │ 0        │
│ Bytes Copied │ 2.4 MB   │
│ Duration     │ 00:00:02 │
│ Status       │ Success  │
└──────────────┴──────────┘

[17:29:39 INF] Synchronization completed
```

### Daemon Mode

```
[18:00:00 INF] Starting daemon mode with 60 second interval
[18:00:00 INF] Sync started - CorrelationId: abc-123
[18:00:02 INF] Sync completed successfully - Created: 0, Updated: 1, Deleted: 0
[18:01:00 INF] Starting sync cycle 2
[18:01:01 INF] Sync completed successfully - Created: 0, Updated: 0, Deleted: 0
```