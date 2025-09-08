using DirectorySync.Service;

namespace DirectorySync
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Directory Synchronization Tool";

            // Not all arguments were provided
            if (args.Length < 4)
            {
                Console.WriteLine("Invalid arguments..." + Environment.NewLine);
                Console.WriteLine("Usage: FolderSync <sourceDir> <replicaDir> <logDir> <intervalSeconds>");
                return;
            }

            // Get needed parameters from agruments
            string srcDir        = args[0];
            string dstDir        = args[1];
            string logDir        = args[2];
            TimeSpan intervalSec = TimeSpan.FromSeconds(int.Parse(args[3]));

            // Initializes logger
            Logger.Initialize(logDir);

            // Create a cancellation token source with an event to handle cancelation (ctrl + C)
            CancellationTokenSource cts = new();
            Console.CancelKeyPress += (s, e) =>
            {
                cts.Cancel();
                e.Cancel = true;
            };

            // Create new syns service
            SyncService syncService = new(srcDir, dstDir, intervalSec, cts.Token);

            // Start sync service
            await syncService.RunAsync();
        }
    }
}
