using DirectorySync.Engine;
using FluentAssertions;

namespace DirectorySync.Tests;

public class ComparisonRegressionTests : IDisposable
{
    private readonly string _sourceDir;
    private readonly string _replicaDir;

    public ComparisonRegressionTests()
    {
        string basePath = Path.Combine(Path.GetTempPath(), $"dirsync-comparison-{Guid.NewGuid()}");
        _sourceDir = Path.Combine(basePath, "source");
        _replicaDir = Path.Combine(basePath, "replica");

        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_replicaDir);
    }

    public void Dispose()
    {
        string? basePath = Directory.GetParent(_sourceDir)?.FullName;
        if (basePath != null && Directory.Exists(basePath))
        {
            Directory.Delete(basePath, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Plan_ShouldDetectSameSizeContentChange_WhenChecksumsAreDisabled()
    {
        string sourceFile = Path.Combine(_sourceDir, "file.txt");
        string replicaFile = Path.Combine(_replicaDir, "file.txt");

        File.WriteAllText(sourceFile, "abc");
        File.WriteAllText(replicaFile, "xyz");
        File.SetLastWriteTimeUtc(replicaFile, DateTime.UtcNow.AddMinutes(-1));
        File.SetLastWriteTimeUtc(sourceFile, DateTime.UtcNow);

        var engine = new SyncEngine(useChecksum: false);
        var options = new SyncOptions
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            UseChecksum = false
        };

        List<SyncTask> tasks = engine.Plan(options);

        tasks.Should().ContainSingle(task => task.Action == SyncAction.UpdateFile);
    }

    [Fact]
    public void Plan_ShouldUseChecksumForSameSizeFiles_WhenTimestampsDiffer()
    {
        string sourceFile = Path.Combine(_sourceDir, "file.txt");
        string replicaFile = Path.Combine(_replicaDir, "file.txt");

        File.WriteAllText(sourceFile, "abc");
        File.WriteAllText(replicaFile, "xyz");
        File.SetLastWriteTimeUtc(replicaFile, DateTime.UtcNow.AddMinutes(-1));
        File.SetLastWriteTimeUtc(sourceFile, DateTime.UtcNow);

        var engine = new SyncEngine(useChecksum: true, useTimestampOptimization: true);
        var options = new SyncOptions
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            UseChecksum = true
        };

        List<SyncTask> tasks = engine.Plan(options);

        tasks.Should().ContainSingle(task => task.Action == SyncAction.UpdateFile);
    }

    [Fact]
    public void Plan_ShouldUseChecksum_WhenSameSizeTimestampsDifferWithinTolerance()
    {
        string sourceFile = Path.Combine(_sourceDir, "file.txt");
        string replicaFile = Path.Combine(_replicaDir, "file.txt");

        File.WriteAllText(sourceFile, "abc");
        File.WriteAllText(replicaFile, "xyz");
        DateTime timestamp = DateTime.UtcNow.AddMinutes(-1);
        File.SetLastWriteTimeUtc(replicaFile, timestamp);
        File.SetLastWriteTimeUtc(sourceFile, timestamp.AddSeconds(1));

        var engine = new SyncEngine(useChecksum: true, useTimestampOptimization: true);
        var options = new SyncOptions
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            UseChecksum = true
        };

        List<SyncTask> tasks = engine.Plan(options);

        tasks.Should().ContainSingle(task => task.Action == SyncAction.UpdateFile);
    }

    [Fact]
    public void Plan_ShouldSkipUpdate_WhenSizeAndTimestampMatch()
    {
        string sourceFile = Path.Combine(_sourceDir, "file.txt");
        string replicaFile = Path.Combine(_replicaDir, "file.txt");

        File.WriteAllText(sourceFile, "abc");
        File.WriteAllText(replicaFile, "abc");
        DateTime timestamp = DateTime.UtcNow.AddMinutes(-1);
        File.SetLastWriteTimeUtc(sourceFile, timestamp);
        File.SetLastWriteTimeUtc(replicaFile, timestamp);

        var engine = new SyncEngine(useChecksum: true, useTimestampOptimization: true);
        var options = new SyncOptions
        {
            Source = _sourceDir,
            Replica = _replicaDir,
            UseChecksum = true
        };

        List<SyncTask> tasks = engine.Plan(options);

        tasks.Should().BeEmpty();
    }
}
