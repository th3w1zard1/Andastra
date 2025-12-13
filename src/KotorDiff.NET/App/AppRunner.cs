// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:582-631
// Original: def run_application(config: KotorDiffConfig) -> int: ...
using System;
using System.IO;
using System.Linq;
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
            SetupLogging(config);

            // Log configuration
            LogConfiguration(config);

            // Run with optional profiler
            // TODO: Implement profiler support
            if (config.UseProfiler)
            {
                Console.WriteLine("[Warning] Profiler not yet implemented");
            }

            try
            {
                var comparison = ExecuteDiff(config);

                // Format and return final output
                if (comparison.HasValue)
                {
                    return FormatComparisonOutput(comparison.Value, config);
                }

                return 0;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error - KotorDiff was cancelled: {e.Message}");
                Console.WriteLine(e.StackTrace);
                return 1;
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:219-242
        // Original: def _setup_logging(config: KotorDiffConfig) -> None: ...
        private static void SetupLogging(KotorDiffConfig config)
        {
            // TODO: Implement logging system setup
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:245-256
        // Original: def _log_configuration(config: KotorDiffConfig) -> None: ...
        private static void LogConfiguration(KotorDiffConfig config)
        {
            Console.WriteLine();
            Console.WriteLine("Configuration:");
            Console.WriteLine($"  Mode: {config.Paths.Count}-way comparison");

            for (int i = 0; i < config.Paths.Count; i++)
            {
                Console.WriteLine($"  Path {i + 1}: '{config.Paths[i]}'");
            }

            Console.WriteLine($"Using --compare-hashes={config.CompareHashes}");
            Console.WriteLine($"Using --use-profiler={config.UseProfiler}");
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
            // TODO: Implement IncrementalTSLPatchDataWriter
            if (config.TslPatchDataPath != null && config.UseIncrementalWriter)
            {
                Console.WriteLine("[Warning] Incremental writer not yet implemented");
            }

            // Run the diff
            Action<string> logFunc = Console.WriteLine;
            bool? comparison = DiffEngine.RunDifferFromArgsImpl(
                allPaths,
                filters: config.Filters,
                logFunc: logFunc,
                compareHashes: config.CompareHashes,
                modificationsByType: modificationsByType);

            // Finalize TSLPatcher data if requested
            if (config.TslPatchDataPath != null && !config.UseIncrementalWriter)
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
                    Console.WriteLine($"[Error] Failed to generate TSLPatcher data: {genError.GetType().Name}: {genError.Message}");
                    Console.WriteLine("Full traceback:");
                    Console.WriteLine(genError.StackTrace);
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
            Console.WriteLine($"\nGenerating TSLPatcher data at: {tslpatchdataPath}");

            // Create the generator
            var generator = new TSLPatchDataGenerator(tslpatchdataPath);

            // Generate all resource files
            var generatedFiles = generator.GenerateAllFiles(modifications, baseDataPath);

            if (generatedFiles.Count > 0)
            {
                Console.WriteLine($"Generated {generatedFiles.Count} resource file(s):");
                foreach (var filename in generatedFiles.Keys)
                {
                    Console.WriteLine($"  - {filename}");
                }
            }

            // Update install folders based on generated files and modifications
            modifications.Install = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Generate changes.ini
            var iniPath = new FileInfo(Path.Combine(tslpatchdataPath.FullName, iniFilename));
            Console.WriteLine($"\nGenerating {iniFilename} at: {iniPath}");

            // Serialize INI file
            var serializer = new CSharpKOTOR.Mods.TSLPatcherINISerializer();
            string iniContent = serializer.Serialize(modifications, includeHeader: true, includeSettings: false, verbose: false);
            File.WriteAllText(iniPath.FullName, iniContent, Encoding.UTF8);
            Console.WriteLine($"Generated {iniFilename} with {iniContent.Split('\n').Length} lines");

            // Summary
            Console.WriteLine("\nTSLPatcher data generation complete:");
            Console.WriteLine($"  Location: {tslpatchdataPath}");
            Console.WriteLine($"  INI file: {iniFilename}");
            Console.WriteLine($"  TLK modifications: {modifications.Tlk?.Count ?? 0}");
            Console.WriteLine($"  2DA modifications: {modifications.Twoda?.Count ?? 0}");
            Console.WriteLine($"  GFF modifications: {modifications.Gff?.Count ?? 0}");
            Console.WriteLine($"  SSF modifications: {modifications.Ssf?.Count ?? 0}");
            Console.WriteLine($"  NCS modifications: {modifications.Ncs?.Count ?? 0}");
            Console.WriteLine($"  Install folders: {modifications.Install?.Count ?? 0}");
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/app.py:271-289
        // Original: def _format_comparison_output(comparison: bool | None, config: KotorDiffConfig) -> int: ...
        private static int FormatComparisonOutput(bool comparison, KotorDiffConfig config)
        {
            if (config.Paths.Count >= 2)
            {
                Console.WriteLine(
                    $"Comparison of {config.Paths.Count} paths: " +
                    (comparison ? " MATCHES " : " DOES NOT MATCH "));
            }
            return comparison ? 0 : 2;
        }
    }
}

