using DirectorySync.Engine;
using FluentAssertions;

namespace DirectorySync.Tests;

public class SyncPlannerTests
{
    [Fact]
    public void GetSyncTasks_ShouldCreateDirectory_WhenMissingInReplica()
    {
        // Arrange
        Dictionary<string, SyncItem> sourceItems = new()
        {
            { "subdir", new SyncItem(true, string.Empty, 0, DateTime.MinValue) }
        };
        Dictionary<string, SyncItem> replicaItems = [];

        string sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        string replicaPath = Path.Combine(Path.GetTempPath(), "Replica");

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, sourcePath,
            replicaItems, replicaPath,
            allowDelete: true);

        // Assert
        tasks.Should().ContainSingle();
        tasks[0].Action.Should().Be(SyncAction.CreateDirectory);
        tasks[0].DestinationPath.Should().Be(Path.Combine(replicaPath, "subdir"));
    }

    [Fact]
    public void GetSyncTasks_ShouldCopyFile_WhenMissingInReplica()
    {
        // Arrange
        Dictionary<string, SyncItem> sourceItems = new()
        {
            { "file.txt", new SyncItem(false, "hash123", 100, DateTime.UtcNow) }
        };
        Dictionary<string, SyncItem> replicaItems = new();

        string sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        string replicaPath = Path.Combine(Path.GetTempPath(), "Replica");

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, sourcePath,
            replicaItems, replicaPath,
            allowDelete: true);

        // Assert
        tasks.Should().ContainSingle();
        tasks[0].Action.Should().Be(SyncAction.CopyFile);
        tasks[0].SourcePath.Should().Be(Path.Combine(sourcePath, "file.txt"));
        tasks[0].DestinationPath.Should().Be(Path.Combine(replicaPath, "file.txt"));
    }

    [Fact]
    public void GetSyncTasks_ShouldUpdateFile_WhenHashDiffers()
    {
        // Arrange
        Dictionary<string, SyncItem> sourceItems = new()
        {
            { "file.txt", new SyncItem(false, "hash123", 100, DateTime.UtcNow) }
        };
        Dictionary<string, SyncItem> replicaItems = new()
        {
            { "file.txt", new SyncItem(false, "hash456", 100, DateTime.UtcNow) }
        };

        string sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        string replicaPath = Path.Combine(Path.GetTempPath(), "Replica");

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, sourcePath,
            replicaItems, replicaPath,
            allowDelete: true);

        // Assert
        tasks.Should().ContainSingle();
        tasks[0].Action.Should().Be(SyncAction.UpdateFile);
    }

    [Fact]
    public void GetSyncTasks_ShouldDeleteFile_WhenNotInSource()
    {
        // Arrange
        Dictionary<string, SyncItem> sourceItems = new();
        Dictionary<string, SyncItem> replicaItems = new()
        {
            { "old-file.txt", new SyncItem(false, "hash789", 100, DateTime.UtcNow) }
        };

        string sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        string replicaPath = Path.Combine(Path.GetTempPath(), "Replica");

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, sourcePath,
            replicaItems, replicaPath,
            allowDelete: true);

        // Assert
        tasks.Should().ContainSingle();
        tasks[0].Action.Should().Be(SyncAction.DeleteFile);
        tasks[0].DestinationPath.Should().Be(Path.Combine(replicaPath, "old-file.txt"));
    }

    [Fact]
    public void GetSyncTasks_ShouldNotDelete_WhenAllowDeleteIsFalse()
    {
        // Arrange
        Dictionary<string, SyncItem> sourceItems = new();
        Dictionary<string, SyncItem> replicaItems = new()
        {
            { "old-file.txt", new SyncItem(false, "hash789", 100, DateTime.UtcNow) }
        };

        string sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        string replicaPath = Path.Combine(Path.GetTempPath(), "Replica");

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, sourcePath,
            replicaItems, replicaPath,
            allowDelete: false);

        // Assert
        tasks.Should().BeEmpty();
    }

    [Fact]
    public void GetSyncTasks_ShouldReturnEmpty_WhenSourceAndReplicaMatch()
    {
        // Arrange
        Dictionary<string, SyncItem> sourceItems = new()
        {
            { "file.txt", new SyncItem(false, "hash123", 100, DateTime.UtcNow) }
        };
        Dictionary<string, SyncItem> replicaItems = new()
        {
            { "file.txt", new SyncItem(false, "hash123", 100, DateTime.UtcNow) }
        };

        string sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        string replicaPath = Path.Combine(Path.GetTempPath(), "Replica");

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, sourcePath,
            replicaItems, replicaPath,
            allowDelete: true);

        // Assert
        tasks.Should().BeEmpty();
    }

    [Fact]
    public void GetSyncTasks_ShouldHandleTypeMismatch_FileVsDirectory()
    {
        // Arrange - file in source, directory in replica
        Dictionary<string, SyncItem> sourceItems = new()
        {
            { "item", new SyncItem(false, "hash123", 100, DateTime.UtcNow) }
        };
        Dictionary<string, SyncItem> replicaItems = new()
        {
            { "item", new SyncItem(true, string.Empty, 0, DateTime.MinValue) }
        };

        string sourcePath = Path.Combine(Path.GetTempPath(), "Source");
        string replicaPath = Path.Combine(Path.GetTempPath(), "Replica");

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, sourcePath,
            replicaItems, replicaPath,
            allowDelete: true);

        // Assert
        tasks.Should().HaveCount(2);
        tasks[0].Action.Should().Be(SyncAction.DeleteDirectory);
        tasks[1].Action.Should().Be(SyncAction.CopyFile);
    }
}
