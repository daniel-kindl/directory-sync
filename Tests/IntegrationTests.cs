using DirectorySync.Engine;
using FluentAssertions;

namespace DirectorySync.Tests;

public class IntegrationTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _replicaDir;
    private readonly SyncEngine _engine;

    public IntegrationTests()
    {
        string basePath = Path.Combine(Path.GetTempPath(), $"dirsync-integration-{Guid.NewGuid()}");
        _sourceDir = Path.Combine(basePath, "source");
        _replicaDir = Path.Combine(basePath, "replica");

        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_replicaDir);

        _engine = new SyncEngine();
    }

    public void Dispose()
    {
        if (Directory.Exists(_sourceDir))
        {
            Directory.Delete(_sourceDir, recursive: true);
        }

        if (Directory.Exists(_replicaDir))
        {
            Directory.Delete(_replicaDir, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Sync_ShouldCreateNewFiles()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_sourceDir, "file1.txt"), "content1");
        File.WriteAllText(Path.Combine(_sourceDir, "file2.txt"), "content2");

        SyncOptions options = new()
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            AllowDelete = true,
            UseChecksum = true,
            DryRun = false
        };

        // Act
        SyncResult result = _engine.Sync(options);

        // Assert
        result.Success.Should().BeTrue();
        result.Created.Should().Be(2);
        File.Exists(Path.Combine(_replicaDir, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(_replicaDir, "file2.txt")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_replicaDir, "file1.txt")).Should().Be("content1");
    }

    [Fact]
    public void Sync_ShouldCreateDirectories()
    {
        // Arrange
        string subDir = Path.Combine(_sourceDir, "subdir");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "nested.txt"), "nested content");

        SyncOptions options = new()
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            AllowDelete = true,
            UseChecksum = true,
            DryRun = false
        };

        // Act
        SyncResult result = _engine.Sync(options);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(Path.Combine(_replicaDir, "subdir")).Should().BeTrue();
        File.Exists(Path.Combine(_replicaDir, "subdir", "nested.txt")).Should().BeTrue();
    }

    [Fact]
    public void Sync_ShouldUpdateModifiedFiles()
    {
        // Arrange - Initial sync
        File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "original");
        SyncOptions options = new()
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            AllowDelete = true,
            UseChecksum = true,
            DryRun = false
        };
        _engine.Sync(options);

        // Modify source file
        File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "modified");

        // Act - Second sync
        SyncResult result = _engine.Sync(options);

        // Assert
        result.Updated.Should().Be(1);
        File.ReadAllText(Path.Combine(_replicaDir, "file.txt")).Should().Be("modified");
    }

    [Fact]
    public void Sync_ShouldDeleteRemovedFiles_WhenAllowDeleteIsTrue()
    {
        // Arrange - Create file in replica
        File.WriteAllText(Path.Combine(_replicaDir, "old-file.txt"), "old content");

        SyncOptions options = new()
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            AllowDelete = true,
            UseChecksum = true,
            DryRun = false
        };

        // Act
        SyncResult result = _engine.Sync(options);

        // Assert
        result.Deleted.Should().Be(1);
        File.Exists(Path.Combine(_replicaDir, "old-file.txt")).Should().BeFalse();
    }

    [Fact]
    public void Sync_ShouldNotDelete_WhenAllowDeleteIsFalse()
    {
        // Arrange - Create file in replica
        File.WriteAllText(Path.Combine(_replicaDir, "old-file.txt"), "old content");

        SyncOptions options = new()
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            AllowDelete = false,
            UseChecksum = true,
            DryRun = false
        };

        // Act
        SyncResult result = _engine.Sync(options);

        // Assert
        result.Deleted.Should().Be(0);
        File.Exists(Path.Combine(_replicaDir, "old-file.txt")).Should().BeTrue();
    }

    [Fact]
    public void Sync_WithDryRun_ShouldNotMakeChanges()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "content");

        SyncOptions options = new()
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            AllowDelete = true,
            UseChecksum = true,
            DryRun = true
        };

        // Act
        SyncResult result = _engine.Sync(options);

        // Assert
        result.Created.Should().Be(1); // Count shows what would be done
        File.Exists(Path.Combine(_replicaDir, "file.txt")).Should().BeFalse(); // But not actually done
    }

    [Fact]
    public void Verify_ShouldReturnTrue_WhenReplicaMatchesSource()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "content");

        SyncOptions options = new()
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            AllowDelete = true,
            UseChecksum = true,
            DryRun = false
        };
        _engine.Sync(options);

        // Act
        bool matches = _engine.Verify(_sourceDir, _replicaDir);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact]
    public void Verify_ShouldReturnFalse_WhenReplicaDiffers()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_sourceDir, "file.txt"), "content");
        File.WriteAllText(Path.Combine(_replicaDir, "different.txt"), "different");

        // Act
        bool matches = _engine.Verify(_sourceDir, _replicaDir);

        // Assert
        matches.Should().BeFalse();
    }
}
