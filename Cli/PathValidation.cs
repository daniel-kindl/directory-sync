namespace DirectorySync.Cli;

/// <summary>
/// Path validation utilities for CLI input sanitization and early error detection.
/// </summary>
/// <remarks>
/// Validates paths, intervals, and tolerances before they reach the engine layer.
/// All validation methods are static and thread-safe.
/// </remarks>
public static class PathValidation
{
    // Windows MAX_PATH limitation (can be longer with \\?\ prefix but conservative for compatibility)
    private const int _maxPathLength = 260;
    private const int _maxFileNameLength = 255;

    // Invalid path characters
    private static readonly char[] _invalidPathChars = Path.GetInvalidPathChars();
    private static readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Represents a validation error with an optional message.
    /// </summary>
    /// <param name="Message">Human-readable error message describing the validation failure, or null.</param>
    public record ValidationError(string? Message);

    /// <summary>
    /// Validates a path for common issues and security concerns.
    /// </summary>
    /// <param name="path">The path to validate (maybe null or whitespace).</param>
    /// <param name="parameterName">The name of the parameter being validated (used in error messages).</param>
    /// <returns>
    /// Null if the path is valid; a <see cref="ValidationError"/> with a descriptive message if validation fails.
    /// </returns>
    /// <remarks>
    /// <para><strong>Validation Checks:</strong></para>
    /// <list type="bullet">
    /// <item>Path must not be null, empty, or whitespace.</item>
    /// <item>Path length must not exceed 260 characters (Windows MAX_PATH).</item>
    /// <item>Path must not contain invalid characters (from <see cref="Path.GetInvalidPathChars"/>).</item>
    /// <item>Path must not contain suspicious traversal patterns (three or more consecutive ".." sequences).</item>
    /// <item>Path must be an absolute (rooted) path.</item>
    /// </list>
    /// <para>This method does not check if the path exists on disk.</para>
    /// </remarks>
    public static ValidationError? ValidatePath(string? path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ValidationError($"{parameterName} is required");
        }

        // Check length
        if (path.Length > _maxPathLength)
        {
            return new ValidationError(
                $"{parameterName} exceeds maximum length of {_maxPathLength} characters (actual: {path.Length})");
        }

        // Check for invalid characters
        if (path.IndexOfAny(_invalidPathChars) >= 0)
        {
            return new ValidationError($"{parameterName} contains invalid characters");
        }

        // Check for suspicious patterns
        if (path.Contains("..\\..\\..") || path.Contains("../../.."))
        {
            return new ValidationError($"{parameterName} contains suspicious path traversal pattern");
        }

        // Check if path is rooted (absolute)
        if (!Path.IsPathRooted(path))
        {
            return new ValidationError($"{parameterName} must be an absolute path");
        }

        return null; // Valid
    }

    /// <summary>
    /// Validates a timestamp tolerance parameter for file comparison.
    /// </summary>
    /// <param name="seconds">The timestamp tolerance in seconds.</param>
    /// <returns>
    /// Null if the tolerance is valid; a <see cref="ValidationError"/> with a descriptive message if validation fails.
    /// </returns>
    /// <remarks>
    /// <para><strong>Validation Rules:</strong></para>
    /// <list type="bullet">
    /// <item>Tolerance must be non-negative (0 or greater).</item>
    /// <item>Tolerance must not exceed 3600 seconds (1 hour).</item>
    /// </list>
    /// <para>Common values: 0 (exact match), 2 (FAT32 compatibility, default), 1 (NTFS granularity).</para>
    /// </remarks>
    public static ValidationError? ValidateTimestampTolerance(double seconds)
    {
        return seconds switch
        {
            < 0 => new ValidationError("Timestamp tolerance cannot be negative"),
            // 1 hour max
            > 3600 => new ValidationError("Timestamp tolerance cannot exceed 3600 seconds (1 hour)"),
            _ => null
        };
    }

    /// <summary>
    /// Validates an interval parameter for daemon mode synchronization.
    /// </summary>
    /// <param name="seconds">The synchronization interval in seconds.</param>
    /// <returns>
    /// Null if the interval is valid; a <see cref="ValidationError"/> with a descriptive message if validation fails.
    /// </returns>
    /// <remarks>
    /// <para><strong>Validation Rules:</strong></para>
    /// <list type="bullet">
    /// <item>Interval must be at least 1 second (prevents excessive CPU usage).</item>
    /// <item>Interval must not exceed 86400 seconds (24 hours) (prevents misconfiguration).</item>
    /// </list>
    /// <para>Typical values: 60 (1 minute, default), 300 (5 minutes), 3600 (1 hour).</para>
    /// </remarks>
    public static ValidationError? ValidateInterval(int seconds)
    {
        return seconds switch
        {
            < 1 => new ValidationError("Interval must be at least 1 second"),
            // 24 hours max
            > 86400 => new ValidationError("Interval cannot exceed 86400 seconds (24 hours)"),
            _ => null
        };
    }
}
