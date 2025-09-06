namespace DirectorySync.Core
{
    /// <summary>
    /// Defines the possible synchronization operations
    /// that can be performed to keep the replica folder
    /// identical to the source folder.
    /// </summary>
    enum SyncAction
    {
        /// <summary>
        /// Create a directory that exists in the source but not in the replica.
        /// </summary>
        CreateDirectory,

        /// <summary>
        /// Delete a directory that exists in the replica but not in the source.
        /// </summary>
        DeleteDirectory,

        /// <summary>
        /// Copy a file from source to replica when it is missing in the replica.
        /// </summary>
        CopyFile,

        /// <summary>
        /// Replace a file in the replica with the source version when content or type differs.
        /// </summary>
        UpdateFile,

        /// <summary>
        /// Delete a file that exists in the replica but not in the source.
        /// </summary>
        DeleteFile
    }

    /// <summary>
    /// Represents a single planned synchronization task,
    /// including the action to perform and the full paths
    /// for the source and replica targets.
    /// </summary>
    /// <param name="Action">The synchronization action to perform.</param>
    /// <param name="SourcePath">Absolute path to the item in the source directory.</param>
    /// <param name="DestinationPath">Absolute path to the item in the replica directory.</param>
    record SyncTask(SyncAction Action, string SourcePath, string DestinationPath);

    /// <summary>
    /// Represents metadata about a file system item
    /// (either a file or a directory) used when planning synchronization.
    /// </summary>
    /// <param name="IsDir">True if the item is a directory, false if it is a file.</param>
    /// <param name="ItemHash">A hash string representing the file content.</param>
    record SyncItem(bool IsDir, string ItemHash);
}
