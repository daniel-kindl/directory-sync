using DirectorySync.Core;
using System.Security.Cryptography;

namespace DirectorySync.Service
{
    public class SyncService
    {
        private readonly string _source;
        private readonly string _replica;
        private readonly TimeSpan _interval;
        private CancellationToken _ctk;

        public SyncService(string sourceDir, string replicaDir, TimeSpan interval, CancellationToken token)
        {
            _source = sourceDir;
            _replica = replicaDir;
            _interval = interval;
            _ctk = token;
        }

        public void RunOnce()
        {
            List<SyncTask> tasks = Planner.GetSyncTasks
            (
                sourceItems: GetDirectoryItems(_source),
                sourceRoot: _source,
                replicaItems: GetDirectoryItems(_replica),
                destinationRoot: _replica
            );

            Executor.ExecuteSynchronization(tasks);
        }

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
                    await Task.Delay(_interval, _ctk); // breaks out on cancel
                }
                catch (TaskCanceledException)
                {
                    break; // exit loop on cancel
                }
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            SHA256 sha256 = SHA256.Create();
            FileStream stream = File.OpenRead(filePath);
            byte[] hash = sha256.ComputeHash(stream);
            stream.Close();
            return Convert.ToHexString(hash);
        }

        private static Dictionary<string, SyncItem> GetDirectoryItems(string directoryPath)
        {
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
                    string relPath = Path.GetRelativePath(directoryPath, dir);
                    directoryItems.TryAdd(relPath, new SyncItem(true, string.Empty));
                }

                foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
                {
                    string relPath = Path.GetRelativePath(directoryPath, file);
                    FileInfo fileInfo = new(file);
                    string fileHash = ComputeFileHash(fileInfo.FullName);
                    directoryItems.TryAdd(relPath, new SyncItem(false, fileHash));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading directory '{directoryPath}': {ex.Message}");
                throw new IOException($"Error reading directory '{directoryPath}': {ex.Message}", ex);
            }

            return directoryItems;
        }
    }
}
