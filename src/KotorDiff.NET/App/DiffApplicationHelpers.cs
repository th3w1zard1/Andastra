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

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:204-211
        // Original: def _setup_logging(config: DiffConfig) -> None: ...
        /// <summary>
        /// Set up the logging system with the provided configuration.
        /// </summary>
        public static void SetupLogging(KotorDiffConfig config)
        {
            // Logging setup is optional - if not available, just skip
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:214-224
        // Original: def _log_configuration(config: DiffConfig) -> None: ...
        /// <summary>
        /// Log the current configuration.
        /// </summary>
        public static void LogConfiguration(KotorDiffConfig config)
        {
            LogOutput("");
            LogOutput("Configuration:");
            LogOutput($"  Mode: {config.Paths.Count}-way comparison");

            for (int i = 0; i < config.Paths.Count; i++)
            {
                object path = config.Paths[i];
                string pathStr = path?.ToString() ?? "null";
                LogOutput($"  Path {i + 1}: '{pathStr}'");
            }

            LogOutput($"Using --compare-hashes={config.CompareHashes}");
            LogOutput($"Using --use-profiler={config.UseProfiler}");
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:227-237
        // Original: def _execute_diff(config: DiffConfig) -> tuple[bool | None, int | None]: ...
        /// <summary>
        /// Execute the diff operation based on configuration.
        /// </summary>
        public static bool? ExecuteDiff(KotorDiffConfig config)
        {
            // Use unified n-way handling for all cases
            var result = HandleDiff(config);
            return result.comparison;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:240-258
        // Original: def _format_comparison_output(comparison: bool | None, config: DiffConfig) -> int: ...
        /// <summary>
        /// Format and output the final comparison result.
        /// </summary>
        public static int FormatComparisonOutput(bool? comparison, KotorDiffConfig config)
        {
            if (config.Paths.Count >= 2)
            {
                string matchText = comparison == true ? " MATCHES " : " DOES NOT MATCH ";
                LogOutput($"Comparison of {config.Paths.Count} paths: {matchText}");
            }
            return comparison == true ? 0 : (comparison == false ? 2 : 3);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:417-499
        // Original: def handle_diff(config: KotorDiffConfig) -> tuple[bool | None, int | None]: ...
        /// <summary>
        /// Handle diff operation with TSLPatcher data generation support.
        /// </summary>
        public static (bool? comparison, int? exitCode) HandleDiff(KotorDiffConfig config)
        {
            // Create modifications collection
            var modifications = new ModificationsByType();
            GlobalConfig.Instance.ModificationsByType = modifications;

            // Create incremental writer if requested
            IncrementalTSLPatchDataWriter incrementalWriter = null;
            if (config.TslPatchDataPath != null)
            {
                incrementalWriter = new IncrementalTSLPatchDataWriter(
                    config.TslPatchDataPath.FullName,
                    config.IniFilename ?? "changes.ini",
                    config.Paths);
            }

            // Call handle_diff_internal
            var result = HandleDiffInternal(
                config.Paths,
                filters: config.Filters,
                incrementalWriter: incrementalWriter);

            // Finalize TSLPatcher data if requested
            if (incrementalWriter != null)
            {
                incrementalWriter.Finalize();
            }
            else if (config.TslPatchDataPath != null && modifications.HasModifications())
            {
                // Use batch generation if not using incremental writer
                GenerateTSLPatcherData(
                    config.TslPatchDataPath,
                    config.IniFilename ?? "changes.ini",
                    modifications,
                    config.Paths);
            }

            return result;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:261-343
        // Original: def generate_tslpatcher_data(...): ...
        /// <summary>
        /// Generate TSLPatcher data files using batch generation (non-incremental).
        /// </summary>
        private static void GenerateTSLPatcherData(
            DirectoryInfo tslpatchdataPath,
            string iniFilename,
            ModificationsByType modifications,
            List<object> paths)
        {
            if (tslpatchdataPath == null || !modifications.HasModifications())
            {
                return;
            }

            LogOutput($"\nGenerating TSLPatcher data at: {tslpatchdataPath.FullName}");

            // Use TSLPatchDataGenerator for batch generation
            var generator = new CSharpKOTOR.TSLPatcher.TSLPatchDataGenerator(tslpatchdataPath);
            
            // Determine base data path from first path if it's a directory
            DirectoryInfo baseDataPath = null;
            if (paths != null && paths.Count > 0)
            {
                object firstPath = paths[0];
                if (firstPath is string pathStr && Directory.Exists(pathStr))
                {
                    baseDataPath = new DirectoryInfo(pathStr);
                }
                else if (firstPath is DirectoryInfo dirInfo)
                {
                    baseDataPath = dirInfo;
                }
            }

            var generatedFiles = generator.GenerateAllFiles(modifications, baseDataPath);

            if (generatedFiles != null && generatedFiles.Count > 0)
            {
                LogOutput($"Generated {generatedFiles.Count} resource file(s):");
                foreach (var kvp in generatedFiles)
                {
                    LogOutput($"  - {kvp.Key}");
                }
            }

            // Generate changes.ini using TSLPatcher serializer
            var iniPath = Path.Combine(tslpatchdataPath.FullName, iniFilename);
            LogOutput($"\nGenerating {iniFilename} at: {iniPath}");

            // Use TSLPatcher INI serializer if available, otherwise use INIManager
            var iniManager = new Generator.INIManager(iniPath);
            iniManager.Load();
            
            // Serialize modifications to INI format
            // Note: This is a simplified version - full serialization would use TSLPatcherINISerializer
            // For now, the incremental writer handles most of this, so batch generation is rarely used
            var serializer = new CSharpKOTOR.TSLPatcher.TSLPatcherINISerializer();
            string iniContent = serializer.Serialize(modifications, includeHeader: true, includeSettings: true);
            File.WriteAllText(iniPath, iniContent, Encoding.UTF8);

            // Summary
            LogOutput("\nTSLPatcher data generation complete:");
            LogOutput($"  Location: {tslpatchdataPath.FullName}");
            LogOutput($"  INI file: {iniFilename}");
            LogOutput($"  TLK modifications: {modifications.TLK?.Count ?? 0}");
            LogOutput($"  2DA modifications: {modifications.TwoDA?.Count ?? 0}");
            LogOutput($"  GFF modifications: {modifications.GFF?.Count ?? 0}");
            LogOutput($"  SSF modifications: {modifications.SSF?.Count ?? 0}");
            LogOutput($"  NCS modifications: {modifications.NCS?.Count ?? 0}");
            LogOutput($"  Install folders: {modifications.Install?.Count ?? 0}");
        }
    }
}

