using DirectorySync.Core;

namespace DirectorySync.Service
{
    internal class Planner
    {
        /// <summary>
        /// Compares the contents of the source and replica directories and 
        /// generates a list of synchronization tasks required to make the 
        /// replica an identical copy of the source.
        /// </summary>
        /// <param name="sourceItems">
        /// Dictionary of relative paths to <see cref="SyncItem"/> metadata for the source directory.
        /// Keys are relative paths; values contain attributes like type (file/dir) and hash.
        /// </param>
        /// <param name="sourceRoot">
        /// Absolute path to the root of the source directory. Used to build full source paths in tasks.
        /// </param>
        /// <param name="replicaItems">
        /// Dictionary of relative paths to <see cref="SyncItem"/> metadata for the replica directory.
        /// Keys are relative paths; values contain attributes like type (file/dir) and hash.
        /// </param>
        /// <param name="destinationRoot">
        /// Absolute path to the root of the replica directory. Used to build full destination paths in tasks.
        /// </param>
        /// <returns>
        /// A list of <see cref="SyncTask"/> objects describing the operations
        /// (create, copy, update, delete) that need to be executed to synchronize
        /// the replica with the source.
        /// </returns>

        internal static List<SyncTask> GetSyncTasks
        (
            Dictionary<string, SyncItem> sourceItems,  string sourceRoot,
            Dictionary<string, SyncItem> replicaItems, string destinationRoot
        )
        {
            Logger.Information("Creating tasks for synchronization...");
            
            List<SyncTask> tasks = new();

            // Create directories or fix type mismatches (dir in source, file in replica)
            foreach (var directory in sourceItems.Where(it => it.Value.IsDir))
            {
                string path = directory.Key;

                if (!replicaItems.TryGetValue(path, out SyncItem? repMeta))
                {
                    tasks.Add(new SyncTask(SyncAction.CreateDirectory, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));
                }
                else if (!repMeta.IsDir)    // Replica has a FILE where source has a DIR
                {
                    // Delete the file
                    tasks.Add(new SyncTask(SyncAction.DeleteFile, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));

                    // Create the directory
                    tasks.Add(new SyncTask(SyncAction.CreateDirectory, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));
                }
            }

            // Create or update files
            foreach (var file in sourceItems.Where(item => !item.Value.IsDir))
            {
                string path      = file.Key;
                SyncItem srcMeta = file.Value;

                // Copy new file to replica
                if (!replicaItems.TryGetValue(path, out SyncItem? repMeta))
                {
                    tasks.Add(new SyncTask(SyncAction.CopyFile, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));
                }

                // Catching cases where a file in the source is a directory in the replica
                else if (repMeta.IsDir != srcMeta.IsDir)
                {
                    // Delete whatever is in replica
                    tasks.Add(new SyncTask(repMeta.IsDir ? SyncAction.DeleteDirectory : SyncAction.DeleteFile, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));

                    // Since src is a file in this loop, recreate as file
                    tasks.Add(new SyncTask(SyncAction.CopyFile, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));
                }

                // Update file in replica
                else if (repMeta.ItemHash != srcMeta.ItemHash)
                {
                    tasks.Add(new SyncTask(SyncAction.UpdateFile, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));
                }
            }

            // Delete files/dirs present in replica but not in source
            foreach (var item in replicaItems.Where(item => !sourceItems.ContainsKey(item.Key))
                                             .OrderByDescending(item => item.Value.IsDir)) // sort files before dirs
            {
                string path      = item.Key;
                SyncItem repMeta = item.Value;

                // Delete file/dir in replica
                tasks.Add(new SyncTask(repMeta.IsDir ? SyncAction.DeleteDirectory : SyncAction.DeleteFile, Path.Combine(sourceRoot, path), Path.Combine(destinationRoot, path)));
            }

            Logger.Information(tasks.Count > 0 ? "Tasks for sinchronization created." : "No tasks needed. Replica up to date.");

            return tasks;
        }
    }
}
