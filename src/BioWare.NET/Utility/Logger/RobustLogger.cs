using System;
using System.IO;
using JetBrains.Annotations;

namespace BioWare.NET.Utility.Logger
{
    /// <summary>
    /// Robust logger for pykotor errors, exceptions, warnings, and info logging.
    /// Matches Python's RobustLogger functionality from loggerplus.
    /// All logs are written to file regardless of log level.
    /// </summary>
    public class RobustLogger
    {
        /// <summary>
        /// Static instance for convenience access.
        /// </summary>
        public static RobustLogger Instance { get; } = new RobustLogger();

        private string logFilePath;
        private readonly object lockObject = new object();

        /// <summary>
        /// Initialize RobustLogger with optional log file path.
        /// If path is null, logs will only go to console/debug output.
        /// </summary>
        public RobustLogger([CanBeNull] string fileLogPath = null)
        {
            logFilePath = fileLogPath;
        }

        /// <summary>
        /// Sets the log file path. Useful for updating when mod path changes.
        /// </summary>
        public void SetLogFilePath([CanBeNull] string fileLogPath)
        {
            lock (lockObject)
            {
                logFilePath = fileLogPath;
            }
        }

        /// <summary>
        /// Log a debug message.
        /// </summary>
        public void Debug(
            string message,
            bool excInfo = false,
            Exception exception = null)
        {
            string logMessage = FormatMessage("DEBUG", message, excInfo, exception);
            WriteToFile(logMessage);
            global::System.Diagnostics.Debug.WriteLine(logMessage);
        }

        /// <summary>
        /// Log an info message.
        /// </summary>
        public void Info(
            string message,
            bool excInfo = false,
            Exception exception = null)
        {
            string logMessage = FormatMessage("INFO", message, excInfo, exception);
            WriteToFile(logMessage);
            global::System.Diagnostics.Debug.WriteLine(logMessage);
        }

        /// <summary>
        /// Log a warning message.
        /// </summary>
        public void Warning(
            string message,
            bool excInfo = false,
            Exception exception = null)
        {
            string logMessage = FormatMessage("WARNING", message, excInfo, exception);
            WriteToFile(logMessage);
            global::System.Diagnostics.Debug.WriteLine(logMessage);
            Console.WriteLine(logMessage);
        }

        /// <summary>
        /// Log an error message.
        /// </summary>
        public void Error(
            string message,
            bool excInfo = false,
            Exception exception = null)
        {
            string logMessage = FormatMessage("ERROR", message, excInfo, exception);
            WriteToFile(logMessage);
            global::System.Diagnostics.Debug.WriteLine(logMessage);
            Console.Error.WriteLine(logMessage);
        }

        /// <summary>
        /// Log a critical message.
        /// </summary>
        public void Critical(string message, bool excInfo = false, Exception exception = null)
        {
            string logMessage = FormatMessage("CRITICAL", message, excInfo, exception);
            WriteToFile(logMessage);
            global::System.Diagnostics.Debug.WriteLine(logMessage);
            Console.Error.WriteLine(logMessage);
        }

        /// <summary>
        /// Log an exception with full stack trace.
        /// </summary>
        public void Exception(string message, Exception exception = null)
        {
            string logMessage = FormatMessage("EXCEPTION", message, true, exception);
            WriteToFile(logMessage);
            global::System.Diagnostics.Debug.WriteLine(logMessage);
            Console.Error.WriteLine(logMessage);
        }

        private string FormatMessage(
            string level,
            string message,
            bool excInfo,
            Exception exception)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string formatted = $"[{timestamp}] [{level}] {message}";

            Exception exc = exception;

            if (exc != null)
            {
                formatted += $"{Environment.NewLine}{exc}";
            }

            return formatted;
        }

        private void WriteToFile(string message)
        {
            if (string.IsNullOrEmpty(logFilePath))
            {
                return;
            }

            try
            {
                lock (lockObject)
                {
                    string directory = Path.GetDirectoryName(logFilePath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.AppendAllText(logFilePath, message + Environment.NewLine, global::System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Don't throw - logging failures shouldn't crash the app
                global::System.Diagnostics.Debug.WriteLine($"Failed to write to log file '{logFilePath}': {ex.Message}");
            }
        }
    }
}
