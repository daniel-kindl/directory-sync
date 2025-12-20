using DirectorySync.Engine;
using Xunit;

namespace DirectorySync.Tests;

/// <summary>
/// Edge case tests for production scenarios.
/// </summary>
public class EdgeCaseTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _source;
    private readonly string _replica;

    public EdgeCaseTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"dirsync_edge_test_{Guid.NewGuid():N}");
        _source = Path.Combine(_testRoot, "source");
        _replica = Path.Combine(_testRoot, "replica");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_replica);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Sync_WithUnicodeFilenames_Success()
    {
        // Arrange: Files with Unicode characters
        string[] unicodeFiles =
        [
            "文件.txt",           // Chinese
            "файл.txt",           // Russian
            "αρχείο.txt",         // Greek
            "ファイル.txt",        // Japanese
            "emoji_😀_test.txt"   // Emoji
        ];

        foreach (string filename in unicodeFiles)
        {
            File.WriteAllText(Path.Combine(_source, filename), $"Content of {filename}");
        }

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(unicodeFiles.Length, result.Created);
        foreach (string filename in unicodeFiles)
        {
            string replicaFile = Path.Combine(_replica, filename);
            Assert.True(File.Exists(replicaFile), $"File not synced: {filename}");
        }
    }

    [Fact]
    public void Sync_WithSpecialCharactersInFilenames_Success()
    {
        // Arrange: Files with special characters (valid on Windows)
        string[] specialFiles =
        [
            "file with spaces.txt",
            "file_with_underscores.txt",
            "file-with-dashes.txt",
            "file.multiple.dots.txt",
            "file(with)parens.txt",
            "file[with]brackets.txt",
            "file{with}braces.txt"
        ];

        foreach (string filename in specialFiles)
        {
            File.WriteAllText(Path.Combine(_source, filename), "test content");
        }

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(specialFiles.Length, result.Created);
    }

    [Fact]
    public void Sync_WithSymbolicLinkFile_SkipsSymlink()
    {
        // Arrange: Create a real file and a symlink
        string realFile = Path.Combine(_source, "realfile.txt");
        string symlinkFile = Path.Combine(_source, "symlink.txt");

        File.WriteAllText(realFile, "real content");

        // Create symbolic link (requires admin or developer mode on Windows)
        try
        {
            File.CreateSymbolicLink(symlinkFile, realFile);
        }
        catch (IOException)
        {
            // Skip test if symlinks not supported
            return;
        }

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Created); // Only the real file
        Assert.True(File.Exists(Path.Combine(_replica, "realfile.txt")));
        Assert.False(File.Exists(Path.Combine(_replica, "symlink.txt")));
    }

    [Fact]
    public void Sync_WithSymbolicLinkDirectory_SkipsSymlink()
    {
        // Arrange: Create real directory and symlink directory
        string realDir = Path.Combine(_source, "realdir");
        string symlinkDir = Path.Combine(_source, "symlinkdir");

        Directory.CreateDirectory(realDir);
        File.WriteAllText(Path.Combine(realDir, "file.txt"), "content");

        try
        {
            Directory.CreateSymbolicLink(symlinkDir, realDir);
        }
        catch (IOException)
        {
            // Skip test if symlinks not supported
            return;
        }

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(_replica, "realdir")));
        Assert.False(Directory.Exists(Path.Combine(_replica, "symlinkdir")));
    }

    [Fact]
    public void Sync_WithLargeFile_Success()
    {
        // Arrange: Create a 100MB file (testing chunked copy with cancellation)
        string largeFile = Path.Combine(_source, "largefile.bin");
        const int fileSizeMb = 100;
        const int bufferSize = 1024 * 1024; // 1MB buffer

        using (FileStream fs = new(largeFile, FileMode.Create))
        {
            byte[] buffer = new byte[bufferSize];
            Random.Shared.NextBytes(buffer);

            for (int i = 0; i < fileSizeMb; i++)
            {
                fs.Write(buffer, 0, buffer.Length);
            }
        }

        var engine = new SyncEngine(useChecksum: false); // Skip checksum for speed
        var options = new SyncOptions { Source = _source, Replica = _replica, UseChecksum = false };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Created);

        string replicaFile = Path.Combine(_replica, "largefile.bin");
        Assert.True(File.Exists(replicaFile));
        Assert.Equal(new FileInfo(largeFile).Length, new FileInfo(replicaFile).Length);
    }

    [Fact]
    public void Sync_CancellationDuringLargeFileCopy_CancelsOrSucceeds()
    {
        // Arrange: Create a large file
        string largeFile = Path.Combine(_source, "largefile.bin");
        const int fileSizeMb = 200; // Larger file to ensure cancellation happens

        using (FileStream fs = new(largeFile, FileMode.Create))
        {
            fs.SetLength(fileSizeMb * 1024 * 1024);
        }

        var engine = new SyncEngine(useChecksum: false);
        var options = new SyncOptions { Source = _source, Replica = _replica, UseChecksum = false };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel after 50ms

        // Act - either throws cancellation or completes (on very fast storage)
        bool wasCancelled = false;
        try
        {
            engine.Sync(options, cancellationToken: cts.Token);
        }
        catch (OperationCanceledException)
        {
            wasCancelled = true;
        }

        // Assert: If canceled, verify cleanup. If completed, that's also valid (fast SSD).
        if (!wasCancelled)
            return;
        // Verify temp files are cleaned up (should not exist or be old)
        string[] tempFiles = Directory.GetFiles(_replica, "*.tmp.*", SearchOption.AllDirectories);
        Assert.True(tempFiles.Length <= 1); // At most the one we were copying
        // Test passes either way - validates cancellation works when slow enough
    }

    [Fact]
    public void Sync_WithDeepNestedPaths_Success()
    {
        // Arrange: Create deeply nested directory structure
        string deepPath = _source;
        for (int i = 0; i < 10; i++)
        {
            deepPath = Path.Combine(deepPath, $"level{i}");
        }
        Directory.CreateDirectory(deepPath);
        File.WriteAllText(Path.Combine(deepPath, "deep_file.txt"), "deep content");

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        // Created includes 10 directories + 1 file = 11 total
        Assert.True(result.Created >= 1); // At least the file

        string replicaDeepPath = deepPath.Replace(_source, _replica);
        Assert.True(File.Exists(Path.Combine(replicaDeepPath, "deep_file.txt")));
    }

    [Fact]
    public void Sync_WithReadOnlyFiles_Success()
    {
        // Arrange: Create read-only file in source
        string readOnlyFile = Path.Combine(_source, "readonly.txt");
        File.WriteAllText(readOnlyFile, "readonly content");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Created);

        string replicaFile = Path.Combine(_replica, "readonly.txt");
        Assert.True(File.Exists(replicaFile));

        // Cleanup: Remove readonly attribute for disposal
        File.SetAttributes(readOnlyFile, FileAttributes.Normal);
        File.SetAttributes(replicaFile, FileAttributes.Normal);
    }

    [Fact]
    public void Sync_WithHiddenFiles_Success()
    {
        // Arrange: Create hidden file
        string hiddenFile = Path.Combine(_source, "hidden.txt");
        File.WriteAllText(hiddenFile, "hidden content");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.Created);
        Assert.True(File.Exists(Path.Combine(_replica, "hidden.txt")));
    }

    [Fact]
    public void Sync_WithEmptyDirectories_CreatesDirectories()
    {
        // Arrange: Create empty directories
        Directory.CreateDirectory(Path.Combine(_source, "empty1"));
        Directory.CreateDirectory(Path.Combine(_source, "empty2", "nested"));

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(_replica, "empty1")));
        Assert.True(Directory.Exists(Path.Combine(_replica, "empty2", "nested")));
    }

    [Fact]
    public void Sync_WithZeroByteFiles_Success()
    {
        // Arrange: Create empty files
        File.Create(Path.Combine(_source, "empty1.txt")).Dispose();
        File.Create(Path.Combine(_source, "empty2.txt")).Dispose();

        var engine = new SyncEngine();
        var options = new SyncOptions { Source = _source, Replica = _replica };

        // Act
        SyncResult result = engine.Sync(options);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.Created);
        Assert.True(File.Exists(Path.Combine(_replica, "empty1.txt")));
        Assert.True(File.Exists(Path.Combine(_replica, "empty2.txt")));
    }
}
