using System;

namespace KotorCLI.Logging
{
    /// <summary>
    /// Quiet logger that only shows errors.
    /// </summary>
    public class QuietLogger : ILogger
    {
        public bool IsDebug => false;

        public void Debug(string message)
        {
            // No output in quiet mode
        }

        public void Info(string message)
        {
            // No output in quiet mode
        }

        public void Warning(string message)
        {
            // No output in quiet mode
        }

        public void Error(string message)
        {
            Console.Error.WriteLine($"Error: {message}");
        }

        public void Critical(string message)
        {
            Console.Error.WriteLine($"Critical: {message}");
        }
    }
}

