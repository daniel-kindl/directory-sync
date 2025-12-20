using DirectorySync.Engine;
using FluentAssertions;

namespace DirectorySync.Tests;

public class FileScannerTests : IDisposable
{
    private readonly string _testDir;

    public FileScannerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"dirsync-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ScanDirectory_ShouldReturnEmpty_WhenDirectoryDoesNotExist()
    {
        // Arrange
        FileScanner scanner = new();
        string nonExistentPath = Path.Combine(_testDir, "nonexistent");

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(nonExistentPath);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ScanDirectory_ShouldFindFiles()
    {
        // Arrange
        FileScanner scanner = new();
        string testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(_testDir);

        // Assert
        result.Should().ContainKey("test.txt");
        result["test.txt"].IsDir.Should().BeFalse();
        result["test.txt"].ItemHash.Should().NotBeEmpty();
    }

    [Fact]
    public void ScanDirectory_ShouldFindDirectories()
    {
        // Arrange
        FileScanner scanner = new();
        string subDir = Path.Combine(_testDir, "subdir");
        Directory.CreateDirectory(subDir);

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(_testDir);

        // Assert
        result.Should().ContainKey("subdir");
        result["subdir"].IsDir.Should().BeTrue();
    }

    [Fact]
    public void ScanDirectory_ShouldFindNestedStructure()
    {
        // Arrange
        FileScanner scanner = new();
        string subDir = Path.Combine(_testDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested content");

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(_testDir);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainKey("subdir");
        result.Should().ContainKey(Path.Combine("subdir", "nested.txt"));
    }

    [Fact]
    public void ScanDirectory_WithoutChecksum_ShouldReturnEmptyHash()
    {
        // Arrange
        FileScanner scanner = new(useChecksum: false);
        string testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(_testDir);

        // Assert
        result["test.txt"].ItemHash.Should().BeEmpty();
    }

    [Fact]
    public void ScanDirectory_WithChecksum_ShouldReturnHash()
    {
        // Arrange
        FileScanner scanner = new(useChecksum: true);
        string testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test content");

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(_testDir);

        // Assert
        result["test.txt"].ItemHash.Should().NotBeEmpty();
        result["test.txt"].ItemHash.Should().HaveLength(64); // SHA-256 hex string
    }

    [Fact]
    public void ScanDirectory_ShouldIncludeFileSize()
    {
        // Arrange
        FileScanner scanner = new();
        string testFile = Path.Combine(_testDir, "test.txt");
        string content = "test content";
        File.WriteAllText(testFile, content);
        long expectedSize = new FileInfo(testFile).Length;

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(_testDir);

        // Assert
        result["test.txt"].Size.Should().Be(expectedSize);
        result["test.txt"].Size.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ScanDirectory_ShouldIncludeLastWriteTime()
    {
        // Arrange
        FileScanner scanner = new();
        string testFile = Path.Combine(_testDir, "test.txt");
        File.WriteAllText(testFile, "test content");
        DateTime expectedTime = File.GetLastWriteTimeUtc(testFile);

        // Act
        Dictionary<string, SyncItem> result = scanner.ScanDirectory(_testDir);

        // Assert
        result["test.txt"].LastWriteUtc.Should().BeCloseTo(expectedTime, TimeSpan.FromSeconds(1));
    }
}
