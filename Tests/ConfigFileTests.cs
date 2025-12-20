using DirectorySync.Cli;
using Xunit;

namespace DirectorySync.Tests;

public class ConfigFileTests : IDisposable
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), $"dirsync-config-test-{Guid.NewGuid()}");

    [Fact]
    public void SaveToFile_CreatesDirectoryIfNotExists()
    {
        // Arrange
        string nestedPath = Path.Combine(_testDir, "subdir", "config", "test.json");
        var config = new ConfigFile
        {
            Source = "/path/to/source",
            Replica = "/path/to/replica",
            Interval = 60,
            UseChecksum = true,
            AllowDelete = false
        };

        // Act
        config.SaveToFile(nestedPath);

        // Assert
        Assert.True(File.Exists(nestedPath), "File should exist after save");
        Assert.True(Directory.Exists(Path.GetDirectoryName(nestedPath)), "Directory should be created");

        // Verify content can be read back
        var loaded = ConfigFile.LoadFromFile(nestedPath);
        Assert.NotNull(loaded);
        Assert.Equal("/path/to/source", loaded!.Source);
        Assert.Equal("/path/to/replica", loaded.Replica);
        Assert.Equal(60, loaded.Interval);
        Assert.True(loaded.UseChecksum);
        Assert.False(loaded.AllowDelete);
    }

    [Fact]
    public void SaveToFile_WorksWithExistingDirectory()
    {
        // Arrange
        Directory.CreateDirectory(_testDir);
        string filePath = Path.Combine(_testDir, "config.json");
        var config = new ConfigFile
        {
            Source = "/test/path",
            Replica = "/backup/path"
        };

        // Act
        config.SaveToFile(filePath);

        // Assert
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public void SaveToFile_WorksWithCurrentDirectory()
    {
        // Arrange
        string filePath = Path.Combine(_testDir, "direct.json");
        Directory.CreateDirectory(_testDir);

        var config = new ConfigFile
        {
            Source = "/test/source"
        };

        // Act & Assert - Should not throw
        config.SaveToFile(filePath);
        Assert.True(File.Exists(filePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }
        GC.SuppressFinalize(this);
    }
}
