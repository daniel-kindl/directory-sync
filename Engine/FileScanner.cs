using System.Security;
using System.Security.Cryptography;

namespace DirectorySync.Engine;

/// <summary>
/// Scans directories and computes file metadata for synchronization.
/// This class is stateless and does not hold disposable resources.
/// All file streams and hash algorithms are properly disposed using 'using' statements.
/// </summary>
public class FileScanner
{
    private readonly bool _useChecksum;
    private readonly bool _useTimestampOptimization;
    private readonly ILogger _logger;
    private readonly TimeSpan _timestampTolerance;

    private const int _defaultTimestampToleranceSeconds = 2; // FAT32 compatibility
    private const int _hashTimeoutMinutes = 5;
    private const int _hashReadBufferSize = 4096;
    private const int _hashChunkSize = 4096 * 256; // 1MB chunks for memory efficiency

    /// <summary>
    /// Initializes a new instance of the <see cref="FileScanner"/> class with the specified options.
    /// </summary>
    /// <param name="useChecksum">If true, computes SHA-256 checksums for file comparison. If false, only size and timestamp are used. Default is true.</param>
    /// <param name="useTimestampOptimization">If true, files with identical size and timestamp (within tolerance) are considered identical without computing checksums. Default is true.</param>
    /// <param name="logger">Optional logger for diagnostic output. If null, a <see cref="NullLogger"/> is used.</param>
    /// <param name="timestampTolerance">Tolerance for timestamp comparisons. If null, defaults to 2 seconds for FAT32 filesystem compatibility.</param>
    /// <remarks>
    /// The default timestamp tolerance of 2 seconds accommodates FAT32 filesystems which have 2-second timestamp granularity.
    /// Hash computation uses 1MB chunks to balance memory efficiency with performance.
    /// </remarks>
    public FileScanner(bool useChecksum = true, bool useTimestampOptimization = true,
                      ILogger? logger = null, TimeSpan? timestampTolerance = null)
    {
        _useChecksum = useChecksum;
        _useTimestampOptimization = useTimestampOptimization;
        _logger = logger ?? new NullLogger();
        _timestampTolerance = timestampTolerance ?? TimeSpan.FromSeconds(_defaultTimestampToleranceSeconds);
    }

    /// <summary>
    /// Scans a directory recursively and returns metadata for all files and directories.
    /// </summary>
    /// <param name="directoryPath">The absolute or relative path to the directory to scan.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests. Cancellation is checked between processing each file and directory.</param>
    /// <returns>
    /// A dictionary mapping relative paths to <see cref="SyncItem"/> metadata. Keys are relative paths from <paramref name="directoryPath"/>.
    /// Returns an empty dictionary if the directory does not exist.
    /// </returns>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to a file or directory is denied.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during scanning.</exception>
    /// <exception cref="SecurityException">Thrown when a path traversal attempt is detected.</exception>
    /// <exception cref="TimeoutException">Thrown when hash computation takes longer than 5 minutes for a single file.</exception>
    /// <remarks>
    /// Symbolic links (reparse points) are skipped to prevent infinite loops and unexpected behavior.
    /// If <paramref name="directoryPath"/> does not exist, a warning is logged and an empty dictionary is returned (no exception is thrown).
    /// Hash computation is subject to a 5-minute timeout per file to prevent hangs on network issues or extremely large files.
    /// The method validates all relative paths to prevent directory traversal attacks.
    /// </remarks>
    public Dictionary<string, SyncItem> ScanDirectory(string directoryPath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting directory scan: {Directory}", directoryPath);

        Dictionary<string, SyncItem> directoryItems = new();

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory does not exist: {Directory}", directoryPath);
            return directoryItems;
        }

        string normalizedBase = Path.GetFullPath(directoryPath);
        int dirCount = 0;
        int fileCount = 0;
        int symlinkDirCount = 0;
        int symlinkFileCount = 0;

        // Use manual recursion to properly skip symlinks before traversing them
        ScanDirectoryRecursive(directoryPath, directoryPath, directoryItems, normalizedBase,
            ref dirCount, ref fileCount, ref symlinkDirCount, ref symlinkFileCount, cancellationToken);

        _logger.LogInformation("Scanned {Directory} - Found {DirCount} directories, {FileCount} files" +
            (symlinkDirCount > 0 || symlinkFileCount > 0 ? " (Skipped {SymlinkDirCount} symlink dirs, {SymlinkFileCount} symlink files)" : ""),
            directoryPath, dirCount, fileCount, symlinkDirCount, symlinkFileCount);

        return directoryItems;
    }

    private void ScanDirectoryRecursive(string basePath, string currentPath, Dictionary<string, SyncItem> items,
        string normalizedBase, ref int dirCount, ref int fileCount, ref int symlinkDirCount, ref int symlinkFileCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Scan directories in current level
        foreach (string dir in Directory.GetDirectories(currentPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Check if this directory is a symbolic link BEFORE recursing into it
            DirectoryInfo dirInfo = new(dir);
            if (dirInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                _logger.LogDebug("Skipping symbolic link directory: {Path}", dir);
                symlinkDirCount++;
                continue; // Don't recurse into symlinks
            }

            string relDirPath = Path.GetRelativePath(basePath, dir);
            PathValidator.ValidateRelativePathSafety(normalizedBase, relDirPath);
            items.TryAdd(relDirPath, new SyncItem(true, string.Empty, 0, DateTime.MinValue));
            dirCount++;

            // Recurse into this directory (it's not a symlink)
            ScanDirectoryRecursive(basePath, dir, items, normalizedBase,
                ref dirCount, ref fileCount, ref symlinkDirCount, ref symlinkFileCount, cancellationToken);
        }

        // Scan files in current level
        foreach (string file in Directory.GetFiles(currentPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Skip symbolic link files
            FileInfo fileInfo = new(file);
            if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                _logger.LogDebug("Skipping symbolic link file: {Path}", file);
                symlinkFileCount++;
                continue;
            }

            string relFilePath = Path.GetRelativePath(basePath, file);
            PathValidator.ValidateRelativePathSafety(normalizedBase, relFilePath);

            string fileHash = _useChecksum ? ComputeFileHash(fileInfo.FullName, cancellationToken) : string.Empty;
            items.TryAdd(relFilePath,
                new SyncItem(false, fileHash, fileInfo.Length, fileInfo.LastWriteTimeUtc));
            fileCount++;
        }
    }

    /// <summary>
    /// Compares two files to determine if they have identical content.
    /// </summary>
    /// <param name="file1">The full path to the first file.</param>
    /// <param name="file2">The full path to the second file.</param>
    /// <returns>
    /// True if the files are considered identical; false otherwise.
    /// Files are identical if they have the same size and, depending on configuration, the same timestamp or checksum.
    /// </returns>
    /// <remarks>
    /// Comparison strategy depends on instance configuration:
    /// <list type="number">
    /// <item>If sizes differ, returns false immediately.</item>
    /// <item>If <see cref="_useTimestampOptimization"/> is true and timestamps match within tolerance, returns true.</item>
    /// <item>If <see cref="_useChecksum"/> is false, returns false (indicating uncertainty).</item>
    /// <item>Otherwise, computes and compares SHA-256 checksums.</item>
    /// </list>
    /// This method is synchronous and may block for large files when computing checksums.
    /// Does not throw exceptions; file access errors propagate to caller.
    /// </remarks>
    public bool AreFilesIdentical(string file1, string file2)
    {
        FileInfo f1 = new(file1);
        FileInfo f2 = new(file2);

        if (f1.Length != f2.Length)
        {
            return false;
        }

        if (_useTimestampOptimization)
        {
            // Use configurable tolerance (default 2s for FAT32 compatibility)
            TimeSpan difference = (f1.LastWriteTimeUtc - f2.LastWriteTimeUtc).Duration();

            if (difference <= _timestampTolerance)
            {
                return true;
            }
        }

        if (!_useChecksum)
        {
            return false;
        }

        return ComputeFileHash(file1) == ComputeFileHash(file2);
    }

    private static string ComputeFileHash(string filePath, CancellationToken cancellationToken = default)
    {
        // Add timeout protection to prevent hanging on network issues or huge files
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(_hashTimeoutMinutes));

        try
        {
            return ComputeFileHashCore(filePath, cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Hash computation cancelled or timed out for file: {filePath}");
        }
    }

    private static string ComputeFileHashCore(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: _hashReadBufferSize, useAsync: false);

        // Check cancellation before starting
        cancellationToken.ThrowIfCancellationRequested();

        // For large files, compute hash in chunks and check cancellation periodically
        byte[] buffer = new byte[_hashChunkSize];
        int bytesRead;

        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }

        sha256.TransformFinalBlock([], 0, 0);

        if (sha256.Hash == null)
        {
            throw new InvalidOperationException("Failed to compute hash");
        }

        return Convert.ToHexString(sha256.Hash);
    }
}
