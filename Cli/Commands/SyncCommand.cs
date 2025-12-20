using System.ComponentModel;
using DirectorySync.Engine;
using Serilog;
using Serilog.Events;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DirectorySync.Cli.Commands;

/// <summary>
/// Command settings for one-time synchronization operations.
/// </summary>
/// <remarks>
/// Supports configuration via command-line arguments, JSON config file, or both (CLI takes precedence).
/// All properties use init-only setters for immutability after construction.
/// </remarks>
public class SyncSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the configuration file path. If not specified, auto-discovers <c>dirsync.json</c> in current or parent directories.
    /// </summary>
    [CommandOption("-c|--config <PATH>")]
    [Description("Configuration file path (default: dirsync.json in current directory)")]
    public string? ConfigFile { get; init; }

    /// <summary>
    /// Gets or sets the source directory path. Overrides configuration file if specified.
    /// </summary>
    [CommandOption("-s|--source <PATH>")]
    [Description("Source directory path")]
    public string? Source { get; init; }

    /// <summary>
    /// Gets or sets the replica directory path. Overrides configuration file if specified.
    /// </summary>
    [CommandOption("-r|--replica <PATH>")]
    [Description("Replica directory path")]
    public string? Replica { get; init; }

    /// <summary>
    /// Gets or sets the log directory path. Overrides configuration file if specified. Defaults to current directory if not specified.
    /// </summary>
    [CommandOption("--log-dir <PATH>")]
    [Description("Log directory path (default: current directory)")]
    public string? LogDir { get; init; }

    /// <summary>
    /// Gets or sets the minimum log level. Defaults to <see cref="LogEventLevel.Information"/>.
    /// </summary>
    [CommandOption("--log-level <LEVEL>")]
    [Description("Log level: Verbose, Debug, Information, Warning, Error (default: Information)")]
    public LogEventLevel LogLevel { get; init; } = LogEventLevel.Information;

    /// <summary>
    /// Gets or sets whether to perform a dry run (preview changes without executing them). Overrides configuration file if specified.
    /// </summary>
    [CommandOption("--dry-run")]
    [Description("Show what would be done without making changes")]
    public bool DryRun { get; init; }

    /// <summary>
    /// Gets or sets whether deletions are disabled. When true, sets <see cref="ConfigFile.AllowDelete"/> to false. Overrides configuration file.
    /// </summary>
    [CommandOption("--no-delete")]
    [Description("Don't delete files/directories from replica")]
    public bool NoDelete { get; init; }

    /// <summary>
    /// Gets or sets whether checksums are disabled. When true, sets <see cref="ConfigFile.UseChecksum"/> to false. Overrides configuration file.
    /// </summary>
    [CommandOption("--no-checksum")]
    [Description("Don't use SHA-256 checksums (faster but less accurate)")]
    public bool NoChecksum { get; init; }

    /// <summary>
    /// Gets or sets the timestamp comparison tolerance in seconds. Defaults to 2.0 for FAT32 compatibility. Overrides configuration file if different from default.
    /// </summary>
    [CommandOption("--timestamp-tolerance <SECONDS>")]
    [Description("Timestamp comparison tolerance in seconds (default: 2 for FAT32 compatibility)")]
    public double TimestampToleranceSeconds { get; init; } = 2.0;

    /// <summary>
    /// Loads and merges configuration from file (if specified) with command-line arguments.
    /// </summary>
    /// <returns>
    /// A merged <see cref="Cli.ConfigFile"/> where command-line arguments override file configuration.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when a specific config file path is provided but the file doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a config file is found but cannot be parsed.</exception>
    /// <remarks>
    /// <para><strong>Configuration Priority (highest to lowest):</strong></para>
    /// <list type="number">
    /// <item>Command-line arguments (this instance's properties)</item>
    /// <item>Specified configuration file (<see cref="ConfigFile"/> option)</item>
    /// <item>Auto-discovered <c>dirsync.json</c> (current or parent directories)</item>
    /// <item>Default values</item>
    /// </list>
    /// <para>If no config file is found via auto-discovery, only CLI arguments are used.</para>
    /// </remarks>
    public Cli.ConfigFile GetEffectiveConfig()
    {
        Cli.ConfigFile? fileConfig = null;

        // Try to load from specified config file
        if (!string.IsNullOrWhiteSpace(ConfigFile))
        {
            fileConfig = Cli.ConfigFile.LoadFromFile(ConfigFile);
            if (fileConfig == null)
            {
                throw new FileNotFoundException($"Configuration file not found: {ConfigFile}");
            }
        }
        else
        {
            // Try to auto-discover dirsync.json
            fileConfig = Cli.ConfigFile.TryLoadFromCurrentDirectory();
        }

        // Create config from command-line args
        var cliConfig = new Cli.ConfigFile
        {
            Source = Source,
            Replica = Replica,
            LogDirectory = LogDir,
            DryRun = DryRun ? true : null,
            AllowDelete = NoDelete ? false : null,
            UseChecksum = NoChecksum ? false : null,
            TimestampTolerance = TimestampToleranceSeconds != 2.0 ? TimestampToleranceSeconds : null
        };

        // Merge: CLI args override file config
        if (fileConfig != null)
        {
            cliConfig.MergeWith(fileConfig);
        }

        return cliConfig;
    }

    /// <summary>
    /// Validates the merged configuration before command execution.
    /// </summary>
    /// <returns>
    /// <see cref="ValidationResult.Success"/> if all settings are valid; otherwise an error result with a descriptive message.
    /// </returns>
    /// <remarks>
    /// <para><strong>Validations Performed:</strong></para>
    /// <list type="bullet">
    /// <item>Configuration file can be loaded and parsed (if specified)</item>
    /// <item>Source path is provided and valid (required, absolute, no traversal patterns)</item>
    /// <item>Replica path is provided and valid (required, absolute, no traversal patterns)</item>
    /// <item>Timestamp tolerance is in valid range (0 to 3600 seconds)</item>
    /// </list>
    /// <para>Called automatically by Spectre.Console.Cli before <see cref="SyncCommand.Execute"/>.</para>
    /// </remarks>
    public override ValidationResult Validate()
    {
        // Get effective config after merging file and CLI args
        Cli.ConfigFile config;
        try
        {
            config = GetEffectiveConfig();
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Configuration error: {ex.Message}");
        }

        // Validate merged config
        PathValidation.ValidationError? sourceValidation = PathValidation.ValidatePath(config.Source, "Source path");
        if (sourceValidation != null)
        {
            return ValidationResult.Error(sourceValidation.Message ?? "Invalid source path");
        }

        PathValidation.ValidationError? replicaValidation = PathValidation.ValidatePath(config.Replica, "Replica path");
        if (replicaValidation != null)
        {
            return ValidationResult.Error(replicaValidation.Message ?? "Invalid replica path");
        }

        double tolerance = config.TimestampTolerance ?? 2.0;
        PathValidation.ValidationError? toleranceValidation = PathValidation.ValidateTimestampTolerance(tolerance);
        if (toleranceValidation != null)
        {
            return ValidationResult.Error(toleranceValidation.Message ?? "Invalid timestamp tolerance");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Executes a one-time directory synchronization operation.
/// </summary>
/// <remarks>
/// <para>Initializes logging, creates a sync engine, performs synchronization, and displays results with formatted tables.</para>
/// <para>Returns appropriate exit codes based on operation outcome (see <see cref="ExitCodes"/>).</para>
/// </remarks>
public sealed class SyncCommand : Command<SyncSettings>
{
    /// <summary>
    /// Executes the synchronization command.
    /// </summary>
    /// <param name="context">The command context provided by Spectre.Console.Cli (not used).</param>
    /// <param name="settings">The validated command settings containing source, replica, and options.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (Ctrl+C).</param>
    /// <returns>
    /// An exit code from <see cref="ExitCodes"/>:
    /// <list type="bullet">
    /// <item><see cref="ExitCodes.Success"/> (0): Synchronization completed successfully.</item>
    /// <item><see cref="ExitCodes.PartialFailure"/> (10): Some operations failed; see failures table in output.</item>
    /// <item><see cref="ExitCodes.Cancelled"/> (130): Operation canceled by user (Ctrl+C).</item>
    /// <item><see cref="ExitCodes.PathValidationError"/> (2): Path validation failed.</item>
    /// <item><see cref="ExitCodes.DirectoryNotFound"/> (3): Source directory not found.</item>
    /// <item><see cref="ExitCodes.PermissionDenied"/> (4): Access denied.</item>
    /// <item><see cref="ExitCodes.InsufficientDiskSpace"/> (5): Not enough disk space on replica drive.</item>
    /// <item><see cref="ExitCodes.GeneralError"/> (1): Other errors.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Output:</strong></para>
    /// Displays a summary table with metrics (created, updated, deleted, bytes copied, duration, status).
    /// If failures occurred, displays an additional table with failure details (action, path, error).
    /// <para><strong>Progress Reporting:</strong></para>
    /// Progress updates are printed to console using Spectre.Console markup showing task count, percentages, and current file.
    /// <para><strong>Logging:</strong></para>
    /// Initializes Serilog with console and file logging. Ensures logs are flushed via <see cref="Logger.Close"/> in the finally block.
    /// </remarks>
    public override int Execute(CommandContext context, SyncSettings settings, CancellationToken cancellationToken)
    {
        // Get effective configuration (merged from file + CLI args)
        ConfigFile config = settings.GetEffectiveConfig();

        Logger.Initialize(config.LogDirectory, settings.LogLevel);

        try
        {
            Log.Information("Starting directory synchronization");

            if (!string.IsNullOrWhiteSpace(settings.ConfigFile))
            {
                Log.Information("Using configuration file: {ConfigFile}", settings.ConfigFile);
            }

            var engine = new SyncEngine(
                useChecksum: config.UseChecksum ?? true,
                useTimestampOptimization: config.UseTimestampOptimization ?? true,
                logger: new SerilogAdapter(),
                timestampTolerance: TimeSpan.FromSeconds(config.TimestampTolerance ?? 2.0));

            var options = new SyncOptions
            {
                Source = config.Source!,
                Replica = config.Replica!,
                DryRun = config.DryRun ?? false,
                AllowDelete = config.AllowDelete ?? true,
                UseChecksum = config.UseChecksum ?? true
            };

            // Create progress reporter with live status
            var progress = new Progress<SyncProgress>(p =>
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Progress:[/] {p.CompletedTasks}/{p.TotalTasks} tasks " +
                    $"({p.PercentComplete:F1}%), {p.BytesPercentComplete:F1}% bytes - {p.CurrentOperation}: {p.CurrentFile}");
            });

            SyncResult result = engine.Sync(options, progress, cancellationToken);

            AnsiConsole.WriteLine();
            var table = new Table();
            table.AddColumn("Metric");
            table.AddColumn("Value");
            table.AddRow("Created", result.Created.ToString());
            table.AddRow("Updated", result.Updated.ToString());
            table.AddRow("Deleted", result.Deleted.ToString());
            table.AddRow("Bytes Copied", FormatStrings.FormatBytes(result.BytesCopied));
            table.AddRow("Duration", result.Duration.ToString(FormatStrings.DurationFormat));
            table.AddRow("Status", result.Success ? "[green]Success[/]" : "[red]Failed[/]");
            AnsiConsole.Write(table);

            if (result.Failures.Count > 0)
            {
                AnsiConsole.MarkupLine($"\n[red]⚠ {result.Failures.Count} operations failed:[/]\n");

                var errorTable = new Table();
                errorTable.AddColumn("Action");
                errorTable.AddColumn("Path");
                errorTable.AddColumn("Error");

                foreach (OperationFailure failure in result.Failures)
                {
                    errorTable.AddRow(
                        failure.Action.ToString(),
                        failure.Path,
                        failure.ErrorMessage
                    );
                }
                AnsiConsole.Write(errorTable);
            }

            Log.Information("Synchronization completed - Created: {Created}, Updated: {Updated}, Deleted: {Deleted}",
                result.Created, result.Updated, result.Deleted);

            return result.Success ? ExitCodes.Success : ExitCodes.PartialFailure;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Sync cancelled by user[/]");
            Log.Warning("Sync cancelled by user");
            return ExitCodes.Cancelled;
        }
        catch (ArgumentException ex)
        {
            Log.Error(ex, "Path validation failed");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.PathValidationError;
        }
        catch (DirectoryNotFoundException ex)
        {
            Log.Error(ex, "Directory not found");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.DirectoryNotFound;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(ex, "Permission denied");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.PermissionDenied;
        }
        catch (IOException ex) when (ex.Message.Contains("Insufficient disk space"))
        {
            Log.Error(ex, "Insufficient disk space");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.InsufficientDiskSpace;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Synchronization failed");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.GeneralError;
        }
        finally
        {
            Logger.Close();
        }
    }
}
