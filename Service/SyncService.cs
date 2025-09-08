using DirectorySync.Core;
using System.Security.Cryptography;

namespace DirectorySync.Service
{
    public class SyncService
    {
        /// <summary>
        /// source directory.
        /// </summary>
        private readonly string _source;

        /// <summary>
        /// replica directory.
        /// </summary>
        private readonly string _replica;

        /// <summary>
        /// Interval for synchronization.
        /// </summary>
        private readonly TimeSpan _interval;

        /// <summary>
        /// Cancellation token to cancel synchronization.
        /// </summary>
        private readonly CancellationToken _ctk;

        public SyncService(string sourceDir, string replicaDir, TimeSpan interval, CancellationToken token)
        {
            _source = sourceDir;
            _replica = replicaDir;
            _interval = interval;
            _ctk = token;
        }

        /// <summary>
        /// Runs synchronization only once.
        /// </summary>
        public void RunOnce()
        {
            // Get a list of synhronization task to be preformed
            List<SyncTask> tasks = Planner.GetSyncTasks
            (
                sourceItems: GetDirectoryItems(_source),
                sourceRoot: _source,
                replicaItems: GetDirectoryItems(_replica),
                destinationRoot: _replica
            );

            // execute the synchronization tasks
            Executor.ExecuteSynchronization(tasks);
        }

        /// <summary>
        /// Runs async synchronization periodically.
        /// </summary>
        /// <returns></returns>
        public async Task RunAsync()
        {
            while (!_ctk.IsCancellationRequested)
            {
                try
                {
                    RunOnce();
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error during synchronization: {ex.Message}");
                }

                try
                {
                    await Task.Delay(_interval, _ctk);
                }
                catch (TaskCanceledException)
                {
                    Logger.Information("Stopping synchronization service...");
                    break;
                }
            }
        }

        /// <summary>
        /// Computes a SHA256 hash on file specified by parameter.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        /// <returns>SHA256 hash.</returns>
        private static string ComputeFileHash(string filePath)
        {
            // Computes a SHA256 hash
            SHA256 sha256     = SHA256.Create();
            FileStream stream = File.OpenRead(filePath);
            byte[] hash       = sha256.ComputeHash(stream);

            // Close file stream
            stream.Close();

            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// This function scans a specified directory ands its content (files, subfolders) to dictionary.
        /// </summary>
        /// <param name="directoryPath">Directory path.</param>
        /// <returns>Returns a dictionary with content of specified directory.</returns>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="IOException"></exception>
        private static Dictionary<string, SyncItem> GetDirectoryItems(string directoryPath)
        {
            Logger.Information($"Scanning directory {directoryPath}");
            
            Dictionary<string, SyncItem> directoryItems = [];

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Logger.Warning($"Directory not found: {directoryPath}");
                    throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
                }

                foreach (var dir in Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories))
                {
                    string relDirPath = Path.GetRelativePath(directoryPath, dir);
                    directoryItems.TryAdd(relDirPath, new SyncItem(true, string.Empty));
                }

                foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    string relFilePath = Path.GetRelativePath(directoryPath, file);
                    FileInfo fileInfo = new(file);
                    string fileHash = ComputeFileHash(fileInfo.FullName);
                    directoryItems.TryAdd(relFilePath, new SyncItem(false, fileHash));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading directory '{directoryPath}': {ex.Message}");
            }

            Logger.Information("Scanning completed.");

            return directoryItems;
        }
    }
}
