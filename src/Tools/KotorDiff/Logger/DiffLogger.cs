// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:57-144
// Original: class DiffLogger: ...
using System;
using System.IO;

namespace KotorDiff.Logger
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:41-47
    // Original: class LogLevel(Enum): ...
    public enum LogLevel
    {
        DEBUG = 0,
        INFO = 1,
        WARNING = 2,
        ERROR = 3,
        CRITICAL = 4
    }

    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:50-54
    // Original: class OutputMode(Enum): ...
    public enum OutputMode
    {
        FULL,
        DIFF_ONLY,
        QUIET
    }

    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:57-144
    // Original: class DiffLogger: ...
    public class DiffLogger
    {
        private static DiffLogger _instance;
        private readonly LogLevel _level;
        private readonly OutputMode _outputMode;
        private readonly bool _useColors;
        private readonly TextWriter _outputFile;

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:60-97
        // Original: def __init__(...): ...
        public DiffLogger(
            LogLevel level = LogLevel.INFO,
            OutputMode outputMode = OutputMode.FULL,
            bool useColors = true,
            TextWriter outputFile = null)
        {
            _level = level;
            _outputMode = outputMode;
            _useColors = useColors;
            _outputFile = outputFile;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:99-102
        // Original: def debug(self, message: str, *args, **kwargs): ...
        public void Debug(string message, params object[] args)
        {
            if (_outputMode != OutputMode.DIFF_ONLY)
            {
                Log(LogLevel.DEBUG, message, args);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:104-107
        // Original: def info(self, message: str, *args, **kwargs): ...
        public void Info(string message, params object[] args)
        {
            if (_outputMode != OutputMode.DIFF_ONLY)
            {
                Log(LogLevel.INFO, message, args);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:109-112
        // Original: def warning(self, message: str, *args, **kwargs): ...
        public void Warning(string message, params object[] args)
        {
            if (_outputMode != OutputMode.QUIET)
            {
                Log(LogLevel.WARNING, message, args);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:114-116
        // Original: def error(self, message: str, *args, **kwargs): ...
        public void Error(string message, params object[] args)
        {
            Log(LogLevel.ERROR, message, args);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:118-120
        // Original: def critical(self, message: str, *args, **kwargs): ...
        public void Critical(string message, params object[] args)
        {
            Log(LogLevel.CRITICAL, message, args);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:122-127
        // Original: def diff_output(self, message: str, *args, **kwargs): ...
        public void DiffOutput(string message, params object[] args)
        {
            string formatted = args.Length > 0 ? string.Format(message, args) : message;
            Console.WriteLine(formatted);
            if (_outputFile != null)
            {
                _outputFile.WriteLine(formatted);
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:129-143
        // Original: def separator(...): ...
        public void Separator(string message, string separatorChar = "-", bool above = false, bool below = true)
        {
            string separatorLine = new string(separatorChar[0], message.Length);
            if (above)
            {
                DiffOutput(separatorLine);
            }
            DiffOutput(message);
            if (below)
            {
                DiffOutput(separatorLine);
            }
        }

        private void Log(LogLevel level, string message, object[] args)
        {
            if (level < _level)
            {
                return;
            }

            string formatted = args.Length > 0 ? string.Format(message, args) : message;
            string prefix = level.ToString().ToUpper();
            string output = $"{prefix}: {formatted}";

            if (_useColors)
            {
                // Color support can be added using System.Console.ForegroundColor if needed
                // TODO: STUB - For now, output is plain text for maximum compatibility
            }

            Console.WriteLine(output);
            if (_outputFile != null)
            {
                _outputFile.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {output}");
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:169-179
        // Original: def setup_logger(...): ...
        public static DiffLogger SetupLogger(
            LogLevel level = LogLevel.INFO,
            OutputMode outputMode = OutputMode.FULL,
            bool useColors = true,
            TextWriter outputFile = null)
        {
            _instance = new DiffLogger(level, outputMode, useColors, outputFile);
            return _instance;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/logger.py:182-186
        // Original: def get_logger() -> DiffLogger: ...
        public static DiffLogger GetLogger()
        {
            if (_instance == null)
            {
                return SetupLogger();
            }
            return _instance;
        }
    }
}

