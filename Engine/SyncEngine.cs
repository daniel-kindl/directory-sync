using System.Diagnostics;
using System.Security;

namespace DirectorySync.Engine;

/// <summary>
/// Main synchronization engine that coordinates scanning, planning, and execution of directory synchronization.
/// </summary>
/// <remarks>
/// <para>This class is stateless and does not hold disposable resources.</para>
/// <para>The static <see cref="ActivitySource"/> is used for distributed tracing and telemetry and does not require disposal.</para>
/// <para>Orchestrates <see cref="FileScanner"/>, <see cref="SyncPlanner"/>, and <see cref="SyncExecutor"/> to perform complete sync operations.</para>
/// <para>Validates paths, checks disk space, and provides both synchronous and asynchronous APIs.</para>
/// </remarks>
public class SyncEngine
{
    private readonly FileScanner _scanner;
    private readonly SyncPlanner _planner;
    private readonly SyncExecutor _executor;
    private readonly ILogger _logger;
    private static readonly ActivitySource _activitySource = new("DirectorySync", "1.0.0");

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncEngine"/> class with the specified options.
    /// </summary>
    /// <param name="useChecksum">If true, uses SHA-256 checksums for file comparison. If false, uses only size and timestamp. Default is true.</param>
    /// <param name="useTimestampOptimization">If true, skips checksum computation for files with matching size and timestamp. Default is true.</param>
    /// <param name="logger">Optional logger for diagnostic output. If null, a <see cref="NullLogger"/> is used.</param>
    /// <param name="timestampTolerance">Tolerance for timestamp comparisons. If null, defaults to 2 seconds for FAT32 compatibility.</param>
    /// <remarks>
    /// Creates internal instances of <see cref="FileScanner"/>, <see cref="SyncPlanner"/>, and <see cref="SyncExecutor"/> with the specified configuration.
    /// Sets the logger on the static <see cref="PathValidator"/> for security logging.
    /// </remarks>
    public SyncEngine(bool useChecksum = true, bool useTimestampOptimization = true,
                     ILogger? logger = null, TimeSpan? timestampTolerance = null)
    {
        _logger = logger ?? new NullLogger();
        _scanner = new FileScanner(useChecksum, useTimestampOptimization, _logger, timestampTolerance);
        _planner = new SyncPlanner(_logger);
        _executor = new SyncExecutor(_logger);

        // Set logger for static PathValidator
        PathValidator.SetLogger(_logger);
    }

    /// <summary>
    /// Validates that source and replica paths are suitable for synchronization.
    /// </summary>
    /// <param name="source">The source directory path (absolute or relative).</param>
    /// <param name="replica">The replica directory path (absolute or relative).</param>
    /// <exception cref="ArgumentException">Thrown when source and replica are the same, or one is nested inside the other.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
    /// <remarks>
    /// <para>Validation checks performed:</para>
    /// <list type="bullet">
    /// <item>Source and replica must not resolve to the same path.</item>
    /// <item>Replica must not be a subdirectory of source (would cause infinite recursion).</item>
    /// <item>Source must not be a subdirectory of replica (would cause data loss).</item>
    /// <item>Source directory must exist.</item>
    /// </list>
    /// <para>The replica directory is created automatically if it doesn't exist (not validated here).</para>
    /// <para>Paths are normalized to absolute paths with <see cref="Path.GetFullPath(string)"/> before comparison.</para>
    /// </remarks>
    public static void ValidatePaths(string source, string replica)
    {
        string fullSource = Path.GetFullPath(source);
        string fullReplica = Path.GetFullPath(replica);

        if (fullSource.Equals(fullReplica, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Source and replica cannot be the same path");
        }

        if (fullReplica.StartsWith(fullSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Replica cannot be inside source");
        }

        if (fullSource.StartsWith(fullReplica + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Source cannot be inside replica");
        }

        if (!Directory.Exists(fullSource))
        {
            throw new DirectoryNotFoundException($"Source directory does not exist: {fullSource}");
        }
    }

    /// <summary>
    /// Creates a synchronization plan without executing any operations.
    /// </summary>
    /// <param name="options">Synchronization options including source, replica, and deletion policy.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Checked during scanning.</param>
    /// <returns>
    /// A list of <see cref="SyncTask"/> objects representing operations needed to synchronize the replica with the source.
    /// Returns an empty list if the replica is already synchronized.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when paths fail validation (see <see cref="ValidatePaths"/>).</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to source or replica is denied.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during scanning.</exception>
    /// <remarks>
    /// This method performs directory scanning but does not modify the file system.
    /// Useful for previewing changes or implementing custom execution logic.
    /// The returned tasks are ordered: directories first, files second, deletions last (if enabled).
    /// </remarks>
    public List<SyncTask> Plan(SyncOptions options, CancellationToken cancellationToken = default)
    {
        ValidatePaths(options.Source, options.Replica);

        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<string, SyncItem> sourceItems = _scanner.ScanDirectory(options.Source, cancellationToken, computeChecksums: false);

        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<string, SyncItem> replicaItems = _scanner.ScanDirectory(options.Replica, cancellationToken, computeChecksums: false);

        foreach ((string path, SyncItem srcItem) in sourceItems.ToList())
        {
            if (srcItem.IsDir || !replicaItems.TryGetValue(path, out SyncItem? repItem) || repItem.IsDir)
            {
                continue;
            }

            string sourcePath = Path.Combine(options.Source, path);
            string replicaPath = Path.Combine(options.Replica, path);

            if (_scanner.AreFilesIdentical(sourcePath, replicaPath))
            {
                sourceItems.Remove(path);
                replicaItems.Remove(path);
            }
        }

        return _planner.GetSyncTasks(sourceItems, options.Source, replicaItems, options.Replica, options.AllowDelete);
    }

    /// <summary>
    /// Performs a complete synchronization operation synchronously.
    /// </summary>
    /// <param name="options">Synchronization options including source, replica, dry-run mode, and deletion policy.</param>
    /// <param name="progress">Optional progress reporter for real-time updates during execution.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Checked between operations.</param>
    /// <returns>A <see cref="SyncResult"/> containing statistics and any failures.</returns>
    /// <exception cref="ArgumentException">Thrown when paths fail validation.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
    /// <exception cref="IOException">Thrown when insufficient disk space is detected or I/O errors occur.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    /// This is a synchronous wrapper around <see cref="SyncAsync"/> for backward compatibility.
    /// Blocks the calling thread until synchronization completes.
    /// For async/await patterns, use <see cref="SyncAsync"/> directly.
    /// </remarks>
    public SyncResult Sync(SyncOptions options, IProgress<SyncProgress>? progress = null,
                          CancellationToken cancellationToken = default)
    {
        // Synchronous wrapper for backward compatibility
        return SyncAsync(options, progress, cancellationToken).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Performs a complete synchronization operation asynchronously.
    /// </summary>
    /// <param name="options">Synchronization options including source, replica, dry-run mode, and deletion policy.</param>
    /// <param name="progress">Optional progress reporter for real-time updates during execution.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Checked throughout the operation.</param>
    /// <returns>
    /// A task that completes with a <see cref="SyncResult"/> containing statistics and any failures.
    /// The result's <see cref="SyncResult.CorrelationId"/> can be used to correlate logs and telemetry.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when paths fail validation.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
    /// <exception cref="IOException">Thrown when insufficient disk space is detected (before execution) or I/O errors occur.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    /// <exception cref="SecurityException">Thrown when path traversal attacks are detected.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <remarks>
    /// <para><strong>Operation Sequence:</strong></para>
    /// <list type="number">
    /// <item>Validates paths using <see cref="ValidatePaths"/>.</item>
    /// <item>Cleans up orphaned temporary files from previous interrupted runs.</item>
    /// <item>Scans source and replica directories.</item>
    /// <item>Creates a synchronization plan.</item>
    /// <item>Validates disk space (unless dry-run mode).</item>
    /// <item>Executes the plan with retry logic and progress reporting.</item>
    /// </list>
    /// <para><strong>Telemetry:</strong></para>
    /// Creates an <see cref="Activity"/> for distributed tracing with tags for all operation parameters and results.
    /// Activity ID is used as the correlation ID in logs and results.
    /// <para><strong>Disk Space Validation:</strong></para>
    /// Before execution (unless <see cref="SyncOptions.DryRun"/> is true), validates that the replica drive has sufficient free space
    /// with a 20% safety margin (or 100MB, whichever is larger) to account for filesystem overhead.
    /// <para><strong>Error Handling:</strong></para>
    /// Individual operation failures are captured in <see cref="SyncResult.Failures"/>; the method returns normally unless a fatal error occurs.
    /// Fatal errors (path validation, disk space, source not found) throw exceptions.
    /// </remarks>
    public async Task<SyncResult> SyncAsync(SyncOptions options, IProgress<SyncProgress>? progress = null,
                          CancellationToken cancellationToken = default)
    {
        using Activity? activity = _activitySource.StartActivity("Sync", ActivityKind.Internal);
        string correlationId = activity?.Id ?? Guid.NewGuid().ToString();

        _logger.LogInformation(
            "Sync started - CorrelationId: {CorrelationId}, Source: {Source}, Replica: {Replica}, DryRun: {DryRun}",
            correlationId, options.Source, options.Replica, options.DryRun);

        activity?.SetTag("sync.source", options.Source);
        activity?.SetTag("sync.replica", options.Replica);
        activity?.SetTag("sync.dryRun", options.DryRun);
        activity?.SetTag("sync.useChecksum", options.UseChecksum);

        try
        {
            ValidatePaths(options.Source, options.Replica);
            _executor.CleanupOrphanedTempFiles(options.Replica);

            List<SyncTask> tasks = Plan(options, cancellationToken);

            activity?.SetTag("sync.totalTasks", tasks.Count);
            _logger.LogInformation("Sync plan created - CorrelationId: {CorrelationId}, Tasks: {TaskCount}",
                correlationId, tasks.Count);

            if (!options.DryRun)
            {
                ValidateDiskSpace(options.Replica, tasks);
            }

            SyncResult result = await _executor.ExecuteAsync(tasks, options.Replica, options.DryRun, progress, cancellationToken);

            // Add telemetry tags
            activity?.SetTag("sync.created", result.Created);
            activity?.SetTag("sync.updated", result.Updated);
            activity?.SetTag("sync.deleted", result.Deleted);
            activity?.SetTag("sync.bytesCopied", result.BytesCopied);
            activity?.SetTag("sync.duration", result.Duration.TotalSeconds);
            activity?.SetTag("sync.success", result.Success);
            activity?.SetTag("sync.failures", result.Failures.Count);

            if (result.Success)
            {
                _logger.LogInformation(
                    "Sync completed successfully - CorrelationId: {CorrelationId}, Created: {Created}, " +
                    "Updated: {Updated}, Deleted: {Deleted}, BytesCopied: {BytesCopied}, Duration: {Duration}s",
                    correlationId, result.Created, result.Updated, result.Deleted,
                    result.BytesCopied, result.Duration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning(
                    "Sync completed with failures - CorrelationId: {CorrelationId}, FailureCount: {FailureCount}",
                    correlationId, result.Failures.Count);

                activity?.SetStatus(ActivityStatusCode.Error, $"{result.Failures.Count} operations failed");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync failed - CorrelationId: {CorrelationId}, Error: {Error}",
                correlationId, ex.Message);

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    private void ValidateDiskSpace(string replicaPath, List<SyncTask> tasks)
    {
        long requiredBytes = SyncPlanner.CalculateRequiredSpace(tasks);

        if (requiredBytes == 0)
        {
            return; // No files to copy
        }

        string fullReplicaPath = Path.GetFullPath(replicaPath);
        string? rootPath = Path.GetPathRoot(fullReplicaPath);

        if (string.IsNullOrEmpty(rootPath))
        {
            return; // Unable to determine drive
        }

        DriveInfo drive = new(rootPath);

        if (!drive.IsReady)
        {
            _logger.LogWarning("Cannot check disk space: Drive {Drive} is not ready", rootPath);
            return;
        }

        // Use 20% margin or minimum 100MB, whichever is larger
        // This accounts for filesystem overhead, compression, and near-full drives
        long requiredWithMargin = Math.Max(
            (long)(requiredBytes * 1.2),
            requiredBytes + (100 * 1024 * 1024)
        );

        if (drive.AvailableFreeSpace < requiredWithMargin)
        {
            throw new IOException(
                $"Insufficient disk space on {drive.Name}. " +
                $"Required: {FormatStrings.FormatBytes(requiredWithMargin)} (includes safety margin), " +
                $"Available: {FormatStrings.FormatBytes(drive.AvailableFreeSpace)}, " +
                $"Total: {FormatStrings.FormatBytes(drive.TotalSize)}");
        }

        _logger.LogDebug(
            "Disk space check passed on {Drive}: Required={Required}, Available={Available}, " +
            "Total={Total}, UsagePercent={UsagePercent:F1}%",
            drive.Name,
            FormatStrings.FormatBytes(requiredWithMargin),
            FormatStrings.FormatBytes(drive.AvailableFreeSpace),
            FormatStrings.FormatBytes(drive.TotalSize),
            (1.0 - (double)drive.AvailableFreeSpace / drive.TotalSize) * 100);
    }

    /// <summary>
    /// Verifies that the replica directory exactly matches the source directory.
    /// </summary>
    /// <param name="source">The source directory path.</param>
    /// <param name="replica">The replica directory path to verify.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Checked during scanning.</param>
    /// <returns>
    /// True if the replica exactly matches the source (same files, directories, sizes, and hashes); false otherwise.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when paths fail validation.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the source directory does not exist.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access is denied.</exception>
    /// <exception cref="IOException">Thrown when I/O errors occur during scanning.</exception>
    /// <remarks>
    /// <para>Verification criteria:</para>
    /// <list type="bullet">
    /// <item>Item count must match exactly.</item>
    /// <item>Every item in source must exist in replica with the same relative path.</item>
    /// <item>Each item must have the same type (file vs. directory).</item>
    /// <item>For files, size and hash must match exactly.</item>
    /// </list>
    /// <para>This method scans both directories completely and compares their snapshots.</para>
    /// <para>Checksums are always computed for verification regardless of <see cref="SyncEngine"/> configuration.</para>
    /// <para>Does not report which items differ; returns false immediately upon finding the first mismatch.</para>
    /// </remarks>
    public bool Verify(string source, string replica, CancellationToken cancellationToken = default)
    {
        ValidatePaths(source, replica);

        Dictionary<string, SyncItem> sourceItems = _scanner.ScanDirectory(source, cancellationToken);
        Dictionary<string, SyncItem> replicaItems = _scanner.ScanDirectory(replica, cancellationToken);

        if (sourceItems.Count != replicaItems.Count)
        {
            return false;
        }

        foreach ((string? path, SyncItem? srcItem) in sourceItems)
        {
            if (!replicaItems.TryGetValue(path, out SyncItem? repItem))
            {
                return false;
            }

            if (srcItem.IsDir != repItem.IsDir)
            {
                return false;
            }

            if (!srcItem.IsDir && (srcItem.Size != repItem.Size || srcItem.ItemHash != repItem.ItemHash))
            {
                return false;
            }
        }

        return true;
    }
}
