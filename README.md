# Directory Synchronization Tool (dirsync)

One-way directory synchronization CLI utility written in C#.  
Keeps a **replica folder** identical to a **source folder** with intelligent change detection, safety rails, and flexible operation modes.

---

## Features

### Core Synchronization
- **One-way sync**: source → replica
- **SHA-256 file hashing** for accurate change detection
- **Tiered comparison**: size/timestamp checks before expensive hashing
- **Atomic file operations**: prevent corruption during copy
- Creates, updates, and deletes files/directories to maintain perfect replica

### CLI Commands
- `sync` - One-time synchronization
- `daemon` - Continuous sync with configurable interval
- `plan` - Preview changes without making modifications (dry-run)
- `verify` - Confirm replica matches source (returns exit code)

### Safety & Reliability
- **Path validation**: prevents dangerous operations (source == replica, nested paths)
- **Path traversal protection**: validates all file paths to prevent directory traversal attacks
- **Configurable deletion**: `--no-delete` flag for safe mode
- **Cancellation support**: Ctrl+C gracefully cancels operations
- **Disk space validation**: checks available space before starting sync
- **Exit codes**: scriptable with proper error reporting
- **Comprehensive logging**: Serilog to console + rolling file logs
- **Failure tracking**: detailed reporting of failed operations

### Performance
- **Smart hashing**: only computes SHA-256 when size/timestamp differ
- **Optimized scanning**: parallel-safe file system operations
- **Configurable checksums**: `--no-checksum` for speed when timestamp is sufficient

---

## Installation

### As .NET Global Tool (Recommended)

```bash
dotnet tool install --global DirectorySync.Cli
```

After installation, the `dirsync` command is available globally.

### From Source

```bash
git clone https://github.com/yourusername/directory-sync.git
cd directory-sync
dotnet build
cd Cli
dotnet run -- --help
```

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
  -s, --source <PATH>        Source directory (required)
  -r, --replica <PATH>       Replica directory (required)
  --log-dir <PATH>           Log directory (default: current directory)
  --log-level <LEVEL>        Verbose|Debug|Information|Warning|Error (default: Information)
  --dry-run                  Show what would be done without making changes
  --no-delete                Don't delete files/directories from replica
  --no-checksum              Skip SHA-256 hashing (faster, less accurate)
```

#### `dirsync daemon` - Continuous synchronization

```bash
dirsync daemon -s <source> -r <replica> [options]

Options:
  -i, --interval <SECONDS>   Sync interval in seconds (default: 60)
  (plus all options from sync command)
```

#### `dirsync plan` - Preview changes

```bash
dirsync plan -s <source> -r <replica> [options]

Shows a table of operations that would be performed without executing them.
```

#### `dirsync verify` - Verify replica

```bash
dirsync verify -s <source> -r <replica>

Exit codes:
  0  - Replica matches source
  20 - Replica does not match source
  2  - Validation error
```

---

## Examples

### Basic Sync with Safety

```bash
# Safe sync (no deletes)
dirsync sync -s C:\Important -r D:\Backup --no-delete

# Full mirror (with deletes)
dirsync sync -s C:\Source -r D:\Replica
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

# Stop with Ctrl+C
```

### Scripting & Automation

```bash
# Sync and check exit code
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

---

## Architecture

### Project Structure

```
directory-sync/
├── Engine/                   # Core sync logic (reusable library)
│   ├── SyncEngine.cs         # Main engine with path validation
│   ├── SyncPlanner.cs        # Computes sync operations
│   ├── SyncExecutor.cs       # Executes operations with atomic copy
│   ├── FileScanner.cs        # Scans directories with tiered comparison
│   └── Models.cs             # Data structures
├── Cli/                      # CLI frontend
│   ├── Commands/             # Spectre.Console.Cli commands
│   │   ├── SyncCommand.cs
│   │   ├── DaemonCommand.cs
│   │   ├── PlanCommand.cs
│   │   └── VerifyCommand.cs
│   ├── Program.cs            # CLI entry point
│   └── Logger.cs             # Serilog wrapper
└── [old files]               # Original prototype (deprecated)
```

### Key Design Decisions

**Engine Separation**: Sync logic is a standalone library, CLI is just a thin wrapper. This enables:
- Testing without CLI overhead
- Future GUIs or services without code duplication

**Tiered Comparison**: Avoids expensive SHA-256 hashing when possible:
1. Check file size (fast)
2. Check LastWriteTimeUtc (fast)
3. Only then compute hash (slow but accurate)

**Atomic Copy**: Files are copied to `.tmp` then moved to final location. Prevents half-written files if process is killed.

**Exit Codes**: Enable scripting and CI/CD integration:
- `0` = Success
- `1` = General error (validation, invalid arguments)
- `2` = Path validation error (source == replica, nested paths)
- `3` = Directory not found
- `4` = Permission denied / unauthorized access
- `5` = Insufficient disk space
- `10` = Partial sync failure (some operations failed)
- `20` = Verify mismatch
- `130` = Operation cancelled by user (Ctrl+C)

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (or compatible runtime)

---

## Development

### Build

```bash
dotnet build directory-sync.sln
```

### Test Locally

```bash
cd Cli
dotnet run -- sync -s ../test-source -r ../test-replica
```

### Package

```bash
cd Cli
dotnet pack --configuration Release
```

### Install Local Package

```bash
dotnet tool install --global --add-source ./Cli/bin/Release DirectorySync.Cli
```

---

## License

MIT

---

## Contributing

Contributions welcome! Areas for enhancement:
- Exclusion patterns (glob/regex)
- JSON output for scripting
- Bandwidth throttling
- Progress bars for large syncs
- Unit tests

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
```