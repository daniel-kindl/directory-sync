using System.Security;

namespace DirectorySync.Engine;

/// <summary>
/// Centralized path validation to prevent directory traversal attacks and ensure path safety.
/// </summary>
/// <remarks>
/// <para>This static class validates that target paths stay within expected base directories.</para>
/// <para>Protects against directory traversal attacks using ".." sequences or absolute paths that escape the base directory.</para>
/// <para>All validation uses normalized absolute paths and case-insensitive comparison for compatibility.</para>
/// </remarks>
public static class PathValidator
{
    private static ILogger? _logger;

    /// <summary>
    /// Sets the logger for path validation operations.
    /// </summary>
    /// <param name="logger">The logger to use for security warnings, or null to disable logging.</param>
    /// <remarks>
    /// This is a static setter because <see cref="PathValidator"/> is a static utility class.
    /// Thread-safety: caller must ensure this is set before concurrent validation calls, or accept potential race conditions.
    /// </remarks>
    public static void SetLogger(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates that a target path is within the specified base path.
    /// </summary>
    /// <param name="basePath">The base directory path that must contain the target. Should be an absolute path.</param>
    /// <param name="targetPath">The path to validate (can be relative or absolute). Must resolve to within <paramref name="basePath"/>.</param>
    /// <exception cref="SecurityException">Thrown if <paramref name="targetPath"/> resolves to a location outside <paramref name="basePath"/>, indicating a potential directory traversal attack.</exception>
    /// <remarks>
    /// <para><strong>Validation Logic:</strong></para>
    /// <list type="number">
    /// <item>If <paramref name="basePath"/> or <paramref name="targetPath"/> is null/empty, validation is skipped (considered safe).</item>
    /// <item>Both paths are normalized to absolute paths using <see cref="Path.GetFullPath(string)"/>.</item>
    /// <item>A directory separator is appended to <paramref name="basePath"/> if missing, to prevent false positives (e.g., "C:\foo" vs. "C:\foobar").</item>
    /// <item>Case-insensitive comparison is used for cross-platform filesystem compatibility.</item>
    /// <item>If <paramref name="targetPath"/> does not start with <paramref name="basePath"/>, a <see cref="SecurityException"/> is thrown and logged.</item>
    /// </list>
    /// <para><strong>Security:</strong></para>
    /// This method is critical for preventing path traversal attacks where malicious relative paths
    /// could escape the intended base directory. Always call before file system operations on user-controlled or externally-sourced paths.
    /// <para><strong>Thread-Safety:</strong></para>
    /// This method is thread-safe. The static <see cref="_logger"/> field may be accessed concurrently; ensure <see cref="SetLogger"/>
    /// is called before concurrent usage or accept potential logging race conditions.
    /// </remarks>
    public static void ValidatePathSafety(string basePath, string targetPath)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            return; // Cannot validate without base path
        }

        if (string.IsNullOrEmpty(targetPath))
        {
            return; // Empty path is safe
        }

        // Normalize both paths to absolute paths for comparison
        string normalizedTarget = Path.GetFullPath(targetPath);
        string normalizedBase = Path.GetFullPath(basePath);

        // Ensure normalized base ends with directory separator for accurate comparison
        if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar))
        {
            normalizedBase += Path.DirectorySeparatorChar;
        }

        // Check if target path is within base path
        // Use OrdinalIgnoreCase for cross-platform compatibility (case-insensitive comparison)
        if (normalizedTarget.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            return;
        _logger?.LogError(null,
            "Path traversal detected - Target: {Target}, Base: {Base}, NormalizedTarget: {NormalizedTarget}, NormalizedBase: {NormalizedBase}",
            targetPath, basePath, normalizedTarget, normalizedBase);

        throw new SecurityException(
            $"Path traversal detected: '{targetPath}' is outside base path '{basePath}'");
    }

    /// <summary>
    /// Validates that a relative path combined with a base path stays within the base directory.
    /// </summary>
    /// <param name="basePath">The base directory path (should be absolute).</param>
    /// <param name="relativePath">The relative path to validate. Must not traverse outside <paramref name="basePath"/> when combined.</param>
    /// <exception cref="SecurityException">Thrown if the combined path resolves outside <paramref name="basePath"/>.</exception>
    /// <remarks>
    /// This is a convenience method that combines <paramref name="basePath"/> and <paramref name="relativePath"/> using <see cref="Path.Combine(string, string)"/>,
    /// then validates the result with <see cref="ValidatePathSafety"/>.
    /// Useful when you have a base path and a relative path that will be combined (e.g., from directory scanning results).
    /// Empty <paramref name="relativePath"/> is considered safe and validation is skipped.
    /// </remarks>
    public static void ValidateRelativePathSafety(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return; // Empty path is safe
        }

        // Combine and validate the result
        string combinedPath = Path.Combine(basePath, relativePath);
        ValidatePathSafety(basePath, combinedPath);
    }
}
