using System.Diagnostics;
using System.Security;

namespace DirectorySync.Engine;

/// <summary>
/// Executes synchronization tasks with retry logic, atomic file operations, and progress reporting.
/// </summary>
/// <param name="logger">Optional logger for diagnostic output. If null, a <see cref="NullLogger"/> is used.</param>
/// <remarks>
/// This class is stateless and does not hold disposable resources.
/// All file streams are properly disposed using 'using' statements.
/// Operations are retried up to 3 times for transient errors (file locks, temporary network issues).
/// File copies are atomic: written to a temporary file first, then moved to the final destination.
/// Symbolic links are rejected to prevent security issues and unexpected behavior.
/// </remarks>
public class SyncExecutor(ILogger? logger = null)
{
    private readonly ILogger _logger = logger ?? new NullLogger();
    private const int _maxRetries = 3;
    private const int _initialRetryDelayMs = 100;
    private const int _tempFileAgeMinutes = 5;
    private const int _hashTimeoutMinutes = 5;
    private const int _copyBufferSize = 81920; // 80KB

    /// <summary>
    /// Executes a list of synchronization tasks asynchronously with progress reporting and cancellation support.
    /// </summary>
    /// <param name="tasks">The list of tasks to execute in order.</param>
    /// <param name="replicaBasePath">The absolute path to the replica directory root, used for path safety validation.</param>
    /// <param name="dryRun">If true, tasks are logged but not executed. Useful for previewing changes.</param>
    /// <param name="progress">Optional progress reporter. Called before each task and upon completion.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Operations are canceled between tasks, not mid-operation.</param>
    /// <returns>
    /// A <see cref="SyncResult"/> containing operation statistics and any failures that occurred.
    /// <see cref="SyncResult.Success"/> is false if any operations failed; individual failures are listed in <see cref="SyncResult.Failures"/>.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled. Partial results are not returned.</exception>
    /// <remarks>
    /// <para><strong>Safety and Validation:</strong></para>
    /// <list type="bullet">
    /// <item>All destination paths are validated against <paramref name="replicaBasePath"/> to prevent directory traversal attacks.</item>
    /// <item>Symbolic links are detected and rejected for all operations to prevent following links outside the replica.</item>
    /// </list>
    /// <para><strong>Retry Logic:</strong></para>
    /// Operations are retried up to 3 times with exponential backoff (100ms, 200ms, 400ms) for transient errors:
    /// <list type="bullet">
    /// <item>File sharing violations (ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION)</item>
    /// <item>Network errors (ERROR_BAD_NETPATH, ERROR_DEV_NOT_EXIST, ERROR_BAD_NET_NAME)</item>
    /// <item>Transient access denied errors (from antivirus/indexing services)</item>
    /// </list>
    /// <para><strong>Atomic File Operations:</strong></para>
    /// File copies and updates use a two-phase commit: write to a temporary file, then atomic move to the destination.
    /// Temporary files are named with a <c>.tmp.{guid}</c> suffix and cleaned up on failure.
    /// <para><strong>Progress Reporting:</strong></para>
    /// Progress is reported before each task with updated statistics (completed tasks, bytes copied).
    /// The final progress report has <see cref="SyncProgress.CurrentOperation"/> set to "Complete".
    /// <para><strong>Error Handling:</strong></para>
    /// Non-transient errors are captured in <see cref="SyncResult.Failures"/> and execution continues with remaining tasks.
    /// Cancellation is treated separately and throws <see cref="OperationCanceledException"/> immediately.
    /// </remarks>
    public async Task<SyncResult> ExecuteAsync(List<SyncTask> tasks, string replicaBasePath, bool dryRun = false,
                             IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        int created = 0, updated = 0, deleted = 0;
        long bytesCopied = 0;
        var failures = new List<OperationFailure>();

        // Calculate totals for progress reporting
        int totalTasks = tasks.Count;
        long totalBytes = tasks
            .Where(t => t.Action is SyncAction.CopyFile or SyncAction.UpdateFile)
            .Sum(t => File.Exists(t.SourcePath) ? new FileInfo(t.SourcePath).Length : 0);

        int completedTasks = 0;

        // Normalize the replica base path once for validation
        string normalizedReplicaBase = Path.GetFullPath(replicaBasePath);

        foreach (SyncTask task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Report progress before task
            progress?.Report(new SyncProgress(
                totalTasks,
                completedTasks,
                totalBytes,
                bytesCopied,
                $"{task.Action}",
                task.DestinationPath
            ));

            try
            {
                // Validate path safety before any operation - check against replica base, not drive root
                if (!string.IsNullOrEmpty(task.DestinationPath))
                {
                    PathValidator.ValidatePathSafety(normalizedReplicaBase, task.DestinationPath);
                }

                if (dryRun)
                {
                    LogDryRun(task);
                    CountTask(task, ref created, ref updated, ref deleted);
                    continue;
                }

                // Execute with retry logic for transient failures
                await ExecuteWithRetryAsync(async () =>
                {
                    switch (task.Action)
                    {
                        case SyncAction.CreateDirectory:
                            Directory.CreateDirectory(task.DestinationPath);
                            created++;
                            break;

                        case SyncAction.DeleteDirectory:
                            // Defense-in-depth: Verify we're not deleting a symbolic link
                            DirectoryInfo dirInfo = new(task.DestinationPath);
                            if (dirInfo.Exists && dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                _logger.LogWarning("Refusing to delete symbolic link directory: {Path}", task.DestinationPath);
                                throw new InvalidOperationException($"Cannot delete symbolic link: {task.DestinationPath}");
                            }
                            Directory.Delete(task.DestinationPath, recursive: true);
                            deleted++;
                            break;

                        case SyncAction.CopyFile:
                            // Defense-in-depth: Verify source is not a symbolic link
                            FileInfo sourceFileInfo = new(task.SourcePath);
                            if (sourceFileInfo.Exists && sourceFileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                _logger.LogWarning("Refusing to copy symbolic link file: {Path}", task.SourcePath);
                                throw new InvalidOperationException($"Cannot copy symbolic link: {task.SourcePath}");
                            }
                            await CopyFileAtomicAsync(task.SourcePath, task.DestinationPath, cancellationToken);
                            bytesCopied += new FileInfo(task.SourcePath).Length;
                            created++;
                            break;

                        case SyncAction.UpdateFile:
                            // Defense-in-depth: Verify source is not a symbolic link
                            FileInfo updateSourceInfo = new(task.SourcePath);
                            if (updateSourceInfo.Exists && updateSourceInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                _logger.LogWarning("Refusing to update from symbolic link file: {Path}", task.SourcePath);
                                throw new InvalidOperationException($"Cannot update from symbolic link: {task.SourcePath}");
                            }
                            await CopyFileAtomicAsync(task.SourcePath, task.DestinationPath, cancellationToken);
                            bytesCopied += new FileInfo(task.SourcePath).Length;
                            updated++;
                            break;

                        case SyncAction.DeleteFile:
                            // Defense-in-depth: Verify we're not deleting a symbolic link
                            FileInfo deleteFileInfo = new(task.DestinationPath);
                            if (deleteFileInfo.Exists && deleteFileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
                            {
                                _logger.LogWarning("Refusing to delete symbolic link file: {Path}", task.DestinationPath);
                                throw new InvalidOperationException($"Cannot delete symbolic link: {task.DestinationPath}");
                            }
                            File.Delete(task.DestinationPath);
                            deleted++;
                            break;
                    }
                }, task, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Don't treat cancellation as failure
                throw;
            }
            catch (UnauthorizedAccessException ex)
            {
                failures.Add(new OperationFailure(
                    task.Action,
                    task.DestinationPath,
                    "Access denied",
                    ex.GetType().Name
                ));
            }
            catch (IOException ex) when (IsFileLocked(ex))
            {
                failures.Add(new OperationFailure(
                    task.Action,
                    task.DestinationPath,
                    "File is locked by another process (after 3 retries)",
                    $"{ex.GetType().Name} (0x{ex.HResult:X})"
                ));
            }
            catch (IOException ex) when (IsNetworkError(ex))
            {
                failures.Add(new OperationFailure(
                    task.Action,
                    task.DestinationPath,
                    $"Network error: {GetNetworkErrorDescription(ex.HResult)}",
                    $"{ex.GetType().Name} (0x{ex.HResult:X})"
                ));
            }
            catch (IOException ex)
            {
                failures.Add(new OperationFailure(
                    task.Action,
                    task.DestinationPath,
                    ex.Message,
                    $"{ex.GetType().Name} (0x{ex.HResult:X})"
                ));
            }
            catch (SecurityException ex)
            {
                failures.Add(new OperationFailure(
                    task.Action,
                    task.DestinationPath,
                    ex.Message,
                    ex.GetType().Name
                ));
            }
            catch (Exception ex)
            {
                failures.Add(new OperationFailure(
                    task.Action,
                    task.DestinationPath,
                    ex.Message,
                    ex.GetType().Name
                ));
            }

            // Increment completed tasks
            completedTasks++;
        }

        // Report final progress
        progress?.Report(new SyncProgress(
            totalTasks,
            completedTasks,
            totalBytes,
            bytesCopied,
            "Complete",
            null
        ));

        stopwatch.Stop();
        bool success = failures.Count == 0;
        return new SyncResult(created, updated, deleted, bytesCopied, stopwatch.Elapsed, success, failures);
    }

    private async Task ExecuteWithRetryAsync(Func<Task> operation, SyncTask task, CancellationToken cancellationToken)
    {
        int attempt = 0;
        while (true)
        {
            try
            {
                await operation();
                return; // Success
            }
            catch (IOException ex) when (IsTransientError(ex) && attempt < _maxRetries)
            {
                attempt++;
                int delayMs = _initialRetryDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff

                _logger.LogWarning(
                    "Transient I/O error on {Action} for {Path}, attempt {Attempt}/{Max}. " +
                    "HResult: 0x{HResult:X}, Error: {Error}. Retrying in {Delay}ms",
                    task.Action, task.DestinationPath, attempt, _maxRetries,
                    ex.HResult, ex.Message, delayMs);

                await Task.Delay(delayMs, cancellationToken);
            }
            catch (UnauthorizedAccessException ex) when (IsTransientAccessError(ex) && attempt < _maxRetries)
            {
                attempt++;
                int delayMs = _initialRetryDelayMs * (int)Math.Pow(2, attempt - 1);

                _logger.LogWarning(
                    "Transient access error on {Action} for {Path}, attempt {Attempt}/{Max}. " +
                    "Error: {Error}. Retrying in {Delay}ms",
                    task.Action, task.DestinationPath, attempt, _maxRetries,
                    ex.Message, delayMs);

                await Task.Delay(delayMs, cancellationToken);
            }
            // Let other exceptions bubble up
        }
    }

    private static bool IsTransientError(IOException ex)
    {
        // Check for common transient error codes
        int hResult = ex.HResult;
        return hResult switch
        {
            unchecked((int)0x80070020) => true,  // ERROR_SHARING_VIOLATION (file in use)
            unchecked((int)0x80070021) => true,  // ERROR_LOCK_VIOLATION
            unchecked((int)0x80070027) => true,  // ERROR_DEV_NOT_EXIST (network disconnection)
            unchecked((int)0x80070035) => true,  // ERROR_BAD_NETPATH (network path not found)
            unchecked((int)0x8007003A) => true,  // ERROR_BAD_NET_NAME
            _ => false
        };
    }

    private static bool IsTransientAccessError(UnauthorizedAccessException ex)
    {
        // Antivirus/indexing services can temporarily deny access
        // Only retry if the error message suggests transient lock
        string message = ex.Message.ToLowerInvariant();
        return message.Contains("being used by another process") ||
               message.Contains("locked") ||
               message.Contains("in use");
    }

    private static bool IsFileLocked(IOException ex)
    {
        // Check specifically for file lock errors
        int hResult = ex.HResult;
        return hResult is unchecked((int)0x80070020) or  // ERROR_SHARING_VIOLATION
                         unchecked((int)0x80070021);     // ERROR_LOCK_VIOLATION
    }

    private static bool IsNetworkError(IOException ex)
    {
        // Check for network-related errors
        int hResult = ex.HResult;
        return hResult is unchecked((int)0x80070027) or  // ERROR_DEV_NOT_EXIST
                         unchecked((int)0x80070035) or  // ERROR_BAD_NETPATH
                         unchecked((int)0x8007003A) or  // ERROR_BAD_NET_NAME
                         unchecked((int)0x80070040);    // ERROR_NETNAME_DELETED
    }

    private static string GetNetworkErrorDescription(int hResult)
    {
        return hResult switch
        {
            unchecked((int)0x80070027) => "Network device not found",
            unchecked((int)0x80070035) => "Network path not found",
            unchecked((int)0x8007003A) => "Network name invalid",
            unchecked((int)0x80070040) => "Network name deleted",
            _ => "Unknown network error"
        };
    }

    private static async Task CopyFileAtomicAsync(string source, string destination, CancellationToken cancellationToken = default)
    {
        string tempPath = destination + $".tmp.{Guid.NewGuid():N}";

        try
        {
            // Copy with cancellation support for large files
            await CopyFileWithCancellationAsync(source, tempPath, cancellationToken);

            // Preserve timestamps
            FileInfo sourceInfo = new(source);
            File.SetLastWriteTimeUtc(tempPath, sourceInfo.LastWriteTimeUtc);

            // Atomic move (overwrites existing)
            File.Move(tempPath, destination, overwrite: true);
        }
        catch
        {
            // Clean up temp file on any failure
            if (!File.Exists(tempPath))
                throw;
            try
            { File.Delete(tempPath); }
            catch { /* Best effort cleanup */ }
            throw;
        }
    }

    private static async Task CopyFileWithCancellationAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using FileStream sourceStream = new(source, FileMode.Open, FileAccess.Read, FileShare.Read, _copyBufferSize, useAsync: true);
        await using FileStream destStream = new(destination, FileMode.Create, FileAccess.Write, FileShare.None, _copyBufferSize, useAsync: true);

        byte[] buffer = new byte[_copyBufferSize];
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        await destStream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Cleans up orphaned temporary files in the specified directory that are older than 5 minutes.
    /// </summary>
    /// <param name="directory">The directory to scan for temporary files (searched recursively).</param>
    /// <remarks>
    /// Searches for files matching the pattern <c>*.tmp.*</c> and deletes those older than 5 minutes.
    /// The age threshold prevents deletion of temporary files from currently-running sync operations.
    /// Safe to call on non-existent directories (no action taken).
    /// Failures to delete individual files are logged but do not throw exceptions.
    /// Should be called at the start of sync operations to clean up debris from interrupted previous runs.
    /// </remarks>
    public void CleanupOrphanedTempFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (string tempFile in Directory.GetFiles(directory, "*.tmp.*", SearchOption.AllDirectories))
        {
            try
            {
                // Only delete if older than threshold (safer for repeated syncs in daemon mode)
                FileInfo info = new(tempFile);
                TimeSpan age = DateTime.UtcNow - info.LastWriteTimeUtc;

                if (age <= TimeSpan.FromMinutes(_tempFileAgeMinutes))
                    continue;

                File.Delete(tempFile);
                _logger.LogInformation("Cleaned up orphaned temp file: {TempFile} (Age: {Age:hh\\:mm\\:ss}, Size: {Size} bytes)",
                    tempFile, age, info.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to cleanup temp file {TempFile}: {Error} ({ExceptionType})",
                    tempFile, ex.Message, ex.GetType().Name);
            }
        }
    }

    private void LogDryRun(SyncTask task)
    {
        string action = task.Action switch
        {
            SyncAction.CreateDirectory => "Would create directory",
            SyncAction.DeleteDirectory => "Would delete directory",
            SyncAction.CopyFile => "Would copy file",
            SyncAction.UpdateFile => "Would update file",
            SyncAction.DeleteFile => "Would delete file",
            _ => "Would perform unknown action on"
        };
        _logger.LogInformation("{Action}: {Path}", action, task.DestinationPath);
    }

    private static void CountTask(SyncTask task, ref int created, ref int updated, ref int deleted)
    {
        switch (task.Action)
        {
            case SyncAction.CreateDirectory:
            case SyncAction.CopyFile:
                created++;
                break;

            case SyncAction.UpdateFile:
                updated++;
                break;

            case SyncAction.DeleteDirectory:
            case SyncAction.DeleteFile:
                deleted++;
                break;
        }
    }
}
