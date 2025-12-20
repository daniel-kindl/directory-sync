using System.Diagnostics;

namespace DirectorySync.Engine;

/// <summary>
/// Defines the types of file system operations that can be performed during synchronization.
/// </summary>
public enum SyncAction
{
    /// <summary>
    /// Creates a new directory in the replica.
    /// </summary>
    CreateDirectory,

    /// <summary>
    /// Deletes an existing directory from the replica (recursive deletion).
    /// </summary>
    DeleteDirectory,

    /// <summary>
    /// Copies a new file to the replica.
    /// </summary>
    CopyFile,

    /// <summary>
    /// Updates an existing file in the replica with the source version.
    /// </summary>
    UpdateFile,

    /// <summary>
    /// Deletes a file from the replica.
    /// </summary>
    DeleteFile
}

/// <summary>
/// Represents a single synchronization operation to be executed.
/// </summary>
/// <param name="Action">The type of synchronization action to perform.</param>
/// <param name="SourcePath">The full path to the source file or directory. May be empty for delete operations.</param>
/// <param name="DestinationPath">The full path to the destination (replica) file or directory.</param>
public record SyncTask(SyncAction Action, string SourcePath, string DestinationPath);

/// <summary>
/// Represents metadata for a file or directory during scanning and comparison.
/// </summary>
/// <param name="IsDir">True if this item is a directory; false if it is a file.</param>
/// <param name="ItemHash">SHA-256 hash of the file content (uppercase hex string). Empty for directories or when checksum is disabled.</param>
/// <param name="Size">Size of the file in bytes. Zero for directories.</param>
/// <param name="LastWriteUtc">Last write time in UTC. <see cref="DateTime.MinValue"/> for directories.</param>
public record SyncItem(bool IsDir, string ItemHash, long Size, DateTime LastWriteUtc);

/// <summary>
/// Contains the results of a synchronization operation including statistics and failure information.
/// </summary>
/// <param name="Created">Number of files and directories created in the replica.</param>
/// <param name="Updated">Number of files updated in the replica.</param>
/// <param name="Deleted">Number of files and directories deleted from the replica.</param>
/// <param name="BytesCopied">Total number of bytes copied during the operation.</param>
/// <param name="Duration">Total elapsed time for the synchronization operation.</param>
/// <param name="Success">True if all operations succeeded; false if any operations failed.</param>
/// <param name="Failures">List of operations that failed during synchronization. Empty if <paramref name="Success"/> is true.</param>
/// <remarks>
/// The <see cref="CorrelationId"/> is automatically populated from the current <see cref="Activity"/> or a new GUID.
/// This class is immutable and thread-safe for reading.
/// </remarks>
public record SyncResult(
    int Created,
    int Updated,
    int Deleted,
    long BytesCopied,
    TimeSpan Duration,
    bool Success,
    List<OperationFailure> Failures
)
{
    /// <summary>
    /// Gets a unique identifier for this synchronization operation, used for correlation in logs and telemetry.
    /// Defaults to the current <see cref="Activity.Id"/> or a new GUID if no activity is present.
    /// </summary>
    public string CorrelationId { get; init; } = Activity.Current?.Id ?? Guid.NewGuid().ToString();
};

/// <summary>
/// Records details of a failed synchronization operation.
/// </summary>
/// <param name="Action">The synchronization action that failed.</param>
/// <param name="Path">The file or directory path where the failure occurred (typically the destination path).</param>
/// <param name="ErrorMessage">A human-readable description of the error.</param>
/// <param name="ExceptionType">The .NET exception type name, or null if not applicable. May include HRESULT for I/O errors.</param>
public record OperationFailure(
    SyncAction Action,
    string Path,
    string ErrorMessage,
    string? ExceptionType
);

/// <summary>
/// Configuration options for a synchronization operation.
/// </summary>
/// <remarks>
/// This class is immutable after construction. All properties use init-only setters.
/// Default values: <see cref="AllowDelete"/> = true, <see cref="UseChecksum"/> = true, <see cref="UseTimestampOptimization"/> = true.
/// </remarks>
public class SyncOptions
{
    /// <summary>
    /// Gets the source directory path (absolute path required).
    /// The source is the authoritative directory; the replica will be synchronized to match it.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Gets the replica directory path (absolute path required).
    /// The replica will be modified to match the source directory structure and contents.
    /// </summary>
    public required string Replica { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a dry run (no actual changes will be made).
    /// When true, operations are logged but not executed. Useful for previewing changes.
    /// </summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets a value indicating whether files and directories can be deleted from the replica.
    /// When false, items present in replica but missing from source are retained. Default is true.
    /// </summary>
    public bool AllowDelete { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether SHA-256 checksums are used for file comparison.
    /// When true, file contents are hashed for accurate comparison. When false, only size and timestamp are compared. Default is true.
    /// </summary>
    /// <remarks>
    /// Disabling checksums improves performance but may miss changes if files have identical size and timestamp.
    /// </remarks>
    public bool UseChecksum { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether timestamp comparison is used to short-circuit checksum computation.
    /// When true, files with identical size and LastWriteTimeUtc (within tolerance) are considered identical. Default is true.
    /// </summary>
    /// <remarks>
    /// This optimization significantly improves performance when files haven't changed.
    /// Timestamp tolerance is configured separately in <see cref="FileScanner"/>.
    /// </remarks>
    public bool UseTimestampOptimization { get; init; } = true;
}

/// <summary>
/// Provides real-time progress information during synchronization operations.
/// </summary>
/// <param name="TotalTasks">Total number of synchronization tasks to execute.</param>
/// <param name="CompletedTasks">Number of tasks completed so far (0 to <paramref name="TotalTasks"/>).</param>
/// <param name="TotalBytes">Total number of bytes to copy across all copy and update operations.</param>
/// <param name="ProcessedBytes">Number of bytes copied so far (0 to <paramref name="TotalBytes"/>).</param>
/// <param name="CurrentOperation">Description of the current operation being performed (e.g., "CopyFile", "DeleteDirectory", "Complete").</param>
/// <param name="CurrentFile">Path of the file or directory currently being processed, or null if not applicable.</param>
/// <remarks>
/// This record is immutable and safe to use across threads via <see cref="IProgress{T}"/>.
/// </remarks>
public record SyncProgress(
    int TotalTasks,
    int CompletedTasks,
    long TotalBytes,
    long ProcessedBytes,
    string CurrentOperation,
    string? CurrentFile
)
{
    /// <summary>
    /// Gets the task completion percentage (0-100). Returns 0 if <see cref="TotalTasks"/> is 0.
    /// </summary>
    public double PercentComplete => TotalTasks > 0 ? (double)CompletedTasks / TotalTasks * 100 : 0;

    /// <summary>
    /// Gets the byte processing completion percentage (0-100). Returns 0 if <see cref="TotalBytes"/> is 0.
    /// </summary>
    public double BytesPercentComplete => TotalBytes > 0 ? (double)ProcessedBytes / TotalBytes * 100 : 0;
}
