using System.ComponentModel;
using DirectorySync.Engine;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace DirectorySync.Cli.Commands;

/// <summary>
/// Command settings for verification operations.
/// </summary>
/// <remarks>
/// Simpler than <see cref="SyncSettings"/>: only requires source and replica paths, no sync options.
/// All properties use init-only setters for immutability.
/// </remarks>
public sealed class VerifySettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the source directory path to verify against. Required.
    /// </summary>
    [CommandOption("-s|--source <PATH>")]
    [Description("Source directory path")]
    public required string Source { get; init; }

    /// <summary>
    /// Gets or sets the replica directory path to verify. Required.
    /// </summary>
    [CommandOption("-r|--replica <PATH>")]
    [Description("Replica directory path")]
    public required string Replica { get; init; }

    /// <summary>
    /// Gets or sets the log directory path. If not specified, logs are written to the current directory.
    /// </summary>
    [CommandOption("--log-dir <PATH>")]
    [Description("Log directory path")]
    public string? LogDir { get; init; }

    /// <summary>
    /// Validates that source and replica paths are provided.
    /// </summary>
    /// <returns>
    /// <see cref="ValidationResult.Success"/> if both paths are non-empty; otherwise an error result.
    /// </returns>
    /// <remarks>
    /// Note: The <c>required</c> keyword on properties provides compile-time safety, but this method provides
    /// runtime validation for the CLI framework. Does not validate path format or existence.
    /// </remarks>
    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Source))
        {
            return ValidationResult.Error("Source path is required");
        }

        if (string.IsNullOrWhiteSpace(Replica))
        {
            return ValidationResult.Error("Replica path is required");
        }

        return ValidationResult.Success();
    }
}

/// <summary>
/// Executes verification to check if the replica exactly matches the source.
/// </summary>
/// <remarks>
/// <para>Scans both directories, computes checksums, and compares all files and directories for exact match.</para>
/// <para>Returns <see cref="ExitCodes.Success"/> if they match, <see cref="ExitCodes.VerifyMismatch"/> if they differ.</para>
/// <para>Does not report which items differ; only returns a pass/fail result.</para>
/// </remarks>
public sealed class VerifyCommand : Command<VerifySettings>
{
    /// <summary>
    /// Executes the verification command.
    /// </summary>
    /// <param name="context">The command context provided by Spectre.Console.Cli (not used).</param>
    /// <param name="settings">The validated command settings containing source and replica paths.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests (Ctrl+C).</param>
    /// <returns>
    /// An exit code from <see cref="ExitCodes"/>:
    /// <list type="bullet">
    /// <item><see cref="ExitCodes.Success"/> (0): Replica matches source exactly.</item>
    /// <item><see cref="ExitCodes.VerifyMismatch"/> (20): Replica does not match source (differences exist).</item>
    /// <item><see cref="ExitCodes.Cancelled"/> (130): Operation canceled by user.</item>
    /// <item><see cref="ExitCodes.PathValidationError"/> (2): Path validation failed.</item>
    /// <item><see cref="ExitCodes.DirectoryNotFound"/> (3): Source or replica directory not found.</item>
    /// <item><see cref="ExitCodes.PermissionDenied"/> (4): Access denied.</item>
    /// <item><see cref="ExitCodes.GeneralError"/> (1): Other errors.</item>
    /// </list>
    /// </returns>
    /// <remarks>
    /// <para><strong>Verification Criteria:</strong></para>
    /// <list type="bullet">
    /// <item>Same number of items in both directories</item>
    /// <item>Every item in source exists in replica with the same relative path</item>
    /// <item>Each item has the same type (file vs. directory)</item>
    /// <item>For files: identical size and SHA-256 checksum</item>
    /// </list>
    /// <para><strong>Output:</strong></para>
    /// Displays a green checkmark if verification passes, or a red X if it fails.
    /// <para><strong>Performance:</strong></para>
    /// Always computes checksums for all files regardless of timestamps (thorough but potentially slow).
    /// Stops at the first mismatch found (does not enumerate all differences).
    /// </remarks>
    public override int Execute(CommandContext context, VerifySettings settings, CancellationToken cancellationToken)
    {
        Logger.Initialize(settings.LogDir);

        try
        {
            Log.Information("Verifying replica matches source");

            var engine = new SyncEngine(logger: new SerilogAdapter());
            bool matches = engine.Verify(settings.Source, settings.Replica, cancellationToken);

            if (matches)
            {
                AnsiConsole.MarkupLine("[green]✓ Replica matches source[/]");
                Log.Information("Verification passed");
                return ExitCodes.Success;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Replica does not match source[/]");
                Log.Warning("Verification failed");
                return ExitCodes.VerifyMismatch;
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[yellow]Verification cancelled by user[/]");
            Log.Warning("Verification cancelled by user");
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
            Log.Error(ex, "Verification failed");
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return ExitCodes.GeneralError;
        }
        finally
        {
            Logger.Close();
        }
    }
}
