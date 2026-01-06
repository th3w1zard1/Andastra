// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli.py:138-219
// Original: def execute_cli(cmdline_args: Namespace): ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Installation;
using KotorDiff;
using KotorDiff.AppCore;

namespace KotorDiff.Cli
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli.py:138-219
    // Original: def execute_cli(cmdline_args: Namespace): ...
    public static class CliExecutor
    {
        public static void ExecuteCli(CliArgs cmdlineArgs)
        {
            // Configure console for UTF-8 output on Windows
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                try
                {
                    Console.OutputEncoding = Encoding.UTF8;
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to configure console for UTF-8 output on Windows");
                }
            }

            Console.WriteLine($"KotorDiff version 1.0.0");

            // Gather all path inputs
            var rawPathInputs = new List<string>();

            if (!string.IsNullOrEmpty(cmdlineArgs.Path1))
            {
                rawPathInputs.Add(CliUtils.NormalizePathArg(cmdlineArgs.Path1));
            }
            if (!string.IsNullOrEmpty(cmdlineArgs.Path2))
            {
                rawPathInputs.Add(CliUtils.NormalizePathArg(cmdlineArgs.Path2));
            }
            if (!string.IsNullOrEmpty(cmdlineArgs.Path3))
            {
                rawPathInputs.Add(CliUtils.NormalizePathArg(cmdlineArgs.Path3));
            }

            if (cmdlineArgs.ExtraPaths != null)
            {
                foreach (string p in cmdlineArgs.ExtraPaths)
                {
                    rawPathInputs.Add(CliUtils.NormalizePathArg(p));
                }
            }

            if (rawPathInputs.Count < 2)
            {
                Console.Error.WriteLine("[Error] At least 2 paths are required for comparison.");
                Console.Error.WriteLine("[Info] Use --help to see CLI options");
                Environment.Exit(1);
            }

            // Convert string paths to Path/Installation objects (matching Python lines 188-200)
            var resolvedPaths = new List<object>();
            foreach (string pathStr in rawPathInputs)
            {
                if (string.IsNullOrEmpty(pathStr))
                {
                    continue;
                }

                try
                {
                    // Try to create an Installation object (for KOTOR installations)
                    // Matching Python: installation = Installation(path_obj)
                    var installation = new Andastra.Parsing.Installation.Installation(pathStr);
                    resolvedPaths.Add(installation);
                    Console.WriteLine($"[DEBUG] Loaded Installation for: {pathStr}");
                }
                catch (Exception e)
                {
                    // Fall back to Path object (for folders/files)
                    // Matching Python: resolved_paths.append(path_obj)
                    resolvedPaths.Add(pathStr);
                    Console.WriteLine($"[DEBUG] Using Path (not Installation) for: {pathStr}");
                    Console.WriteLine($"[DEBUG] Installation load failed: {e.GetType().Name}: {e.Message}");
                }
            }

            // Create configuration object
            var config = new KotorDiffConfig
            {
                Paths = resolvedPaths,
                TslPatchDataPath = !string.IsNullOrEmpty(cmdlineArgs.TslPatchData) ? new DirectoryInfo(cmdlineArgs.TslPatchData) : null,
                IniFilename = !string.IsNullOrEmpty(cmdlineArgs.Ini) ? cmdlineArgs.Ini : "changes.ini",
                OutputLogPath = !string.IsNullOrEmpty(cmdlineArgs.OutputLog) ? new FileInfo(cmdlineArgs.OutputLog) : null,
                LogLevel = !string.IsNullOrEmpty(cmdlineArgs.LogLevel) ? cmdlineArgs.LogLevel : "info",
                OutputMode = !string.IsNullOrEmpty(cmdlineArgs.OutputMode) ? cmdlineArgs.OutputMode : "full",
                UseColors = !cmdlineArgs.NoColor,
                CompareHashes = cmdlineArgs.CompareHashes,
                UseProfiler = cmdlineArgs.UseProfiler,
                Filters = cmdlineArgs.Filter,
                LoggingEnabled = cmdlineArgs.Logging
            };

            // Run the application
            // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli.py:218
            // Original: exit_code: int = run_application(config); sys.exit(exit_code)
            int exitCode = KotorDiff.AppCore.AppRunner.RunApplication(config);
            Environment.Exit(exitCode);
        }
    }
}

