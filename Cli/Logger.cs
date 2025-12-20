using Serilog;
using Serilog.Events;

namespace DirectorySync.Cli;

/// <summary>
/// Provides centralized logging initialization and cleanup using Serilog.
/// </summary>
/// <remarks>
/// Configures dual logging to console and rolling file with configurable log level and retention.
/// All methods are static and manage the global Serilog <see cref="Log.Logger"/> instance.
/// </remarks>
public static class Logger
{
    private const int _logRetentionDays = 7;

    /// <summary>
    /// Initializes the global Serilog logger with console and file sinks.
    /// </summary>
    /// <param name="logDir">
    /// Directory path for log files. If null, logs are written to the current directory.
    /// The directory is created automatically if it doesn't exist.
    /// </param>
    /// <param name="level">Minimum log level to capture. Default is <see cref="LogEventLevel.Information"/>.</param>
    /// <remarks>
    /// <para><strong>Log File Configuration:</strong></para>
    /// <list type="bullet">
    /// <item>File name: <c>dirsync.log</c> (in <paramref name="logDir"/> or current directory)</item>
    /// <item>Rolling: Daily (creates new file per day with timestamp suffix)</item>
    /// <item>Retention: Last 7 days of logs are kept; older files are deleted automatically</item>
    /// </list>
    /// <para><strong>Thread-Safety:</strong></para>
    /// This method sets the global <see cref="Log.Logger"/> and is not thread-safe for concurrent calls.
    /// Should be called once at application startup before any logging occurs.
    /// <para><strong>Cleanup:</strong></para>
    /// Call <see cref="Close"/> before application exit to ensure all buffered log entries are flushed.
    /// </remarks>
    public static void Initialize(string? logDir, LogEventLevel level = LogEventLevel.Information)
    {
        string logPath = Path.Combine(logDir ?? Environment.CurrentDirectory, "dirsync.log");

        if (logDir != null && !Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.Console()
            .WriteTo.File(logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: _logRetentionDays)
            .CreateLogger();
    }

    /// <summary>
    /// Closes and flushes the global Serilog logger, ensuring all buffered log entries are written.
    /// </summary>
    /// <remarks>
    /// <para>Should be called before application exit, typically in a <c>finally</c> block.</para>
    /// <para>Blocks until all pending log entries are flushed to their targets (console, files).</para>
    /// <para>After calling this method, the global <see cref="Log.Logger"/> is disposed and should not be used.</para>
    /// <para>Safe to call multiple times; subsequent calls have no effect.</para>
    /// </remarks>
    public static void Close()
    {
        Log.CloseAndFlush();
    }
}
