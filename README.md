# Directory Synchronization Tool

A simple one-way synchronization tool written in C#.  
It ensures that a **replica folder** is kept identical to a **source folder** by periodically synchronizing files and directories.

---

## Features
- One-way sync: source ---> replica
- Periodic synchronization (interval set by user)
- SHA-256 file hashing to detect changes
- Creates, updates, and deletes files/directories
- Logging to **console** and **log file** (`log.txt`), using Serilog
- Graceful cancellation with **Ctrl+C**

---

## Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download) (or compatible runtime)

---

## Usage

```bash
dotnet run -- <sourceDir> <replicaDir> <logDir> <intervalSeconds>
```

Example:
```bash
dotnet run "C:\Data\Source" "D:\Backups\Replica" "C:\Logs" 60
```

---

## Output

Example:
```bash
[11:45:03 INF] Executing 3 synchronization tasks...
[11:45:03 INF] Copying file: C:\Data\Source\report.txt to D:\Backups\Replica\report.txt
[11:45:03 INF] Deleted file: D:\Backups\Replica\old.txt
[11:45:03 INF] Created directory: D:\Backups\Replica\archive
```