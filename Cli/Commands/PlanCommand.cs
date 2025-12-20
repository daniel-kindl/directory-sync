using DirectorySync.Engine;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DirectorySync.Cli.Commands;

/// <summary>
/// Executes a dry-run synchronization to preview changes without modifying the replica.
/// </summary>
/// <remarks>
/// Displays a table of planned operations (creates, updates, deletes) but does not execute them.
/// Useful for previewing what a sync operation would do before committing to changes.
/// </remarks>
public sealed class PlanCommand : Command<SyncSettings>
{
    /// <summary>
    /// Executes the plan command to preview synchronization changes.
    /// </summary>
    /// <param name="context">The command context provided by Spectre.Console.Cli (not used).</param>
    /// <param name="settings">The validated command settings containing source, replica, and options.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (Ctrl+C).</param>
    /// <returns>
    /// An exit code from <see cref="ExitCodes"/>:
    /// <list type="bullet">
    /// <item><see cref="ExitCodes.Success"/> (0): Planning completed (whether changes are needed or not).</item>
    /// <item><see cref="ExitCodes.Cancelled"/> (130): Operation canceled by user.</item>
    /// <item><see cref="ExitCodes.PathValidationError"/> (2): Path validation failed.</item>
    /// <item><see cref="ExitCodes.DirectoryNotFound"/> (3): Source directory not found.</item>
    /// <item><see cref="ExitCodes.PermissionDenied"/> (4): Access denied.</item>
    /// <item><see cref="ExitCodes.GeneralError"/> (1): Other errors.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Output:</strong></para>
    /// If no changes are needed, displays a green checkmark message.
    /// Otherwise, displays a table with planned operations (action type and destination path).
    /// <para><strong>Behavior:</strong></para>
    /// Scans source and replica directories, creates a sync plan, but does not execute any file system modifications.
    /// The <see cref="SyncOptions.DryRun"/> flag is implicitly set to true.
    /// <para><strong>Performance:</strong></para>
    /// Performs full directory scanning and checksum computation (if enabled), so may take time for large directories.
    /// </remarks>
    public override int Execute(CommandContext context, SyncSettings settings, CancellationToken cancellationToken)
    {
        Logger.Initialize(settings.LogDir, settings.LogLevel);

        try
        {
            // Get effective configuration
            ConfigFile config = settings.GetEffectiveConfig();

            Log.Information("Planning synchronization");
            Log.Information("Source: {Source}", config.Source);
            Log.Information("Replica: {Replica}", config.Replica);

            var engine = new SyncEngine(
                useChecksum: config.UseChecksum ?? true,
                useTimestampOptimization: config.UseTimestampOptimization ?? true,
                logger: new SerilogAdapter(),
                timestampTolerance: TimeSpan.FromSeconds(config.TimestampTolerance ?? 2.0));

            var options = new SyncOptions
            {
                Source = config.Source!,
                Replica = config.Replica!,
                DryRun = true,
                AllowDelete = config.AllowDelete ?? true,
                UseChecksum = config.UseChecksum ?? true
            };

            List<SyncTask> tasks = engine.Plan(options, cancellationToken);

            if (tasks.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Replica is up to date");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]Found {tasks.Count} operations needed:[/]");
            AnsiConsole.WriteLine();

            var table = new Table();
            table.AddColumn("Action");
            table.AddColumn("Path");

            foreach (SyncTask task in tasks)
            {
                string action = task.Action switch
                {
                    SyncAction.CreateDirectory => "[green]Create Dir[/]",
                    SyncAction.DeleteDirectory => "[red]Delete Dir[/]",
                    SyncAction.CopyFile => "[green]Copy File[/]",
                    SyncAction.UpdateFile => "[yellow]Update File[/]",
                    SyncAction.DeleteFile => "[red]Delete File[/]",
                    _ => "Unknown"
                };

                table.AddRow(action, task.DestinationPath);
            }

            AnsiConsole.Write(table);

            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Planning cancelled by user[/]");
            Log.Warning("Planning cancelled by user");
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
            Log.Error(ex, "Planning failed");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.GeneralError;
        }
        finally
        {
            Logger.Close();
        }
    }
}
