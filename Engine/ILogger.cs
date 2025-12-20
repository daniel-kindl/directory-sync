namespace DirectorySync.Engine;

/// <summary>
/// Defines a logging abstraction for the synchronization engine.
/// </summary>
/// <remarks>
/// This interface allows the engine to remain agnostic of specific logging frameworks.
/// Implementations should be thread-safe as methods may be called from multiple threads.
/// </remarks>
public interface ILogger
{
    /// <summary>
    /// Logs a debug-level message with optional format arguments.
    /// </summary>
    /// <param name="message">The message template with placeholders in structured logging format (e.g., "Processing {FileName}").</param>
    /// <param name="args">Values to substitute into the message template placeholders.</param>
    void LogDebug(string message, params object[] args);

    /// <summary>
    /// Logs an informational message with optional format arguments.
    /// </summary>
    /// <param name="message">The message template with placeholders in structured logging format.</param>
    /// <param name="args">Values to substitute into the message template placeholders.</param>
    void LogInformation(string message, params object[] args);

    /// <summary>
    /// Logs a warning message with optional format arguments.
    /// </summary>
    /// <param name="message">The message template with placeholders in structured logging format.</param>
    /// <param name="args">Values to substitute into the message template placeholders.</param>
    void LogWarning(string message, params object[] args);

    /// <summary>
    /// Logs an error message with optional exception and format arguments.
    /// </summary>
    /// <param name="exception">The exception that caused the error, or null if not applicable.</param>
    /// <param name="message">The message template with placeholders in structured logging format.</param>
    /// <param name="args">Values to substitute into the message template placeholders.</param>
    void LogError(Exception? exception, string message, params object[] args);
}

/// <summary>
/// A no-op logger implementation that discards all log messages.
/// </summary>
/// <remarks>
/// Used as the default logger when no logging is configured. Thread-safe and stateless.
/// </remarks>
public class NullLogger : ILogger
{
    /// <summary>
    /// Discards the debug message.
    /// </summary>
    public void LogDebug(string message, params object[] args) { }

    /// <summary>
    /// Discards the informational message.
    /// </summary>
    public void LogInformation(string message, params object[] args) { }

    /// <summary>
    /// Discards the warning message.
    /// </summary>
    public void LogWarning(string message, params object[] args) { }

    /// <summary>
    /// Discards the error message.
    /// </summary>
    public void LogError(Exception? exception, string message, params object[] args) { }
}
