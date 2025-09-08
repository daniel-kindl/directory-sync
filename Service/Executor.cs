using DirectorySync.Core;

namespace DirectorySync.Service
{
    internal class Executor
    {
        /// <summary>
        /// Executes a series of synchronization tasks, such as creating, deleting, copying, or updating files and
        /// directories.
        /// </summary>
        /// <remarks>This method processes each task in the provided list sequentially. Supported actions
        /// include: <list type="bullet"> <item><description><see cref="SyncAction.CreateDirectory"/>: Creates a
        /// directory at the specified destination path.</description></item> <item><description><see
        /// cref="SyncAction.DeleteDirectory"/>: Deletes the directory at the specified destination
        /// path.</description></item> <item><description><see cref="SyncAction.CopyFile"/> or <see
        /// cref="SyncAction.UpdateFile"/>: Copies a file from the source path to the destination
        /// path.</description></item> <item><description><see cref="SyncAction.DeleteFile"/>: Deletes the file at the
        /// specified destination path.</description></item> </list> If the <paramref name="tasks"/> list is null or
        /// empty, the method logs a message and exits without performing any actions. Errors encountered during
        /// individual tasks are logged, but the method continues processing the remaining tasks.</remarks>
        /// <param name="tasks">A list of <see cref="SyncTask"/> objects representing the synchronization actions to perform. Each task
        /// specifies the action type and the associated source and/or destination paths.</param>
        public static void ExecuteSynchronization(List<SyncTask> tasks)
        {
            if (tasks == null || tasks.Count == 0)
            {
                return;
            }

            Logger.Information($"Executing {tasks.Count} synchronization tasks...");

            foreach (var task in tasks)
            {
                switch (task.Action)
                {
                    case SyncAction.CreateDirectory:
                        try
                        {
                            Logger.Information($"Creating directory: {task.DestinationPath}");
                            Directory.CreateDirectory(task.DestinationPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to create directory '{task.DestinationPath}': {ex}");
                        }
                        break;

                    case SyncAction.DeleteDirectory:
                        try
                        {
                            Logger.Information($"Deleting directory: {task.DestinationPath}");
                            Directory.Delete(task.DestinationPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to delete directory '{task.DestinationPath}': {ex}");
                        }
                        break;

                    case SyncAction.CopyFile:
                    case SyncAction.UpdateFile:
                        try
                        {
                            Logger.Information($"Copying file: {task.SourcePath} to {task.DestinationPath}");
                            File.Copy(task.SourcePath, task.DestinationPath, true);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to copy file '{task.SourcePath}' to '{task.DestinationPath}': {ex}");
                        }
                        break;

                    case SyncAction.DeleteFile:
                        try
                        {
                            Logger.Information($"Deleting file: {task.DestinationPath}");
                            File.Delete(task.DestinationPath);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Failed to delete file '{task.DestinationPath}': {ex}");
                        }
                        break;

                    default:
                        Logger.Warning($"Unknown action '{task.Action}' for task with destination '{task.DestinationPath}'");
                        break;
                }
            }

            Logger.Information("Synchronization finished.");
        }
    }
}
