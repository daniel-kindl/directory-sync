namespace DirectorySync.Engine;

/// <summary>
/// Creates synchronization plans by comparing source and replica directory snapshots.
/// </summary>
/// <remarks>
/// This class is stateless and thread-safe for concurrent plan generation.
/// Plans are generated in three phases: directories (create), files (copy/update), and deletions (when enabled).
/// </remarks>
public class SyncPlanner
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncPlanner"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output. If null, a <see cref="NullLogger"/> is used.</param>
    public SyncPlanner(ILogger? logger = null)
    {
        _logger = logger ?? new NullLogger();
    }

    /// <summary>
    /// Generates a list of synchronization tasks needed to make the replica match the source.
    /// </summary>
    /// <param name="sourceItems">Dictionary of items scanned from the source directory, keyed by relative path.</param>
    /// <param name="sourceRoot">Absolute path to the source directory root (used to construct full paths in tasks).</param>
    /// <param name="replicaItems">Dictionary of items scanned from the replica directory, keyed by relative path.</param>
    /// <param name="destinationRoot">Absolute path to the replica directory root (used to construct full paths in tasks).</param>
    /// <param name="allowDelete">If true, items in replica but not in source will be deleted. If false, deletions are skipped.</param>
    /// <returns>
    /// A list of <see cref="SyncTask"/> objects representing operations to execute.
    /// Returns an empty list if the replica is already synchronized with the source.
    /// </returns>
    /// <remarks>
    /// <para>Planning is performed in three phases:</para>
    /// <list type="number">
    /// <item><description>Directories from source: creates missing directories, resolves file/directory conflicts.</description></item>
    /// <item><description>Files from source: copies new files, updates changed files (by hash, size, or timestamp when hashes are deferred), resolves directory/file conflicts.</description></item>
    /// <item><description>Deletions (if <paramref name="allowDelete"/> is true): removes items from replica that don't exist in source, directories deleted last.</description></item>
    /// </list>
    /// <para>
    /// File changes are detected by comparing <see cref="SyncItem.ItemHash"/> and <see cref="SyncItem.Size"/>.
    /// When both hashes are deferred, <see cref="SyncItem.LastWriteUtc"/> is compared instead.
    /// </para>
    /// <para>
    /// This method does not throw exceptions and does not perform file system operations.
    /// It operates purely on the in-memory snapshots provided.
    /// </para>
    /// </remarks>
    public List<SyncTask> GetSyncTasks(
        Dictionary<string, SyncItem> sourceItems,
        string sourceRoot,
        Dictionary<string, SyncItem> replicaItems,
        string destinationRoot,
        bool allowDelete)
    {
        _logger.LogDebug("Planning sync tasks - Source: {SourceCount} items, Replica: {ReplicaCount} items, AllowDelete: {AllowDelete}",
            sourceItems.Count, replicaItems.Count, allowDelete);

        List<SyncTask> tasks = [];

        // Phase 1: Process directories from source
        IEnumerable<KeyValuePair<string, SyncItem>> sourceDirectories = sourceItems.Where(item => item.Value.IsDir);
        foreach (KeyValuePair<string, SyncItem> directory in sourceDirectories)
        {
            string path = directory.Key;

            if (!replicaItems.TryGetValue(path, out SyncItem? repMeta))
            {
                tasks.Add(new SyncTask(SyncAction.CreateDirectory,
                    Path.Combine(sourceRoot, path),
                    Path.Combine(destinationRoot, path)));
            }
            else if (!repMeta.IsDir)
            {
                if (allowDelete)
                {
                    tasks.Add(new SyncTask(SyncAction.DeleteFile,
                        Path.Combine(sourceRoot, path),
                        Path.Combine(destinationRoot, path)));
                }
                tasks.Add(new SyncTask(SyncAction.CreateDirectory,
                    Path.Combine(sourceRoot, path),
                    Path.Combine(destinationRoot, path)));
            }
        }

        // Phase 2: Process files from source
        IEnumerable<KeyValuePair<string, SyncItem>> sourceFiles = sourceItems.Where(item => !item.Value.IsDir);
        foreach ((string path, SyncItem srcMeta) in sourceFiles)
        {
            if (!replicaItems.TryGetValue(path, out SyncItem? repMeta))
            {
                tasks.Add(new SyncTask(SyncAction.CopyFile,
                    Path.Combine(sourceRoot, path),
                    Path.Combine(destinationRoot, path)));
            }
            else if (repMeta.IsDir != srcMeta.IsDir)
            {
                if (allowDelete)
                {
                    tasks.Add(new SyncTask(
                        repMeta.IsDir ? SyncAction.DeleteDirectory : SyncAction.DeleteFile,
                        Path.Combine(sourceRoot, path),
                        Path.Combine(destinationRoot, path)));
                }
                tasks.Add(new SyncTask(SyncAction.CopyFile,
                    Path.Combine(sourceRoot, path),
                    Path.Combine(destinationRoot, path)));
            }
            else if (repMeta.Size != srcMeta.Size ||
                     (string.IsNullOrEmpty(repMeta.ItemHash) && string.IsNullOrEmpty(srcMeta.ItemHash)
                         ? repMeta.LastWriteUtc != srcMeta.LastWriteUtc
                         : repMeta.ItemHash != srcMeta.ItemHash))
            {
                tasks.Add(new SyncTask(SyncAction.UpdateFile,
                    Path.Combine(sourceRoot, path),
                    Path.Combine(destinationRoot, path)));
            }
        }

        if (!allowDelete)
        {
            _logger.LogDebug("Planned {TaskCount} tasks (deletions disabled)", tasks.Count);
            return tasks;
        }

        // Handle deletions: items in replica that don't exist in source
        // Process directories after files to ensure proper cleanup order
        IOrderedEnumerable<KeyValuePair<string, SyncItem>> itemsToDelete = replicaItems
            .Where(item => !sourceItems.ContainsKey(item.Key))
            .OrderByDescending(item => item.Value.IsDir); // Directories last

        int deleteCount = 0;
        foreach ((string path, SyncItem repMeta) in itemsToDelete)
        {
            SyncAction action = repMeta.IsDir ? SyncAction.DeleteDirectory : SyncAction.DeleteFile;

            tasks.Add(new SyncTask(
                action,
                Path.Combine(sourceRoot, path),
                Path.Combine(destinationRoot, path)));
            deleteCount++;
        }

        _logger.LogDebug("Planned {TaskCount} tasks ({CreateCount} creates, {UpdateCount} updates, {DeleteCount} deletes)",
            tasks.Count,
            tasks.Count(t => t.Action is SyncAction.CreateDirectory or SyncAction.CopyFile),
            tasks.Count(t => t.Action == SyncAction.UpdateFile),
            deleteCount);

        return tasks;
    }

    /// <summary>
    /// Calculates the total disk space required to execute a list of synchronization tasks.
    /// </summary>
    /// <param name="tasks">The list of tasks to analyze.</param>
    /// <returns>
    /// The total number of bytes that will be copied, summed across all <see cref="SyncAction.CopyFile"/>
    /// and <see cref="SyncAction.UpdateFile"/> tasks. Returns 0 if no copy operations are needed or if source files don't exist.
    /// </returns>
    /// <remarks>
    /// This method accesses the file system to query file sizes via <see cref="FileInfo"/>.
    /// If a source file referenced in a task does not exist, it contributes 0 bytes to the total.
    /// Does not account for filesystem overhead, compression, or existing file sizes (pessimistic estimate).
    /// </remarks>
    public static long CalculateRequiredSpace(List<SyncTask> tasks)
    {
        return tasks
            .Where(t => t.Action is SyncAction.CopyFile or SyncAction.UpdateFile)
            .Sum(t => File.Exists(t.SourcePath) ? new FileInfo(t.SourcePath).Length : 0);
    }
}
