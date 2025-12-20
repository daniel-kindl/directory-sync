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

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, @"C:\Source",
            replicaItems, @"C:\Replica",
            allowDelete: true);

        // Assert
        tasks.Should().ContainSingle();
        tasks[0].Action.Should().Be(SyncAction.CreateDirectory);
        tasks[0].DestinationPath.Should().Be(@"C:\Replica\subdir");
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

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, @"C:\Source",
            replicaItems, @"C:\Replica",
            allowDelete: true);

        // Assert
        tasks.Should().ContainSingle();
        tasks[0].Action.Should().Be(SyncAction.CopyFile);
        tasks[0].SourcePath.Should().Be(@"C:\Source\file.txt");
        tasks[0].DestinationPath.Should().Be(@"C:\Replica\file.txt");
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

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, @"C:\Source",
            replicaItems, @"C:\Replica",
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

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, @"C:\Source",
            replicaItems, @"C:\Replica",
            allowDelete: true);

        // Assert
        tasks.Should().ContainSingle();
        tasks[0].Action.Should().Be(SyncAction.DeleteFile);
        tasks[0].DestinationPath.Should().Be(@"C:\Replica\old-file.txt");
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

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, @"C:\Source",
            replicaItems, @"C:\Replica",
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

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, @"C:\Source",
            replicaItems, @"C:\Replica",
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

        // Act
        List<SyncTask> tasks = new SyncPlanner().GetSyncTasks(
            sourceItems, @"C:\Source",
            replicaItems, @"C:\Replica",
            allowDelete: true);

        // Assert
        tasks.Should().HaveCount(2);
        tasks[0].Action.Should().Be(SyncAction.DeleteDirectory);
        tasks[1].Action.Should().Be(SyncAction.CopyFile);
    }
}
