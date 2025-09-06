using DirectorySync.Service;

namespace DirectorySync
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.Title = "Directory Synchronization Tool";

            string sourceDir      = @"C:\Users\Daniel\Desktop\sourceDir";
            string replicaDir     = @"C:\Users\Daniel\Desktop\replicaDir";
            int intervalMs        = 10 * 1000;
            CancellationToken ctk = new();

            SyncService syncService = new
            (
                sourceDir:  sourceDir,
                replicaDir: replicaDir,
                interval:   TimeSpan.FromMilliseconds(intervalMs),
                token:      ctk
            );

           await syncService.RunAsync();
        }
    }
}
