using Serilog;

namespace DirectorySync.Service
{
    /// <summary>
    /// Provides static methods for logging messages at various severity levels. Wrapper around Serilog logging library.
    /// </summary>
    /// <remarks>This class acts as a centralized logging utility, allowing messages to be logged with
    /// different severity levels such as Information, Warning, Error, Debug, and Fatal. Logs are written to both the
    /// console and a rolling log file ("log.txt") with daily intervals.</remarks>
    internal static class Logger
    {
        private static ILogger? _logger;

        /// <summary>
        /// Initializes a logger service. Sets a log directory.
        /// </summary>
        /// <param name="logDir">Path to logging directory.</param>
        public static void Initialize(string logDir)
        {
            string logPath = Path.Combine(logDir, "log.txt");

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            try
            {
                _logger = new LoggerConfiguration()
                    .WriteTo.Console()
                    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Logger initialization: {ex.Message}");
                throw;
            }
        }

        public static void Information(string message) => _logger?.Information(message);

        public static void Warning(string message) => _logger?.Warning(message);

        public static void Error(string message) => _logger?.Error(message);

        public static void Debug(string message) => _logger?.Debug(message);

        public static void Fatal(string message) => _logger?.Fatal(message);
    }
}
