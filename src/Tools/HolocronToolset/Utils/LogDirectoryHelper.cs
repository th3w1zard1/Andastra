using System;
using System.IO;

namespace HolocronToolset.Utils
{
    /// <summary>
    /// Utility class for getting log directory paths.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/loggerplus/__init__.py:367-388
    /// </summary>
    public static class LogDirectoryHelper
    {
        /// <summary>
        /// Gets the log directory path, optionally with a subdirectory.
        /// Matching PyKotor implementation: get_log_directory(subdir)
        /// </summary>
        /// <param name="subdir">Optional subdirectory name (default: "logs")</param>
        /// <param name="extractPath">Optional extract path from settings</param>
        /// <returns>Path to the log directory</returns>
        public static string GetLogDirectory(string subdir = null, string extractPath = null)
        {
            if (string.IsNullOrEmpty(subdir))
            {
                subdir = "logs";
            }

            string basePath = string.IsNullOrEmpty(extractPath) ? Directory.GetCurrentDirectory() : extractPath;
            string logPath = Path.Combine(basePath, subdir);

            try
            {
                // Try to create the directory if it doesn't exist
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                // Verify we can write to the directory
                string testFile = Path.Combine(logPath, ".test");
                try
                {
                    File.WriteAllText(testFile, "");
                    File.Delete(testFile);
                }
                catch
                {
                    // If we can't write, fall back to current directory
                    logPath = Path.Combine(Directory.GetCurrentDirectory(), subdir);
                    if (!Directory.Exists(logPath))
                    {
                        Directory.CreateDirectory(logPath);
                    }
                }

                return logPath;
            }
            catch
            {
                // Final fallback: use current directory
                return Path.Combine(Directory.GetCurrentDirectory(), subdir);
            }
        }
    }
}

