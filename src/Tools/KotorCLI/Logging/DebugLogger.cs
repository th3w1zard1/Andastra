using System;

namespace KotorCLI.Logging
{
    /// <summary>
    /// Debug logger that shows all messages including debug level.
    /// </summary>
    public class DebugLogger : ILogger
    {
        private readonly bool _noColor;

        public DebugLogger(bool noColor = false)
        {
            _noColor = noColor;
        }

        public bool IsDebug => true;

        public void Debug(string message)
        {
            if (_noColor)
            {
                Console.WriteLine($"[DEBUG] {message}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[DEBUG] {message}");
                Console.ResetColor();
            }
        }

        public void Info(string message)
        {
            if (_noColor)
            {
                Console.WriteLine(message);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        public void Warning(string message)
        {
            if (_noColor)
            {
                Console.WriteLine($"Warning: {message}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Warning: {message}");
                Console.ResetColor();
            }
        }

        public void Error(string message)
        {
            if (_noColor)
            {
                Console.Error.WriteLine($"Error: {message}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error: {message}");
                Console.ResetColor();
            }
        }

        public void Critical(string message)
        {
            if (_noColor)
            {
                Console.Error.WriteLine($"Critical: {message}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = ConsoleColor.Yellow;
                Console.Error.WriteLine($"Critical: {message}");
                Console.ResetColor();
            }
        }
    }
}

