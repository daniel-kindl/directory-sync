using Serilog;

namespace DirectorySync.Cli;

/// <summary>
/// Adapts Serilog's static <see cref="Log"/> API to the <see cref="Engine.ILogger"/> interface.
/// </summary>
/// <remarks>
/// <para>This adapter allows the engine layer to remain independent of Serilog.</para>
/// <para>All log calls are forwarded to Serilog's global <see cref="Log"/> instance.</para>
/// <para>Thread-safe: Serilog's global logger is thread-safe.</para>
/// </remarks>
public class SerilogAdapter : DirectorySync.Engine.ILogger
{
    /// <summary>
    /// Logs a debug-level message to Serilog.
    /// </summary>
    /// <param name="message">The message template with structured logging placeholders.</param>
    /// <param name="args">Values for the message template placeholders.</param>
    public void LogDebug(string message, params object[] args)
        => Log.Debug(message, args);

    /// <summary>
    /// Logs an informational message to Serilog.
    /// </summary>
    /// <param name="message">The message template with structured logging placeholders.</param>
    /// <param name="args">Values for the message template placeholders.</param>
    public void LogInformation(string message, params object[] args)
        => Log.Information(message, args);

    /// <summary>
    /// Logs a warning message to Serilog.
    /// </summary>
    /// <param name="message">The message template with structured logging placeholders.</param>
    /// <param name="args">Values for the message template placeholders.</param>
    public void LogWarning(string message, params object[] args)
        => Log.Warning(message, args);

    /// <summary>
    /// Logs an error message with optional exception to Serilog.
    /// </summary>
    /// <param name="exception">The exception that caused the error, or null if not applicable.</param>
    /// <param name="message">The message template with structured logging placeholders.</param>
    /// <param name="args">Values for the message template placeholders.</param>
    public void LogError(Exception? exception, string message, params object[] args)
        => Log.Error(exception, message, args);
}
