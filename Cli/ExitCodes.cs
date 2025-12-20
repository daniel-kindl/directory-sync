namespace DirectorySync.Cli;

/// <summary>
/// Defines exit codes returned by the directory synchronization CLI application.
/// </summary>
/// <remarks>
/// Follows Unix conventions where 0 indicates success and non-zero indicates failure.
/// Exit code 130 follows the Bash convention for SIGINT (Ctrl+C).
/// </remarks>
public static class ExitCodes
{
    /// <summary>
    /// Operation completed successfully with no errors. Value: 0.
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// General error such as invalid arguments or configuration issues. Value: 1.
    /// </summary>
    public const int GeneralError = 1;

    /// <summary>
    /// Path validation error, such as source and replica being the same or nested paths. Value: 2.
    /// </summary>
    public const int PathValidationError = 2;

    /// <summary>
    /// Source or replica directory not found. Value: 3.
    /// </summary>
    public const int DirectoryNotFound = 3;

    /// <summary>
    /// Permission denied or unauthorized access to files or directories. Value: 4.
    /// </summary>
    public const int PermissionDenied = 4;

    /// <summary>
    /// Insufficient disk space on the replica drive. Value: 5.
    /// </summary>
    public const int InsufficientDiskSpace = 5;

    /// <summary>
    /// Partial synchronization failure where some operations succeeded but others failed. Value: 10.
    /// Check <see cref="Engine.SyncResult.Failures"/> for details.
    /// </summary>
    public const int PartialFailure = 10;

    /// <summary>
    /// Verification failed: replica does not match source. Value: 20.
    /// </summary>
    public const int VerifyMismatch = 20;

    /// <summary>
    /// Operation cancelled by user (Ctrl+C or <see cref="CancellationToken"/>). Value: 130.
    /// Follows Bash convention for SIGINT (128 + 2).
    /// </summary>
    public const int Cancelled = 130;
}
