// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py
// Original: def log_output(*args, **kwargs): ... def visual_length(...): ... def log_output_with_separator(...): ... def diff_data_wrapper(...): ... def handle_diff_internal(...): ... def run_differ_from_args(...): ...
using System;
using System.IO;
using System.Text;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Mods;
using KotorDiff.NET.Diff;
using CSharpKOTOR.TSLPatcher;

namespace KotorDiff.NET.App
{
    /// <summary>
    /// Helper functions for diff application operations.
    /// 1:1 port of functions from vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py
    /// </summary>
    public static class DiffApplicationHelpers
    {
        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:136-145
        // Original: def visual_length(s: str, tab_length: int = 8) -> int:
        /// <summary>
        /// Calculate visual length of string accounting for tabs.
        /// </summary>
        public static int VisualLength(string s, int tabLength = 8)
        {
            if (!s.Contains("\t"))
            {
                return s.Length;
            }

            string[] parts = s.Split('\t');
            int visLength = 0;
            foreach (string part in parts)
            {
                visLength += part.Length;
            }

            for (int i = 0; i < parts.Length - 1; i++)
            {
                visLength += tabLength - (parts[i].Length % tabLength);
            }

            return visLength;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:82-133
        // Original: def log_output(*args, **kwargs):
        /// <summary>
        /// Log output to console and file.
        /// </summary>
        public static void LogOutput(string message, bool separator = false, bool separatorAbove = false)
        {
            // Handle separator logic
            if (separator || separatorAbove)
            {
                LogOutputWithSeparator(message, above: separatorAbove);
                return;
            }

            // Print to console with Unicode error handling
            try
            {
                Console.WriteLine(message);
            }
            catch (Exception)
            {
                // Fallback: encode with error handling for Windows console
                try
                {
                    Encoding encoding = Console.OutputEncoding ?? Encoding.UTF8;
                    byte[] bytes = encoding.GetBytes(message);
                    string safeMsg = encoding.GetString(bytes);
                    Console.WriteLine(safeMsg);
                }
                catch (Exception)
                {
                    // Last resort: use ASCII with backslashreplace
                    byte[] bytes = Encoding.ASCII.GetBytes(message);
                    string safeMsg = Encoding.ASCII.GetString(bytes);
                    Console.WriteLine(safeMsg);
                }
            }

            // Write to log file if enabled
            if (!GlobalConfig.Instance.LoggingEnabled.HasValue || !GlobalConfig.Instance.LoggingEnabled.Value || GlobalConfig.Instance.Config == null)
            {
                return;
            }

            if (GlobalConfig.Instance.OutputLog == null)
            {
                string chosenLogFilePath = "log_install_differ.log";
                GlobalConfig.Instance.OutputLog = new FileInfo(chosenLogFilePath);
                if (GlobalConfig.Instance.OutputLog.Directory != null && !GlobalConfig.Instance.OutputLog.Directory.Exists)
                {
                    // Keep trying until we get a valid path
                    while (true)
                    {
                        chosenLogFilePath = GlobalConfig.Instance.Config.OutputLogPath?.FullName ?? "log_install_differ.log";
                        GlobalConfig.Instance.OutputLog = new FileInfo(chosenLogFilePath);
                        if (GlobalConfig.Instance.OutputLog.Directory != null && GlobalConfig.Instance.OutputLog.Directory.Exists)
                        {
                            break;
                        }
                        Console.WriteLine($"Invalid path: {GlobalConfig.Instance.OutputLog}");
                    }
                }
            }

            // Write the message to the file (always use UTF-8 for file)
            try
            {
                File.AppendAllText(GlobalConfig.Instance.OutputLog.FullName, message + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                // Ignore file write errors
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:148-160
        // Original: def log_output_with_separator(...):
        /// <summary>
        /// Log output with separator lines.
        /// </summary>
        public static void LogOutputWithSeparator(string message, bool below = true, bool above = false, bool surround = false)
        {
            if (above || surround)
            {
                LogOutput(new string('-', VisualLength(message)));
            }
            LogOutput(message);
            if ((below && !above) || surround)
            {
                LogOutput(new string('-', VisualLength(message)));
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:163-193
        // Original: def diff_data_wrapper(...):
        /// <summary>
        /// Wrapper around DiffEngine.DiffData that passes global config.
        /// </summary>
        public static bool? DiffDataWrapper(
            byte[] data1,
            byte[] data2,
            DiffContext context,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (GlobalConfig.Instance.Config == null)
            {
                throw new InvalidOperationException("Global config config is None - must call run_application first");
            }

            Action<string> logFunc = (msg) =>
            {
                LogOutput(msg);
            };

            return DiffEngine.DiffData(
                data1,
                data2,
                context,
                compareHashes: GlobalConfig.Instance.Config.CompareHashes,
                modificationsByType: GlobalConfig.Instance.ModificationsByType,
                logFunc: logFunc,
                incrementalWriter: incrementalWriter);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:345-383
        // Original: def handle_diff_internal(...):
        /// <summary>
        /// Internal n-way diff handler that accepts arbitrary number of paths.
        /// </summary>
        public static (bool? comparison, int? exitCode) HandleDiffInternal(
            System.Collections.Generic.List<object> filesAndFoldersAndInstallations,
            System.Collections.Generic.List<string> filters = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (filesAndFoldersAndInstallations.Count < 2)
            {
                LogOutput("[ERROR] At least 2 paths required for comparison");
                return (null, 1);
            }

            // Use the new n-way implementation for all cases
            LogOutput($"[INFO] N-way comparison with {filesAndFoldersAndInstallations.Count} paths");
            bool? comparison = RunDifferFromArgs(
                filesAndFoldersAndInstallations,
                filters: filters,
                incrementalWriter: incrementalWriter);

            // Format output
            if (comparison == true)
            {
                LogOutput("\n[IDENTICAL] All paths are identical", separatorAbove: true);
                return (comparison, 0);
            }
            if (comparison == false)
            {
                LogOutput("\n[DIFFERENT] Differences found between paths", separatorAbove: true);
                return (comparison, 1);
            }

            // Error case
            LogOutput("\n[ERROR] Comparison failed or returned None", separatorAbove: true);
            return (comparison, 1);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:499-543
        // Original: def run_differ_from_args(...):
        /// <summary>
        /// Run n-way differ using global config.
        /// </summary>
        public static bool? RunDifferFromArgs(
            System.Collections.Generic.List<object> filesAndFoldersAndInstallations,
            System.Collections.Generic.List<string> filters = null,
            IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (GlobalConfig.Instance.Config == null)
            {
                throw new InvalidOperationException("Global config config is None - must call run_application first");
            }

            // Extract config values once for type safety
            bool compareHashesEnabled = GlobalConfig.Instance.Config.CompareHashes;

            Action<string> logFuncWrapper = (msg) =>
            {
                LogOutput(msg);
            };

            // Call the n-way implementation directly
            return DiffEngine.RunDifferFromArgsImpl(
                filesAndFoldersAndInstallations,
                filters: filters,
                logFunc: logFuncWrapper,
                compareHashes: compareHashesEnabled,
                modificationsByType: GlobalConfig.Instance.ModificationsByType,
                incrementalWriter: incrementalWriter);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:63-65
        // Original: def find_module_root(filename: str) -> str:
        /// <summary>
        /// Wrapper around DiffEngineUtils.GetModuleRoot for backwards compatibility.
        /// </summary>
        public static string FindModuleRoot(string filename)
        {
            return DiffEngineUtils.GetModuleRoot(filename);
        }
    }
}

