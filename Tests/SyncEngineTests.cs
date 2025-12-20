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
        const string source = @"C:\Source";
        const string replica = @"C:\Source\Replica";

        // Act
        Action act = () => SyncEngine.ValidatePaths(source, replica);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Replica cannot be inside source");
    }

    [Fact]
    public void ValidatePaths_ShouldThrow_WhenSourceIsInsideReplica()
    {
        // Arrange
        const string source = @"C:\Replica\Source";
        const string replica = @"C:\Replica";

        // Act
        Action act = () => SyncEngine.ValidatePaths(source, replica);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("Source cannot be inside replica");
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
