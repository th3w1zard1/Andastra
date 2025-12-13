// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:582-631
// Original: def run_application(config: KotorDiffConfig) -> int: ...
using System;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Extract;
using CSharpKOTOR.Mods;
using KotorDiff.NET.Diff;
using KotorDiff.NET.Generator;
using KotorDiff.NET.Logger;

namespace KotorDiff.NET.App
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:582-631
    // Original: def run_application(config: KotorDiffConfig) -> int: ...
    public static class AppRunner
    {
        public static int RunApplication(KotorDiffConfig config)
        {
            // Store config in global config
            GlobalConfig.Instance.Config = config;
            GlobalConfig.Instance.LoggingEnabled = config.LoggingEnabled;

            // Set up output log path
            if (config.OutputLogPath != null)
            {
                GlobalConfig.Instance.OutputLog = config.OutputLogPath;
            }

            // Set up the logging system
            var logger = SetupLogging(config);

            // Log configuration
            LogConfiguration(config, logger);

            // Run with optional profiler
            System.Diagnostics.Stopwatch profiler = null;
            if (config.UseProfiler)
            {
                profiler = System.Diagnostics.Stopwatch.StartNew();
            }

            try
            {
                var comparison = ExecuteDiff(config);

                if (profiler != null)
                {
                    profiler.Stop();
                    DiffLogger.GetLogger()?.Info($"Profiler output: Elapsed time {profiler.Elapsed.TotalSeconds} seconds");
                }

                // Format and return final output
                if (comparison.HasValue)
                {
                    return FormatComparisonOutput(comparison.Value, config);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                DiffLogger.GetLogger()?.Info("KeyboardInterrupt - KotorDiff was cancelled by user.");
                if (profiler != null)
                {
                    profiler.Stop();
                }
                return 1;
            }
            catch (Exception e)
            {
                DiffLogger.GetLogger()?.Critical($"[CRASH] Unhandled exception caught: {e.Message}");
                DiffLogger.GetLogger()?.Critical(e.ToString());
                if (profiler != null)
                {
                    profiler.Stop();
                }
                return 1;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:219-242
        // Original: def _setup_logging(config: KotorDiffConfig) -> None: ...
        private static DiffLogger SetupLogging(KotorDiffConfig config)
        {
            LogLevel level = LogLevel.INFO;
            if (Enum.TryParse(config.LogLevel, true, out LogLevel parsedLevel))
            {
                level = parsedLevel;
            }

            OutputMode mode = OutputMode.FULL;
            if (Enum.TryParse(config.OutputMode, true, out OutputMode parsedMode))
            {
                mode = parsedMode;
            }

            TextWriter outputFile = null;
            if (config.OutputLogPath != null)
            {
                try
                {
                    outputFile = new StreamWriter(config.OutputLogPath.FullName, append: true, Encoding.UTF8);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"[ERROR] Could not open log file '{config.OutputLogPath}': {e.Message}");
                }
            }

            return DiffLogger.SetupLogger(level, mode, useColors: config.UseColors, outputFile: outputFile);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:245-256
        // Original: def _log_configuration(config: KotorDiffConfig) -> None: ...
        private static void LogConfiguration(KotorDiffConfig config, DiffLogger logger)
        {
            logger.Info("");
            logger.Info("Configuration:");
            logger.Info($"  Mode: {config.Paths.Count}-way comparison");

            for (int i = 0; i < config.Paths.Count; i++)
            {
                logger.Info($"  Path {i + 1}: '{config.Paths[i]}'");
            }

            logger.Info($"Using --compare-hashes={config.CompareHashes}");
            logger.Info($"Using --use-profiler={config.UseProfiler}");
            if (config.TslPatchDataPath != null)
            {
                logger.Info($"Using --tslpatchdata={config.TslPatchDataPath}");
            }
            logger.Info($"Using --ini={config.IniFilename}");
            logger.Info($"Using --incremental-writer={config.UseIncrementalWriter}");
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:417-527
        // Original: def handle_diff(config: KotorDiffConfig) -> tuple[bool | None, int | None]: ...
        private static bool? ExecuteDiff(KotorDiffConfig config)
        {
            // Create modifications collection for INI generation
            var modificationsByType = ModificationsByType.CreateEmpty();
            GlobalConfig.Instance.ModificationsByType = modificationsByType;

            // Use paths from config
            var allPaths = config.Paths;

            // Create incremental writer if requested
            IncrementalTSLPatchDataWriter incrementalWriter = null;
            if (config.TslPatchDataPath != null && config.UseIncrementalWriter)
            {
                string baseDataPath = null;
                // Try to determine base path from first valid directory path
                foreach (var candidatePath in allPaths)
                {
                    if (candidatePath is Installation install)
                    {
                        baseDataPath = install.InstallationPath;
                        break;
                    }
                    else if (candidatePath is string pathStr && Directory.Exists(pathStr))
                    {
                        baseDataPath = pathStr;
                        break;
                    }
                }

                incrementalWriter = new IncrementalTSLPatchDataWriter(
                    config.TslPatchDataPath.FullName,
                    config.IniFilename,
                    baseDataPath: baseDataPath,
                    moddedDataPath: null, // TODO: Determine modded path
                    strrefCache: null, // TODO: Implement StrRefReferenceCache
                    twodaCaches: null, // TODO: Implement TwoDAMemoryReferenceCache
                    logFunc: (msg) => DiffLogger.GetLogger()?.Info(msg)
                );
                DiffLogger.GetLogger()?.Info($"Using incremental writer for tslpatchdata: {config.TslPatchDataPath}");
            }

            // Run the diff
            Action<string> logFunc = (msg) => DiffLogger.GetLogger()?.Info(msg);
            bool? comparison = DiffEngine.RunDifferFromArgsImpl(
                allPaths,
                filters: config.Filters,
                logFunc: logFunc,
                compareHashes: config.CompareHashes,
                modificationsByType: modificationsByType,
                incrementalWriter: incrementalWriter);

            // Finalize TSLPatcher data if requested
            if (config.TslPatchDataPath != null)
            {
                if (config.UseIncrementalWriter && incrementalWriter != null)
                {
                    try
                    {
                        // Finalize INI by writing InstallList section
                        incrementalWriter.FinalizeWriter();
                        
                        // Summary
                        DiffLogger.GetLogger()?.Info("\nTSLPatcher data generation complete:");
                        DiffLogger.GetLogger()?.Info($"  Location: {config.TslPatchDataPath}");
                        DiffLogger.GetLogger()?.Info($"  INI file: {config.IniFilename}");
                        DiffLogger.GetLogger()?.Info($"  TLK modifications: {incrementalWriter.AllModifications.Tlk.Count}");
                        DiffLogger.GetLogger()?.Info($"  2DA modifications: {incrementalWriter.AllModifications.Twoda.Count}");
                        DiffLogger.GetLogger()?.Info($"  GFF modifications: {incrementalWriter.AllModifications.Gff.Count}");
                        DiffLogger.GetLogger()?.Info($"  SSF modifications: {incrementalWriter.AllModifications.Ssf.Count}");
                        DiffLogger.GetLogger()?.Info($"  NCS modifications: {incrementalWriter.AllModifications.Ncs.Count}");
                        int totalInstallFiles = incrementalWriter.InstallFolders.Values.Sum(files => files.Count);
                        DiffLogger.GetLogger()?.Info($"  Install files: {totalInstallFiles}");
                        DiffLogger.GetLogger()?.Info($"  Install folders: {incrementalWriter.InstallFolders.Count}");
                    }
                    catch (Exception genError)
                    {
                        DiffLogger.GetLogger()?.Error($"[Error] Failed to finalize TSLPatcher data: {genError.GetType().Name}: {genError.Message}");
                        DiffLogger.GetLogger()?.Debug("Full traceback:");
                        DiffLogger.GetLogger()?.Debug(genError.StackTrace);
                        return null;
                    }
                }
                else if (!config.UseIncrementalWriter)
                {
            {
                try
                {
                    GenerateTslPatcherData(
                        config.TslPatchDataPath,
                        config.IniFilename,
                        modificationsByType,
                        baseDataPath: null); // TODO: Determine base path from paths
                }
                catch (Exception genError)
                {
                    DiffLogger.GetLogger()?.Error($"[Error] Failed to generate TSLPatcher data: {genError.GetType().Name}: {genError.Message}");
                    DiffLogger.GetLogger()?.Debug("Full traceback:");
                    DiffLogger.GetLogger()?.Debug(genError.StackTrace);
                    return null;
                }
            }

            return comparison;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:292-374
        // Original: def generate_tslpatcher_data(...): ...
        private static void GenerateTslPatcherData(
            DirectoryInfo tslpatchdataPath,
            string iniFilename,
            ModificationsByType modifications,
            DirectoryInfo baseDataPath = null)
        {
            DiffLogger.GetLogger()?.Info($"\nGenerating TSLPatcher data at: {tslpatchdataPath}");

            // Create the generator
            var generator = new TSLPatchDataGenerator(tslpatchdataPath);

            // Generate all resource files
            var generatedFiles = generator.GenerateAllFiles(modifications, baseDataPath);

            if (generatedFiles.Count > 0)
            {
                DiffLogger.GetLogger()?.Info($"Generated {generatedFiles.Count} resource file(s):");
                foreach (var filename in generatedFiles.Keys)
                {
                    DiffLogger.GetLogger()?.Info($"  - {filename}");
                }
            }

            // Update install folders based on generated files and modifications
            modifications.Install = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Generate changes.ini
            var iniPath = new FileInfo(Path.Combine(tslpatchdataPath.FullName, iniFilename));
            DiffLogger.GetLogger()?.Info($"\nGenerating {iniFilename} at: {iniPath}");

            // Serialize INI file
            var serializer = new CSharpKOTOR.Mods.TSLPatcherINISerializer();
            string iniContent = serializer.Serialize(modifications, includeHeader: true, includeSettings: false, verbose: false);
            File.WriteAllText(iniPath.FullName, iniContent, Encoding.UTF8);
            DiffLogger.GetLogger()?.Info($"Generated {iniFilename} with {iniContent.Split('\n').Length} lines");

            // Summary
            DiffLogger.GetLogger()?.Info("\nTSLPatcher data generation complete:");
            DiffLogger.GetLogger()?.Info($"  Location: {tslpatchdataPath}");
            DiffLogger.GetLogger()?.Info($"  INI file: {iniFilename}");
            DiffLogger.GetLogger()?.Info($"  TLK modifications: {modifications.Tlk?.Count ?? 0}");
            DiffLogger.GetLogger()?.Info($"  2DA modifications: {modifications.Twoda?.Count ?? 0}");
            DiffLogger.GetLogger()?.Info($"  GFF modifications: {modifications.Gff?.Count ?? 0}");
            DiffLogger.GetLogger()?.Info($"  SSF modifications: {modifications.Ssf?.Count ?? 0}");
            DiffLogger.GetLogger()?.Info($"  NCS modifications: {modifications.Ncs?.Count ?? 0}");
            DiffLogger.GetLogger()?.Info($"  Install folders: {modifications.Install?.Count ?? 0}");
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:271-289
        // Original: def _format_comparison_output(comparison: bool | None, config: KotorDiffConfig) -> int: ...
        private static int FormatComparisonOutput(bool comparison, KotorDiffConfig config)
        {
            if (config.Paths.Count >= 2)
            {
                DiffLogger.GetLogger()?.Info(
                    $"Comparison of {config.Paths.Count} paths: " +
                    (comparison ? " MATCHES " : " DOES NOT MATCH "));
            }
            return comparison ? 0 : 2;
        }
    }
}

