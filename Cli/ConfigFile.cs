using System.Text.Json;
using System.Text.Json.Serialization;

namespace DirectorySync.Cli;

/// <summary>
/// Configuration file model for DirectorySync.
/// Supports loading settings from dirsync.json for daemon and automation scenarios.
/// </summary>
public class ConfigFile
{
    /// <summary>
    /// Gets or sets the source directory path (absolute path required).
    /// The source is the authoritative directory; the replica will be synchronized to match it.
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the replica directory path (absolute path required).
    /// The replica will be modified to match the source directory structure and contents.
    /// </summary>
    [JsonPropertyName("replica")]
    public string? Replica { get; set; }

    /// <summary>
    /// Gets or sets the interval in seconds for daemon mode.
    /// Valid range: 1 to 86400 seconds (1 second to 24 hours). Default is 60 if not specified.
    /// </summary>
    [JsonPropertyName("interval")]
    public int? Interval { get; set; }

    /// <summary>
    /// Gets or sets whether deletion of files and directories in the replica is allowed.
    /// When false, items in replica but missing from source are retained. Default is true if not specified.
    /// </summary>
    [JsonPropertyName("allowDelete")]
    public bool? AllowDelete { get; set; }

    /// <summary>
    /// Gets or sets whether SHA-256 checksums are used for file comparison.
    /// When false, only file size and timestamp are used (faster but less accurate). Default is true if not specified.
    /// </summary>
    [JsonPropertyName("useChecksum")]
    public bool? UseChecksum { get; set; }

    /// <summary>
    /// Gets or sets whether timestamp optimization is enabled.
    /// When true, files with identical size and timestamp (within tolerance) skip checksum computation. Default is true if not specified.
    /// </summary>
    [JsonPropertyName("useTimestampOptimization")]
    public bool? UseTimestampOptimization { get; set; }

    /// <summary>
    /// Gets or sets the timestamp tolerance in seconds for file comparison.
    /// Valid range: 0 to 3600 seconds. Default is 2.0 seconds (for FAT32 compatibility) if not specified.
    /// </summary>
    [JsonPropertyName("timestampTolerance")]
    public double? TimestampTolerance { get; set; }

    /// <summary>
    /// Gets or sets the log directory path.
    /// If null, logs are written to the current directory. The directory is created automatically if needed.
    /// </summary>
    [JsonPropertyName("logDirectory")]
    public string? LogDirectory { get; set; }

    /// <summary>
    /// Gets or sets whether dry-run mode is enabled (no actual changes are made).
    /// When true, operations are logged but not executed. Default is false if not specified.
    /// </summary>
    [JsonPropertyName("dryRun")]
    public bool? DryRun { get; set; }

    /// <summary>
    /// Loads configuration from a JSON file.
    /// </summary>
    /// <param name="path">The absolute or relative path to the JSON configuration file.</param>
    /// <returns>
    /// A <see cref="ConfigFile"/> instance populated from the JSON, or null if the file does not exist.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the JSON file exists but is malformed or cannot be deserialized.</exception>
    /// <remarks>
    /// <para>JSON parsing is lenient: allows comments, trailing commas, and case-insensitive property names.</para>
    /// <para>Returns null if the file doesn't exist (not considered an error).</para>
    /// <para>All configuration properties are nullable; missing properties in JSON will be null in the returned object.</para>
    /// </remarks>
    public static ConfigFile? LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            return JsonSerializer.Deserialize<ConfigFile>(json, options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to parse configuration file: {path}", ex);
        }
    }

    /// <summary>
    /// Saves this configuration to a JSON file with indented formatting.
    /// </summary>
    /// <param name="path">The absolute or relative path where the JSON file will be written. Existing files are overwritten.</param>
    /// <remarks>
    /// <para>Creates the parent directory automatically if it doesn't exist.</para>
    /// <para>JSON is written with indentation for human readability.</para>
    /// <para>Null properties are serialized as <c>null</c> in the JSON output.</para>
    /// </remarks>
    /// <exception cref="UnauthorizedAccessException">Thrown when write access to <paramref name="path"/> is denied.</exception>
    /// <exception cref="IOException">Thrown when an I/O error occurs during writing.</exception>
    public void SaveToFile(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(this, options);

        // Ensure directory exists before writing file
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, json);
    }

    /// <summary>
    /// Tries to find and load <c>dirsync.json</c> from the current directory or up to 3 parent directories.
    /// </summary>
    /// <returns>
    /// A <see cref="ConfigFile"/> if <c>dirsync.json</c> is found and successfully loaded; otherwise null.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if a <c>dirsync.json</c> file is found but cannot be parsed.</exception>
    /// <remarks>
    /// <para>Search order:</para>
    /// <list type="number">
    /// <item>Current working directory</item>
    /// <item>Parent directory</item>
    /// <item>Grandparent directory</item>
    /// <item>Great-grandparent directory</item>
    /// </list>
    /// <para>Returns null if no <c>dirsync.json</c> is found in any of these locations.</para>
    /// <para>Useful for auto-discovering configuration without requiring explicit <c>--config</c> arguments.</para>
    /// </remarks>
    public static ConfigFile? TryLoadFromCurrentDirectory()
    {
        const string configFileName = "dirsync.json";

        // Try current directory first
        string currentDir = Directory.GetCurrentDirectory();
        string configPath = Path.Combine(currentDir, configFileName);

        if (File.Exists(configPath))
        {
            return LoadFromFile(configPath);
        }

        // Try parent directories (up to 3 levels)
        DirectoryInfo? dir = Directory.GetParent(currentDir);
        int levels = 0;
        while (dir != null && levels < 3)
        {
            configPath = Path.Combine(dir.FullName, configFileName);
            if (File.Exists(configPath))
            {
                return LoadFromFile(configPath);
            }
            dir = dir.Parent;
            levels++;
        }

        return null;
    }

    /// <summary>
    /// Merges this configuration with another, using the other's values only for properties that are null in this instance.
    /// </summary>
    /// <param name="other">The configuration to merge from, or null (in which case no changes are made).</param>
    /// <remarks>
    /// <para>This instance's non-null properties take precedence and are not overwritten.</para>
    /// <para>Null properties in this instance are replaced with values from <paramref name="other"/>.</para>
    /// <para>This method mutates the current instance in place.</para>
    /// <para>Typical use case: merge command-line arguments (this) with file configuration (<paramref name="other"/>) where CLI takes precedence.</para>
    /// </remarks>
    public void MergeWith(ConfigFile? other)
    {
        if (other == null)
            return;

        Source ??= other.Source;
        Replica ??= other.Replica;
        Interval ??= other.Interval;
        AllowDelete ??= other.AllowDelete;
        UseChecksum ??= other.UseChecksum;
        UseTimestampOptimization ??= other.UseTimestampOptimization;
        TimestampTolerance ??= other.TimestampTolerance;
        LogDirectory ??= other.LogDirectory;
        DryRun ??= other.DryRun;
    }
}
