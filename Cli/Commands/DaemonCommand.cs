using System.ComponentModel;
using DirectorySync.Engine;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DirectorySync.Cli.Commands;

/// <summary>
/// Command settings for daemon mode (continuous synchronization at regular intervals).
/// </summary>
/// <remarks>
/// Inherits all settings from <see cref="SyncSettings"/> and adds interval configuration.
/// </remarks>
public sealed class DaemonSettings : SyncSettings
{
    /// <summary>
    /// Gets or sets the synchronization interval in seconds. Overrides configuration file if specified. Defaults to 60 if not specified.
    /// Valid range: 1 to 86400 seconds (1 second to 24 hours).
    /// </summary>
    [CommandOption("-i|--interval <SECONDS>")]
    [Description("Sync interval in seconds (default: 60)")]
    public int? Interval { get; init; }

    /// <summary>
    /// Loads and merges configuration with daemon-specific interval setting.
    /// </summary>
    /// <returns>
    /// A merged <see cref="ConfigFile"/> with interval from CLI (if provided) taking precedence over file configuration.
    /// </returns>
    /// <remarks>
    /// Calls base <see cref="SyncSettings.GetEffectiveConfig"/> then overrides the interval if provided via CLI.
    /// </remarks>
    public new Cli.ConfigFile GetEffectiveConfig()
    {
        ConfigFile config = base.GetEffectiveConfig();

        // Override interval from CLI if provided
        if (Interval.HasValue)
        {
            config.Interval = Interval.Value;
        }

        return config;
    }

    /// <summary>
    /// Validates daemon-specific settings in addition to base synchronization settings.
    /// </summary>
    /// <returns>
    /// <see cref="ValidationResult.Success"/> if all settings are valid; otherwise an error result.
    /// </returns>
    /// <remarks>
    /// Validates interval is within the valid range (1 to 86400 seconds) after validating base settings.
    /// </remarks>
    public override ValidationResult Validate()
    {
        ValidationResult baseValidation = base.Validate();
        if (!baseValidation.Successful)
        {
            return baseValidation;
        }

        ConfigFile config = GetEffectiveConfig();
        int effectiveInterval = config.Interval ?? 60;

        PathValidation.ValidationError? intervalValidation = PathValidation.ValidateInterval(effectiveInterval);
        if (intervalValidation != null)
        {
            return ValidationResult.Error(intervalValidation.Message ?? "Invalid interval");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Executes continuous directory synchronization at regular intervals until cancelled.
/// </summary>
/// <remarks>
/// <para>Runs an infinite loop, performing synchronization every <see cref="DaemonSettings.Interval"/> seconds.</para>
/// <para>Handles Ctrl+C gracefully for clean shutdown. Individual sync cycle errors are logged but don't stop the daemon.</para>
/// <para>Progress reporting is less verbose than one-time sync (logs every 10 tasks instead of every task).</para>
/// </remarks>
public sealed class DaemonCommand : AsyncCommand<DaemonSettings>
{
    /// <summary>
    /// Executes the daemon command asynchronously.
    /// </summary>
    /// <param name="context">The command context provided by Spectre.Console.Cli (not used).</param>
    /// <param name="settings">The validated command settings containing source, replica, interval, and options.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (Ctrl+C or external cancellation).</param>
    /// <returns>
    /// A task that completes with an exit code from <see cref="ExitCodes"/>:
    /// <list type="bullet">
    /// <item><see cref="ExitCodes.Success"/> (0): Daemon stopped gracefully after cancellation.</item>
    /// <item><see cref="ExitCodes.Cancelled"/> (130): Operation cancelled by user (Ctrl+C).</item>
    /// <item><see cref="ExitCodes.DirectoryNotFound"/> (3): Source or replica deleted during operation (daemon stops).</item>
    /// <item><see cref="ExitCodes.PathValidationError"/> (2): Path validation failed at startup.</item>
    /// <item><see cref="ExitCodes.PermissionDenied"/> (4): Access denied at startup.</item>
    /// <item><see cref="ExitCodes.GeneralError"/> (1): Other startup errors.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Operation Loop:</strong></para>
    /// <list type="number">
    /// <item>Performs synchronization (validates paths each cycle for safety)</item>
    /// <item>Logs cycle results (created, updated, deleted counts and any failures)</item>
    /// <item>Waits for the configured interval</item>
    /// <item>Repeats until cancellation</item>
    /// </list>
    /// <para><strong>Error Handling:</strong></para>
    /// Individual sync cycle errors are logged but don't stop the daemon (allows recovery from transient issues).
    /// If source or replica is deleted, the daemon stops with <see cref="ExitCodes.DirectoryNotFound"/>.
    /// <para><strong>Cancellation:</strong></para>
    /// Registers a Ctrl+C handler for graceful shutdown. Cancellation is checked before each cycle and during the interval delay.
    /// <para><strong>Logging:</strong></para>
    /// Initializes Serilog at startup. Progress updates are less verbose than one-time sync (logged only every 10 tasks or on completion).
    /// </remarks>
    public override async Task<int> ExecuteAsync(CommandContext context, DaemonSettings settings, CancellationToken cancellationToken)
    {
        // Get effective configuration (merged from file + CLI args)
        ConfigFile config = settings.GetEffectiveConfig();
        int effectiveInterval = config.Interval ?? 60;

        Logger.Initialize(config.LogDirectory, settings.LogLevel);

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Console.CancelKeyPress += (s, e) =>
        {
            Log.Warning("Cancellation requested, stopping daemon...");
            cts.Cancel();
            e.Cancel = true;
        };

        try
        {
            Log.Information("Starting daemon mode with {Interval} second interval", effectiveInterval);

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

            int cycleCount = 0;
            while (!cts.Token.IsCancellationRequested)
            {
                cycleCount++;
                Log.Information("Starting sync cycle {Cycle}", cycleCount);

                try
                {
                    // Validate paths before each cycle
                    SyncEngine.ValidatePaths(options.Source, options.Replica);


                    // Progress reporting for daemon (log-based, less verbose)
                    var progress = new Progress<SyncProgress>(p =>
                    {
                        if (p.CompletedTasks % 10 == 0 || p.CurrentOperation == "Complete")
                        {
                            Log.Debug("Sync progress: {Completed}/{Total} tasks ({Percent:F1}%)",
                                p.CompletedTasks, p.TotalTasks, p.PercentComplete);
                        }
                    });

                    SyncResult result = await engine.SyncAsync(options, progress, cts.Token);

                    Log.Information("Cycle {Cycle} complete: {Created} created, {Updated} updated, {Deleted} deleted",
                        cycleCount, result.Created, result.Updated, result.Deleted);

                    if (result.Failures.Count > 0)
                    {
                        Log.Warning("Cycle {Cycle} had {FailureCount} failures", cycleCount, result.Failures.Count);
                        foreach (OperationFailure failure in result.Failures)
                        {
                            Log.Error("Failed {Action} on {Path}: {Error}",
                                failure.Action, failure.Path, failure.ErrorMessage);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (DirectoryNotFoundException ex)
                {
                    Log.Error(ex, "Source or replica directory no longer exists");
                    AnsiConsole.MarkupLine("[red]Error: Source or replica directory was deleted. Daemon stopping.[/]");
                    return ExitCodes.DirectoryNotFound;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Sync cycle {Cycle} failed", cycleCount);
                    AnsiConsole.MarkupLine($"[yellow]Error in cycle {cycleCount}. Retrying...[/]");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(effectiveInterval), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            Log.Information("Daemon stopped after {Cycles} cycles", cycleCount);
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Daemon cancelled by user[/]");
            Log.Warning("Daemon cancelled by user");
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
        catch (Exception ex)
        {
            Log.Error(ex, "Daemon failed");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.GeneralError;
        }
        finally
        {
            Logger.Close();
        }
    }
}
