// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli.py:22-126
// Original: def parse_args() -> Namespace: ...
using System;
using System.Collections.Generic;
using System.Linq;

namespace KotorDiff.Cli
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli.py:22-126
    // Original: def parse_args() -> Namespace: ...
    public static class CliParser
    {
        public static CliArgs ParseArgs(string[] args)
        {
            var result = new CliArgs();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                string nextArg = i + 1 < args.Length ? args[i + 1] : null;

                if (arg == "--path1" && nextArg != null)
                {
                    result.Path1 = CliUtils.NormalizePathArg(nextArg);
                    i++;
                }
                else if (arg == "--path2" && nextArg != null)
                {
                    result.Path2 = CliUtils.NormalizePathArg(nextArg);
                    i++;
                }
                else if (arg == "--path3" && nextArg != null)
                {
                    result.Path3 = CliUtils.NormalizePathArg(nextArg);
                    i++;
                }
                else if (arg == "--path" && nextArg != null)
                {
                    if (result.ExtraPaths == null)
                    {
                        result.ExtraPaths = new List<string>();
                    }
                    result.ExtraPaths.Add(CliUtils.NormalizePathArg(nextArg));
                    i++;
                }
                else if (arg == "--tslpatchdata" && nextArg != null)
                {
                    result.TslPatchData = nextArg;
                    i++;
                }
                else if (arg == "--ini" && nextArg != null)
                {
                    result.Ini = nextArg;
                    i++;
                }
                else if (arg == "--output-log" && nextArg != null)
                {
                    result.OutputLog = nextArg;
                    i++;
                }
                else if (arg == "--log-level" && nextArg != null)
                {
                    result.LogLevel = nextArg;
                    i++;
                }
                else if (arg == "--output-mode" && nextArg != null)
                {
                    result.OutputMode = nextArg;
                    i++;
                }
                else if (arg == "--no-color")
                {
                    result.NoColor = true;
                }
                else if (arg == "--compare-hashes" && nextArg != null)
                {
                    result.CompareHashes = bool.Parse(nextArg);
                }
                else if (arg == "--filter" && nextArg != null)
                {
                    if (result.Filter == null)
                    {
                        result.Filter = new List<string>();
                    }
                    result.Filter.Add(nextArg);
                    i++;
                }
                else if (arg == "--logging" && nextArg != null)
                {
                    result.Logging = bool.Parse(nextArg);
                }
                else if (arg == "--use-profiler" && nextArg != null)
                {
                    result.UseProfiler = bool.Parse(nextArg);
                }
                else if (arg == "--console")
                {
                    result.Console = true;
                }
                else if (arg == "--gui")
                {
                    result.Gui = true;
                }
                else if (arg == "--verbose" || arg == "-v")
                {
                    result.Verbose = true;
                }
                else if (arg == "--debug")
                {
                    result.Debug = true;
                }
                else if (arg == "--help" || arg == "-h")
                {
                    PrintHelp();
                    Environment.Exit(0);
                }
            }

            // Handle positional arguments (first two non-flag args become path1 and path2)
            var positionalArgs = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                // Skip flags and their values
                if (arg.StartsWith("--") || arg.StartsWith("-"))
                {
                    // Skip the flag value if it exists
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--") && !args[i + 1].StartsWith("-"))
                    {
                        // Check if this flag takes a value
                        if (arg == "--path1" || arg == "--path2" || arg == "--path3" || arg == "--path" ||
                            arg == "--tslpatchdata" || arg == "--ini" || arg == "--output-log" ||
                            arg == "--log-level" || arg == "--output-mode" || arg == "--compare-hashes" ||
                            arg == "--filter" || arg == "--logging" || arg == "--use-profiler")
                        {
                            i++; // Skip the value
                        }
                    }
                }
                else
                {
                    // This is a positional argument
                    positionalArgs.Add(CliUtils.NormalizePathArg(arg));
                }
            }

            // Assign positional arguments to path1 and path2 if not already set
            if (string.IsNullOrEmpty(result.Path1) && positionalArgs.Count > 0)
            {
                result.Path1 = positionalArgs[0];
            }
            if (string.IsNullOrEmpty(result.Path2) && positionalArgs.Count > 1)
            {
                result.Path2 = positionalArgs[1];
            }

            return result;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli.py:222-229
        // Original: def has_cli_paths(cmdline_args: Namespace) -> bool: ...
        public static bool HasCliPaths(CliArgs args)
        {
            return !string.IsNullOrEmpty(args.Path1) ||
                   !string.IsNullOrEmpty(args.Path2) ||
                   !string.IsNullOrEmpty(args.Path3) ||
                   (args.ExtraPaths != null && args.ExtraPaths.Count > 0);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("KotorDiff - Finds differences between KOTOR files/dirs. Supports comparisons across any number of paths.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  kotordiff [options]");
            Console.WriteLine();
            Console.WriteLine("Path arguments:");
            Console.WriteLine("  --path1 PATH          Path to compare (required)");
            Console.WriteLine("  --path2 PATH          Additional path to compare (required)");
            Console.WriteLine("  --path3 PATH          Additional path to compare (optional)");
            Console.WriteLine("  --path PATH           Additional paths for N-way comparison (can be used multiple times)");
            Console.WriteLine();
            Console.WriteLine("Output options:");
            Console.WriteLine("  --tslpatchdata PATH   Path where tslpatchdata folder should be created");
            Console.WriteLine("  --ini FILENAME        Filename for changes.ini (default: changes.ini)");
            Console.WriteLine("  --output-log PATH     Filepath of the desired output logfile");
            Console.WriteLine();
            Console.WriteLine("Logging and display options:");
            Console.WriteLine("  --log-level LEVEL     Logging level: debug, info, warning, error, critical (default: info)");
            Console.WriteLine("  --output-mode MODE    Output mode: full, diff_only, quiet (default: full)");
            Console.WriteLine("  --no-color            Disable colored output");
            Console.WriteLine();
            Console.WriteLine("Comparison options:");
            Console.WriteLine("  --compare-hashes BOOL  Compare hashes of unsupported files (default: True)");
            Console.WriteLine("  --filter PATTERN      Filter specific files/modules (can be used multiple times)");
            Console.WriteLine("  --logging BOOL        Whether to log results to file (default: True)");
            Console.WriteLine("  --use-profiler BOOL   Use profiler to find execution bottlenecks (default: False)");
            Console.WriteLine();
            Console.WriteLine("GUI/Console options:");
            Console.WriteLine("  --console             Show console window even in GUI mode");
            Console.WriteLine("  --gui                 Force GUI mode even with paths provided");
            Console.WriteLine();
            Console.WriteLine("Verbosity options:");
            Console.WriteLine("  --verbose, -v          Show verbose output (tslpatchdata, install messages, etc.)");
            Console.WriteLine("  --debug                Show debug output");
        }
    }

    public class CliArgs
    {
        public string Path1 { get; set; }
        public string Path2 { get; set; }
        public string Path3 { get; set; }
        public List<string> ExtraPaths { get; set; }
        public string TslPatchData { get; set; }
        public string Ini { get; set; } = "changes.ini";
        public string OutputLog { get; set; }
        public string LogLevel { get; set; } = "info";
        public string OutputMode { get; set; } = "full";
        public bool NoColor { get; set; }
        public bool CompareHashes { get; set; } = true;
        public List<string> Filter { get; set; }
        public bool Logging { get; set; } = true;
        public bool UseProfiler { get; set; }
        public bool Console { get; set; }
        public bool Gui { get; set; }
        public bool Verbose { get; set; }
        public bool Debug { get; set; }
    }
}

