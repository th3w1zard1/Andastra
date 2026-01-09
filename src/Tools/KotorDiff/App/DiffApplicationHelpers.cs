// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py
// Original: def log_output(*args, **kwargs): ... def visual_length(...): ... def log_output_with_separator(...): ... def diff_data_wrapper(...): ... def handle_diff_internal(...): ... def run_differ_from_args(...): ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Mods;
using Andastra.Parsing.Tools;
using Andastra.Utility;
using KotorDiff.Diff;
using Andastra.Parsing.TSLPatcher;
using Tuple = System.Tuple;
using SystemTextEncoding = System.Text.Encoding;
using Game = Andastra.Parsing.Common.BioWareGame;

namespace KotorDiff.AppCore
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
                    SystemTextEncoding encoding = Console.OutputEncoding ?? SystemTextEncoding.UTF8;
                    byte[] bytes = encoding.GetBytes(message);
                    string safeMsg = encoding.GetString(bytes);
                    Console.WriteLine(safeMsg);
                }
                catch (Exception)
                {
                    // Last resort: use ASCII with backslashreplace
                    byte[] bytes = SystemTextEncoding.ASCII.GetBytes(message);
                    string safeMsg = SystemTextEncoding.ASCII.GetString(bytes);
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
                File.AppendAllText(GlobalConfig.Instance.OutputLog.FullName, message + Environment.NewLine, SystemTextEncoding.UTF8);
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
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (GlobalConfig.Instance.Config == null)
            {
                throw new InvalidOperationException("Global config config is None - must call run_application first");
            }

            Action<string> logFunc = CreateConditionalLogFunc();

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
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (filesAndFoldersAndInstallations.Count < 2)
            {
                LogOutput("[ERROR] At least 2 paths required for comparison");
                return (null, 1);
            }

            bool isVerbose = GlobalConfig.Instance.Config?.Verbose == true || GlobalConfig.Instance.Config?.Debug == true;

            // Use the new n-way implementation for all cases
            if (isVerbose)
            {
                LogOutput($"[INFO] N-way comparison with {filesAndFoldersAndInstallations.Count} paths");
            }
            bool? comparison = RunDifferFromArgs(
                filesAndFoldersAndInstallations,
                filters: filters,
                incrementalWriter: incrementalWriter);

            // Format output - only show verbose messages if verbose flag is set
            if (isVerbose)
            {
                if (comparison == true)
                {
                    LogOutput("\n[IDENTICAL] All paths are identical", separatorAbove: true);
                }
                else if (comparison == false)
                {
                    LogOutput("\n[DIFFERENT] Differences found between paths", separatorAbove: true);
                }
                else
                {
                    LogOutput("\n[ERROR] Comparison failed or returned None", separatorAbove: true);
                }
            }

            return (comparison, comparison == true ? 0 : (comparison == false ? 1 : 1));
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/application.py:499-543
        // Original: def run_differ_from_args(...):
        /// <summary>
        /// Run n-way differ using global config.
        /// </summary>
        public static bool? RunDifferFromArgs(
            System.Collections.Generic.List<object> filesAndFoldersAndInstallations,
            System.Collections.Generic.List<string> filters = null,
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter = null)
        {
            if (GlobalConfig.Instance.Config == null)
            {
                throw new InvalidOperationException("Global config config is None - must call run_application first");
            }

            // Extract config values once for type safety
            bool compareHashesEnabled = GlobalConfig.Instance.Config.CompareHashes;

            Action<string> logFuncWrapper = CreateConditionalLogFunc();

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

        /// <summary>
        /// Create a conditional log function that only outputs verbose messages (tslpatchdata, INSTALL, etc.)
        /// when verbose flag is set OR when TslPatchDataPath is configured.
        /// </summary>
        private static Action<string> CreateConditionalLogFunc()
        {
            if (GlobalConfig.Instance.Config == null)
            {
                return LogOutput;
            }

            bool isVerbose = GlobalConfig.Instance.Config.Verbose || GlobalConfig.Instance.Config.Debug;
            bool hasTslPatchData = GlobalConfig.Instance.Config.TslPatchDataPath != null;
            bool shouldShowVerbose = isVerbose || hasTslPatchData;

            return (msg) =>
            {
                // Always show non-verbose messages
                if (!IsVerboseMessage(msg))
                {
                    LogOutput(msg);
                    return;
                }

                // Only show verbose messages if verbose flag is set or tslpatchdata is being generated
                if (shouldShowVerbose)
                {
                    LogOutput(msg);
                }
            };
        }

        /// <summary>
        /// Check if a message is a verbose message that should be suppressed by default.
        /// </summary>
        private static bool IsVerboseMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg))
            {
                return false;
            }

            string msgLower = msg.ToLowerInvariant();
            
            // Verbose messages include:
            // - tslpatchdata messages
            // - [INSTALL] messages
            // - Progress messages
            // - Debug messages
            // - Detailed processing information
            return msg.Contains("[INSTALL]") ||
                   msg.Contains("tslpatchdata") ||
                   msg.Contains("Processing TLK") ||
                   msg.Contains("TLK processing complete") ||
                   msg.Contains("Found") && msg.Contains("total modifications") ||
                   msg.Contains("Applied filters") ||
                   msg.Contains("Filtered to") ||
                   msg.Contains("Comparing") && msg.Contains("resources") ||
                   msg.Contains("Progress:") ||
                   msg.Contains("[DEBUG]") ||
                   msg.Contains("Winning installation") ||
                   msg.Contains("Processing resource:") ||
                   msg.Contains("Destination:") ||
                   msg.Contains("Filename:") ||
                   msg.Contains("File will be copied") ||
                   msg.Contains("Will use installed file") ||
                   msg.Contains("append.tlk") ||
                   msg.Contains("replace.tlk") ||
                   msg.StartsWith("  |--") ||
                   msg.StartsWith("  +--") ||
                   (msg.StartsWith("  ") && (msg.Contains("Source") || msg.Contains("Missing") || msg.Contains("→")));
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
            bool isVerbose = config.Verbose || config.Debug;
            if (!isVerbose)
            {
                return; // Suppress configuration output unless verbose
            }

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

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:197-202
        // Original: def stop_profiler(profiler: cProfile.Profile): ...
        /// <summary>
        /// Stop and save profiler output.
        /// </summary>
        public static void StopProfiler(Profiler profiler)
        {
            if (profiler == null || !profiler.IsEnabled)
            {
                return;
            }

            profiler.Disable();
            string profilerOutputFile = Path.Combine(Directory.GetCurrentDirectory(), "profiler_output.pstat");
            profiler.DumpStats(profilerOutputFile);
            LogOutput($"Profiler output saved to: {profilerOutputFile}");
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
                // Get path strings for display
                string path1Str = GetPathDisplayString(config.Paths[0]);
                string path2Str = config.Paths.Count > 1 ? GetPathDisplayString(config.Paths[1]) : "";
                
                string matchText = comparison == true ? "DOES MATCH" : "DOES NOT MATCH";
                
                if (config.Paths.Count == 2)
                {
                    LogOutput($"{path1Str} {matchText} {path2Str}");
                }
                else
                {
                    string matchText2 = comparison == true ? " MATCHES " : " DOES NOT MATCH ";
                    LogOutput($"Comparison of {config.Paths.Count} paths: {matchText2}");
                }
            }
            return comparison == true ? 0 : (comparison == false ? 2 : 3);
        }

        /// <summary>
        /// Get display string for a path object.
        /// </summary>
        private static string GetPathDisplayString(object path)
        {
            if (path == null)
            {
                return "null";
            }

            if (path is Installation installation)
            {
                return installation.Path;
            }

            if (path is string pathStr)
            {
                return pathStr;
            }

            if (path is FileInfo fileInfo)
            {
                return fileInfo.FullName;
            }

            if (path is DirectoryInfo dirInfo)
            {
                return dirInfo.FullName;
            }

            return path.ToString();
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:417-499
        // Original: def handle_diff(config: KotorDiffConfig) -> tuple[bool | None, int | None]: ...
        /// <summary>
        /// Handle diff operation with TSLPatcher data generation support.
        /// </summary>
        public static (bool? comparison, int? exitCode) HandleDiff(KotorDiffConfig config)
        {
            // Store config in global config (matching Python line 556: _global_config.config = config)
            GlobalConfig.Instance.Config = config;
            GlobalConfig.Instance.LoggingEnabled = config.LoggingEnabled;

            // Set up output log path
            if (config.OutputLogPath != null)
            {
                GlobalConfig.Instance.OutputLog = config.OutputLogPath;
            }

            // Create modifications collection
            var modifications = new ModificationsByType();
            GlobalConfig.Instance.ModificationsByType = modifications;

            // Create incremental writer if requested
            // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:431-474
            // Original: if config.tslpatchdata_path: ... incremental_writer = global::TSLPatcher.IncrementalTSLPatchDataWriter(...)
            global::TSLPatcher.IncrementalTSLPatchDataWriter incrementalWriter = null;
            object basePath = null;
            if (config.TslPatchDataPath != null)
            {
                // Find first valid directory path
                if (config.Paths != null && config.Paths.Count > 0)
                {
                    foreach (var candidatePath in config.Paths)
                    {
                        if (candidatePath is Installation installation)
                        {
                            basePath = installation;
                            break;
                        }
                        else if (candidatePath is string pathStr && Directory.Exists(pathStr))
                        {
                            basePath = pathStr;
                            break;
                        }
                        else if (candidatePath is DirectoryInfo dirInfo)
                        {
                            basePath = dirInfo;
                            break;
                        }
                    }
                }

                if (config.UseIncrementalWriter)
                {
                    // Determine game from first valid directory path
                    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:441-450
                    Game? game = null;
                    if (basePath != null)
                    {
                        try
                        {
                            if (basePath is Installation installation)
                            {
                                game = installation.Game;
                            }
                            else
                            {
                                // Default to K1 for directory paths
                                game = Game.K1;
                            }
                        }
                        catch (Exception e)
                        {
                            LogOutput($"[Warning] Could not determine game: {e.GetType().Name}: {e.Message}");
                            LogOutput("Full traceback:");
                            LogOutput($"  {e.StackTrace}");
                        }
                    }

                    // Create StrRef cache if we have a valid game
                    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:452-453
                    StrRefReferenceCache strrefCache = game.HasValue ? new StrRefReferenceCache(game.Value) : null;

                    // Create 2DA memory caches if we have a valid game
                    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:455-464
                    // Structure: {installation_index: CaseInsensitiveDict<TwoDAMemoryReferenceCache>}
                    Dictionary<int, Dictionary<string, TwoDAMemoryReferenceCache>> twodaCaches = null;
                    if (game.HasValue && config.Paths != null)
                    {
                        twodaCaches = new Dictionary<int, Dictionary<string, TwoDAMemoryReferenceCache>>();
                        // Initialize caches for each path index
                        for (int idx = 0; idx < config.Paths.Count; idx++)
                        {
                            twodaCaches[idx] = new Dictionary<string, TwoDAMemoryReferenceCache>(StringComparer.OrdinalIgnoreCase);
                        }
                    }

                    // Extract base data path string for writer
                    string baseDataPathStr = null;
                    if (basePath is string pathStr)
                    {
                        baseDataPathStr = pathStr;
                    }
                    else if (basePath is DirectoryInfo dirInfo)
                    {
                        baseDataPathStr = dirInfo.FullName;
                    }
                    else if (basePath is Installation installation)
                    {
                        baseDataPathStr = installation.Path;
                    }

                    incrementalWriter = new global::TSLPatcher.IncrementalTSLPatchDataWriter(
                        config.TslPatchDataPath.FullName,
                        config.IniFilename ?? "changes.ini",
                        baseDataPathStr,
                        null, // moddedDataPath
                        strrefCache,
                        twodaCaches,
                        (string msg) => LogOutput(msg));
                    LogOutput($"Using incremental writer for tslpatchdata: {config.TslPatchDataPath.FullName}");
                }
                else
                {
                    // Extract base data path from first path if it's a directory (for non-incremental)
                    string baseDataPath = null;
                    string moddedDataPath = null;
                    if (config.Paths != null && config.Paths.Count > 0)
                    {
                        object firstPath = config.Paths[0];
                        if (firstPath is string pathStr && Directory.Exists(pathStr))
                        {
                            baseDataPath = pathStr;
                        }
                        else if (firstPath is DirectoryInfo dirInfo)
                        {
                            baseDataPath = dirInfo.FullName;
                        }
                        else if (firstPath is Installation installation)
                        {
                            baseDataPath = installation.Path;
                        }

                        // Extract modded data path from second path if available
                        if (config.Paths.Count > 1)
                        {
                            object secondPath = config.Paths[1];
                            if (secondPath is string pathStr2 && Directory.Exists(pathStr2))
                            {
                                moddedDataPath = pathStr2;
                            }
                            else if (secondPath is DirectoryInfo dirInfo2)
                            {
                                moddedDataPath = dirInfo2.FullName;
                            }
                            else if (secondPath is Installation installation2)
                            {
                                moddedDataPath = installation2.Path;
                            }
                        }
                    }

                    incrementalWriter = new global::TSLPatcher.IncrementalTSLPatchDataWriter(
                        config.TslPatchDataPath.FullName,
                        config.IniFilename ?? "changes.ini",
                        baseDataPath,
                        moddedDataPath);
                }
            }

            // Call handle_diff_internal
            var result = HandleDiffInternal(
                config.Paths,
                filters: config.Filters,
                incrementalWriter: incrementalWriter);

            // Finalize TSLPatcher data if requested
            // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:482-525
            // Original: if config.tslpatchdata_path: ... if config.use_incremental_writer and incremental_writer: ...
            if (config.TslPatchDataPath != null)
            {
                if (config.UseIncrementalWriter && incrementalWriter != null)
                {
                    try
                    {
                        // Finalize INI by writing InstallList section
                        incrementalWriter.FinalizeWriter();

                        // Summary
                        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:489-500
                        LogOutput("\nTSLPatcher data generation complete:");
                        LogOutput($"  Location: {config.TslPatchDataPath.FullName}");
                        LogOutput($"  INI file: {config.IniFilename ?? "changes.ini"}");
                        LogOutput($"  TLK modifications: {incrementalWriter.AllModifications.Tlk?.Count ?? 0}");
                        LogOutput($"  2DA modifications: {incrementalWriter.AllModifications.Twoda?.Count ?? 0}");
                        LogOutput($"  GFF modifications: {incrementalWriter.AllModifications.Gff?.Count ?? 0}");
                        LogOutput($"  SSF modifications: {incrementalWriter.AllModifications.Ssf?.Count ?? 0}");
                        LogOutput($"  NCS modifications: {incrementalWriter.AllModifications.Ncs?.Count ?? 0}");
                        int totalInstallFiles = incrementalWriter.InstallFolders?.Values.Sum(files => files?.Count ?? 0) ?? 0;
                        LogOutput($"  Install files: {totalInstallFiles}");
                        LogOutput($"  Install folders: {incrementalWriter.InstallFolders?.Count ?? 0}");
                    }
                    catch (Exception genError)
                    {
                        LogOutput($"[Error] Failed to finalize TSLPatcher data: {genError.GetType().Name}: {genError.Message}");
                        LogOutput("Full traceback:");
                        LogOutput($"  {genError.StackTrace}");
                        return (null, 1);
                    }
                }
                else if (!config.UseIncrementalWriter && HasModifications(modifications))
                {
                    try
                    {
                        // Use batch generation if not using incremental writer
                        GenerateTSLPatcherData(
                            config.TslPatchDataPath,
                            config.IniFilename ?? "changes.ini",
                            modifications,
                            config.Paths);
                    }
                    catch (Exception genError)
                    {
                        LogOutput($"[Error] Failed to generate TSLPatcher data: {genError.GetType().Name}: {genError.Message}");
                        LogOutput("Full traceback:");
                        LogOutput($"  {genError.StackTrace}");
                        return (null, 1);
                    }
                }
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
            if (tslpatchdataPath == null || !HasModifications(modifications))
            {
                return;
            }

            LogOutput($"\nGenerating TSLPatcher data at: {tslpatchdataPath.FullName}");

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

            // Analyze TLK StrRef references and create linking patches BEFORE generating files
            // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:311-340
            // Original: if modifications.tlk and base_data_path: ... analyze_tlk_strref_references(...)
            if (modifications.Tlk != null && modifications.Tlk.Count > 0 && baseDataPath != null)
            {
                LogOutput("\n=== Analyzing StrRef References ===");
                LogOutput("Searching entire installation/folder for files that reference modified StrRefs...");

                foreach (var tlkMod in modifications.Tlk)
                {
                    try
                    {
                        // Build the tuple expected by AnalyzeTlkStrrefReferences signature
                        // TODO:  Here we do not have strref_mappings directly, so pass empty mapping for now
                        var strrefMappings = new Dictionary<int, int>();
                        var tlkModTuple = Tuple.Create(tlkMod, strrefMappings);

                        ReferenceAnalyzers.AnalyzeTlkStrrefReferences(
                            tlkModTuple,
                            strrefMappings,
                            baseDataPath.FullName,
                            modifications.Gff,
                            modifications.Twoda,
                            modifications.Ssf,
                            modifications.Ncs,
                            (string msg) => LogOutput(msg));
                    }
                    catch (Exception e)
                    {
                        LogOutput($"[Warning] StrRef analysis failed for tlk_mod={tlkMod}: {e.GetType().Name}: {e.Message}");
                        LogOutput($"Full traceback (tlk_mod={tlkMod}):");
                        LogOutput($"  {e.StackTrace}");
                    }
                }

                LogOutput("StrRef analysis complete. Added linking patches:");
                int gffCount = modifications.Gff?.Sum(m => m.Modifiers?.Count ?? 0) ?? 0;
                int twodaCount = modifications.Twoda?.Sum(m => m.Modifiers?.Count ?? 0) ?? 0;
                int ssfCount = modifications.Ssf?.Sum(m => m.Modifiers?.Count ?? 0) ?? 0;
                int ncsCount = modifications.Ncs?.Sum(m => m.Modifiers?.Count ?? 0) ?? 0;
                LogOutput($"  GFF patches: {gffCount}");
                LogOutput($"  2DA patches: {twodaCount}");
                LogOutput($"  SSF patches: {ssfCount}");
                LogOutput($"  NCS patches: {ncsCount}");
            }

            // Use TSLPatchDataGenerator for batch generation
            var generator = new Andastra.Parsing.TSLPatcher.TSLPatchDataGenerator(tslpatchdataPath);

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

            // Use TSLPatcher INI serializer
            var serializer = new Andastra.Parsing.Mods.TSLPatcherINISerializer();
            string iniContent = serializer.Serialize(modifications, includeHeader: true, includeSettings: true);
                File.WriteAllText(iniPath, iniContent, SystemTextEncoding.UTF8);

            // Summary
            LogOutput("\nTSLPatcher data generation complete:");
            LogOutput($"  Location: {tslpatchdataPath.FullName}");
            LogOutput($"  INI file: {iniFilename}");
            LogOutput($"  TLK modifications: {modifications.Tlk?.Count ?? 0}");
            LogOutput($"  2DA modifications: {modifications.Twoda?.Count ?? 0}");
            LogOutput($"  GFF modifications: {modifications.Gff?.Count ?? 0}");
            LogOutput($"  SSF modifications: {modifications.Ssf?.Count ?? 0}");
            LogOutput($"  NCS modifications: {modifications.Ncs?.Count ?? 0}");
            LogOutput($"  Install folders: {modifications.Install?.Count ?? 0}");
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/writer.py:145-150
        // Original: def has_modifications(self) -> bool: ...
        /// <summary>
        /// Check if modifications collection has any modifications.
        /// </summary>
        private static bool HasModifications(ModificationsByType modifications)
        {
            if (modifications == null)
            {
                return false;
            }

            return (modifications.Tlk != null && modifications.Tlk.Count > 0) ||
                   (modifications.Twoda != null && modifications.Twoda.Count > 0) ||
                   (modifications.Gff != null && modifications.Gff.Count > 0) ||
                   (modifications.Ssf != null && modifications.Ssf.Count > 0) ||
                   (modifications.Ncs != null && modifications.Ncs.Count > 0) ||
                   (modifications.Nss != null && modifications.Nss.Count > 0) ||
                   (modifications.Install != null && modifications.Install.Count > 0);
        }
    }
}

