using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace HolocronToolset.NET
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_init.py:151
    // Original: def main_init():
    public static class MainInit
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_init.py:18-27
        // Original: def is_frozen() -> bool:
        public static bool IsFrozen()
        {
            string entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(entryAssembly))
            {
                return false;
            }
            // Check if running from a single-file executable
            return !File.Exists(entryAssembly);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/misc.py:172-182
        // Original: def is_debug_mode() -> bool:
        /// <summary>
        /// Determines if the application is running in debug mode.
        /// Checks for debugger attachment, environment variables, and frozen state.
        /// </summary>
        /// <returns>True if debug mode is enabled, false otherwise.</returns>
        public static bool IsDebugMode()
        {
            bool ret = false;

            // Check for DEBUG_MODE environment variable (equivalent to Python's DEBUG_MODE)
            string debugMode = Environment.GetEnvironmentVariable("DEBUG_MODE");
            if (debugMode == "1")
            {
                ret = true;
            }

            // Check if debugger is attached (equivalent to Python's sys.gettrace)
            if (Debugger.IsAttached)
            {
                ret = true;
            }

            // If frozen, disable debug mode (equivalent to Python's sys.frozen or sys._MEIPASS check)
            if (IsFrozen())
            {
                ret = false;
            }

            return ret;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_init.py:30-44
        // Original: def on_app_crash(etype, exc, tback):
        public static void OnAppCrash(Exception exception)
        {
            if (exception is System.Threading.ThreadAbortException)
            {
                return;
            }
            // TODO: SIMPLIFIED - Log the exception - in a real implementation, this would use a logger
            Console.Error.WriteLine($"Uncaught exception: {exception}");
            Console.Error.WriteLine(exception.StackTrace);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_init.py:147-148
        // Original: def is_running_from_temp() -> bool:
        public static bool IsRunningFromTemp()
        {
            string entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(entryAssembly))
            {
                return false;
            }
            string tempPath = Path.GetTempPath();
            return entryAssembly.StartsWith(tempPath, StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_init.py:151-185
        // Original: def main_init():
        public static void Initialize()
        {
            // Set up exception handling
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    OnAppCrash(ex);
                }
            };

            // Check if running from temp directory
            if (IsRunningFromTemp())
            {
                throw new InvalidOperationException(
                    "This application cannot be run from within a zip or temporary directory. " +
                    "Please extract it to a permanent location before running.");
            }
        }
    }
}
