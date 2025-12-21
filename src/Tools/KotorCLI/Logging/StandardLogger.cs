using System;

namespace KotorCLI.Logging
{
    /// <summary>
    /// Standard logger implementation with colored output support.
    /// </summary>
    public class StandardLogger : ILogger
    {
        private readonly bool _noColor;

        public StandardLogger(bool noColor = false)
        {
            _noColor = noColor;
        }

        public bool IsDebug => false;

        public void Debug(string message)
        {
            // Debug messages not shown in standard mode
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

