using DirectorySync.Engine;
using FluentAssertions;

namespace DirectorySync.Tests;

public class SyncEngineTests
{
    [Fact]
    public void ValidatePaths_ShouldThrow_WhenSourceAndReplicaAreSame()
    {
        // Arrange
        string samePath = Path.GetTempPath();

        // Act
        Action act = () => SyncEngine.ValidatePaths(samePath, samePath);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Source and replica cannot be the same path");
    }

    [Fact]
    public void ValidatePaths_ShouldThrow_WhenReplicaIsInsideSource()
    {
        // Arrange
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string source = Path.Combine(tempRoot, "Source");
        string replica = Path.Combine(source, "Replica");
        Directory.CreateDirectory(replica);

        try
        {
            // Act
            Action act = () => SyncEngine.ValidatePaths(source, replica);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Replica cannot be inside source");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void ValidatePaths_ShouldThrow_WhenSourceIsInsideReplica()
    {
        // Arrange
        string tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string replica = Path.Combine(tempRoot, "Replica");
        string source = Path.Combine(replica, "Source");
        Directory.CreateDirectory(source);

        try
        {
            // Act
            Action act = () => SyncEngine.ValidatePaths(source, replica);

            // Assert
            act.Should().Throw<ArgumentException>()
                .WithMessage("Source cannot be inside replica");
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public void ValidatePaths_ShouldThrow_WhenSourceDoesNotExist()
    {
        // Arrange
        string source = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string replica = Path.GetTempPath();

        // Act
        Action act = () => SyncEngine.ValidatePaths(source, replica);

        // Assert
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void ValidatePaths_ShouldNotThrow_WhenPathsAreValid()
    {
        // Arrange
        string source = Path.GetTempPath();
        string replica = Path.Combine(Path.GetTempPath(), "test-replica");

        // Act
        Action act = () => SyncEngine.ValidatePaths(source, replica);

        // Assert
        act.Should().NotThrow();
    }
}
