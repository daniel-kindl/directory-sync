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
            List<SyncTask> tasks = new List<SyncTask>();

            // Create directories missing in replica directory
            foreach (var directory in sourceItems.Where(item => item.Value.IsDir))

            {
                var path = directory.Key; 

                if (!replicaItems.ContainsKey(path))
                {
                    tasks.Add(new SyncTask(
                        Action: SyncAction.CreateDirectory,
                        SourcePath: Path.Combine(sourceRoot, path),
                        DestinationPath: Path.Combine(destinationRoot, path)
                    ));
                }
            }

            // Create or update files
            foreach (var file in sourceItems.Where(item => !item.Value.IsDir))
            {
                var path    = file.Key;
                var srcMeta = file.Value;

                if (!replicaItems.TryGetValue(path, out var repMeta))
                {
                    tasks.Add(new SyncTask(
                        Action: SyncAction.CopyFile,
                        SourcePath: Path.Combine(sourceRoot, path),
                        DestinationPath: Path.Combine(destinationRoot, path)
                    ));
                }

                // catching cases where a file in the source is a directory in the replica (or vice versa)
                else if (repMeta.IsDir || repMeta.ItemHash != srcMeta.ItemHash)
                {
                    tasks.Add(new SyncTask(
                        Action: SyncAction.UpdateFile,
                        SourcePath: Path.Combine(sourceRoot, path),
                        DestinationPath: Path.Combine(destinationRoot, path)
                    ));
                }
            }

            // Delete files/dirs present in replica but not in source
            foreach (var item in replicaItems.Where(item => !sourceItems.ContainsKey(item.Key))
                                             .OrderByDescending(item => item.Value.IsDir)) // sort files before dirs
            {
                var path    = item.Key;
                var repMeta = item.Value;

                tasks.Add(new SyncTask(
                    Action: repMeta.IsDir ? SyncAction.DeleteDirectory : SyncAction.DeleteFile,
                    SourcePath: Path.Combine(sourceRoot, path),
                    DestinationPath: Path.Combine(destinationRoot, path)
                ));
            }

            return tasks;
        }
    }
}
