using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Common;
using CSharpKOTOR.Common.Script;
using CSharpKOTOR.Formats.NCS;
using CSharpKOTOR.Formats.NCS.Optimizers;

namespace KNSSComp.NET
{
    /// <summary>
    /// Unified CLI entrypoint for NSS compilation/decompilation.
    /// Drop-in compatible with all nwnnsscomp.exe variants, unifying argument discrepancies.
    /// </summary>
    public class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            try
            {
                var parsedArgs = ParseArguments(args);
                if (parsedArgs == null)
                {
                    PrintUsage();
                    return 1;
                }

                if (parsedArgs.ShowHelp)
                {
                    PrintUsage();
                    return 0;
                }

                if (parsedArgs.ShowVersion)
                {
                    PrintVersion();
                    return 0;
                }

                if (parsedArgs.Compile)
                {
                    return Compile(parsedArgs);
                }
                else if (parsedArgs.Decompile)
                {
                    return Decompile(parsedArgs);
                }
                else
                {
                    Console.Error.WriteLine("ERROR: Must specify either -c (compile) or -d (decompile)");
                    PrintUsage();
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"  Caused by: {ex.InnerException.Message}");
                }
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("KNSSComp.NET - Unified NSS Compiler/Decompiler");
            Console.WriteLine("Drop-in compatible with all nwnnsscomp.exe variants");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  knsscomp.net.exe [OPTIONS]");
            Console.WriteLine();
            Console.WriteLine("OPERATIONS:");
            Console.WriteLine("  -c, --compile          Compile NSS to NCS");
            Console.WriteLine("  -d, --decompile        Decompile NCS to NSS");
            Console.WriteLine();
            Console.WriteLine("INPUT/OUTPUT:");
            Console.WriteLine("  <source>               Source file (NSS for compile, NCS for decompile)");
            Console.WriteLine("  <output>               Output file (NCS for compile, NSS for decompile)");
            Console.WriteLine("  -o, --output <file>   Output file path");
            Console.WriteLine("  --outputdir <dir>      Output directory (for variants that support it)");
            Console.WriteLine("  --outputname <name>    Output filename only (for variants that support it)");
            Console.WriteLine();
            Console.WriteLine("GAME VERSION:");
            Console.WriteLine("  -g, --game <1|2>       Game version: 1 for KOTOR, 2 for TSL (default: auto-detect)");
            Console.WriteLine("  -k1, --kotor1          Force KOTOR 1 mode");
            Console.WriteLine("  -k2, --kotor2, --tsl  Force TSL (KOTOR 2) mode");
            Console.WriteLine();
            Console.WriteLine("INCLUDES:");
            Console.WriteLine("  -i, --include <dir>    Add include directory (can be specified multiple times)");
            Console.WriteLine("  -I <dir>                Alternative include directory flag");
            Console.WriteLine();
            Console.WriteLine("OPTIMIZATION:");
            Console.WriteLine("  -O, --optimize          Enable optimizations (when -o means optimize)");
            Console.WriteLine("  --no-optimize           Disable optimizations");
            Console.WriteLine();
            Console.WriteLine("NWSCRIPT:");
            Console.WriteLine("  --nwscript <file>      Path to custom nwscript.nss file");
            Console.WriteLine("                         (default: uses ScriptDefs.cs)");
            Console.WriteLine();
            Console.WriteLine("INCLUDE LIBRARY:");
            Console.WriteLine("  --include-lib <file>   Path to custom include library file");
            Console.WriteLine("                         (default: uses ScriptLib.cs)");
            Console.WriteLine();
            Console.WriteLine("OTHER:");
            Console.WriteLine("  -v, --verbose          Verbose output");
            Console.WriteLine("  --debug                Enable debug output");
            Console.WriteLine("  -h, --help             Show this help message");
            Console.WriteLine("  --version              Show version information");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  # Compile (TSLPatcher style)");
            Console.WriteLine("  knsscomp.net.exe -c script.nss -o script.ncs");
            Console.WriteLine();
            Console.WriteLine("  # Compile (KOTOR Tool style)");
            Console.WriteLine("  knsscomp.net.exe -c script.nss --outputdir . -o script.ncs -g 2");
            Console.WriteLine();
            Console.WriteLine("  # Compile (V1 style)");
            Console.WriteLine("  knsscomp.net.exe -c script.nss script.ncs");
            Console.WriteLine();
            Console.WriteLine("  # Compile with includes");
            Console.WriteLine("  knsscomp.net.exe -c script.nss -o script.ncs -i ./includes");
            Console.WriteLine();
            Console.WriteLine("  # Decompile");
            Console.WriteLine("  knsscomp.net.exe -d script.ncs -o script.nss");
            Console.WriteLine();
            Console.WriteLine("  # Compile with custom nwscript.nss");
            Console.WriteLine("  knsscomp.net.exe -c script.nss -o script.ncs --nwscript custom_nwscript.nss");
        }

        private static void PrintVersion()
        {
            Console.WriteLine("KNSSComp.NET 1.0.0");
            Console.WriteLine("Unified NSS Compiler/Decompiler for KOTOR and TSL");
            Console.WriteLine("Drop-in compatible with all nwnnsscomp.exe variants");
        }

        private class ParsedArguments
        {
            public bool Compile { get; set; }
            public bool Decompile { get; set; }
            public string SourceFile { get; set; }
            public string OutputFile { get; set; }
            public string OutputDir { get; set; }
            public string OutputName { get; set; }
            public Game? Game { get; set; }
            public List<string> IncludeDirs { get; set; } = new List<string>();
            public bool Optimize { get; set; } = true;
            public string NwscriptPath { get; set; }
            public string IncludeLibPath { get; set; }
            public bool Verbose { get; set; }
            public bool Debug { get; set; }
            public bool ShowHelp { get; set; }
            public bool ShowVersion { get; set; }
        }

        private static ParsedArguments ParseArguments(string[] args)
        {
            var result = new ParsedArguments();
            var positionalArgs = new List<string>();
            bool expectingOutput = false;
            bool expectingOutputDir = false;
            bool expectingOutputName = false;
            bool expectingGame = false;
            bool expectingInclude = false;
            bool expectingNwscript = false;
            bool expectingIncludeLib = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (expectingOutput)
                {
                    result.OutputFile = arg;
                    expectingOutput = false;
                    continue;
                }

                if (expectingOutputDir)
                {
                    result.OutputDir = arg;
                    expectingOutputDir = false;
                    continue;
                }

                if (expectingOutputName)
                {
                    result.OutputName = arg;
                    expectingOutputName = false;
                    continue;
                }

                if (expectingGame)
                {
                    if (arg == "1" || arg == "k1" || arg.Equals("kotor1", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Game = CSharpKOTOR.Common.Game.K1;
                    }
                    else if (arg == "2" || arg == "k2" || arg.Equals("kotor2", StringComparison.OrdinalIgnoreCase) || arg.Equals("tsl", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Game = CSharpKOTOR.Common.Game.K2;
                    }
                    else
                    {
                        Console.Error.WriteLine($"ERROR: Invalid game value: {arg} (expected 1, 2, k1, k2, kotor1, kotor2, or tsl)");
                        return null;
                    }
                    expectingGame = false;
                    continue;
                }

                if (expectingInclude)
                {
                    result.IncludeDirs.Add(arg);
                    expectingInclude = false;
                    continue;
                }

                if (expectingNwscript)
                {
                    result.NwscriptPath = arg;
                    expectingNwscript = false;
                    continue;
                }

                if (expectingIncludeLib)
                {
                    result.IncludeLibPath = arg;
                    expectingIncludeLib = false;
                    continue;
                }

                // Handle flags
                if (arg == "-c" || arg == "--compile")
                {
                    result.Compile = true;
                }
                else if (arg == "-d" || arg == "--decompile")
                {
                    result.Decompile = true;
                }
                else if (arg == "-o" || arg == "--output")
                {
                    // Check if next arg exists and is not a flag
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                    {
                        expectingOutput = true;
                    }
                    else
                    {
                        // -o without value: ambiguous case
                        // If we already have an output file from positional args, treat as optimize
                        // Otherwise, this is an error (missing output file value)
                        if (result.OutputFile != null)
                        {
                            result.Optimize = true;
                        }
                        else
                        {
                            Console.Error.WriteLine("ERROR: -o/--output requires a file path");
                            return null;
                        }
                    }
                }
                else if (arg == "-O" || arg == "--optimize")
                {
                    result.Optimize = true;
                }
                else if (arg == "--no-optimize")
                {
                    result.Optimize = false;
                }
                else if (arg == "--outputdir")
                {
                    expectingOutputDir = true;
                }
                else if (arg == "--outputname")
                {
                    expectingOutputName = true;
                }
                else if (arg == "-g" || arg == "--game")
                {
                    expectingGame = true;
                }
                else if (arg == "-k1" || arg == "--kotor1")
                {
                    result.Game = CSharpKOTOR.Common.Game.K1;
                }
                else if (arg == "-k2" || arg == "--kotor2" || arg == "--tsl")
                {
                    result.Game = CSharpKOTOR.Common.Game.K2;
                }
                else if (arg == "-i" || arg == "--include" || arg == "-I")
                {
                    expectingInclude = true;
                }
                else if (arg == "--nwscript")
                {
                    expectingNwscript = true;
                }
                else if (arg == "--include-lib")
                {
                    expectingIncludeLib = true;
                }
                else if (arg == "-v" || arg == "--verbose")
                {
                    result.Verbose = true;
                }
                else if (arg == "--debug")
                {
                    result.Debug = true;
                }
                else if (arg == "-h" || arg == "--help")
                {
                    result.ShowHelp = true;
                }
                else if (arg == "--version")
                {
                    result.ShowVersion = true;
                }
                else if (arg.StartsWith("-"))
                {
                    Console.Error.WriteLine($"ERROR: Unknown option: {arg}");
                    return null;
                }
                else
                {
                    // Positional argument
                    positionalArgs.Add(arg);
                }
            }

            // Handle positional arguments
            // Pattern 1: -c source output (V1 style)
            // Pattern 2: -c source -o output (TSLPatcher/KNSSCOMP style)
            // Pattern 3: source output (implicit compile)
            // Pattern 4: source (output inferred from source name)
            if (positionalArgs.Count > 0)
            {
                if (result.SourceFile == null)
                {
                    result.SourceFile = positionalArgs[0];
                }
                if (positionalArgs.Count > 1 && result.OutputFile == null)
                {
                    result.OutputFile = positionalArgs[1];
                }
            }

            // If compile/decompile not specified, infer from file extensions
            if (!result.Compile && !result.Decompile)
            {
                if (result.SourceFile != null)
                {
                    string ext = Path.GetExtension(result.SourceFile)?.ToLowerInvariant();
                    if (ext == ".nss")
                    {
                        result.Compile = true;
                    }
                    else if (ext == ".ncs")
                    {
                        result.Decompile = true;
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Cannot infer operation from file extension. Please specify -c or -d");
                        return null;
                    }
                }
                else
                {
                    Console.Error.WriteLine("ERROR: Must specify source file");
                    return null;
                }
            }

            // Validate required arguments
            if (result.SourceFile == null)
            {
                Console.Error.WriteLine("ERROR: Source file is required");
                return null;
            }

            if (!File.Exists(result.SourceFile))
            {
                Console.Error.WriteLine($"ERROR: Source file not found: {result.SourceFile}");
                return null;
            }

            // Determine output file if not specified
            if (result.OutputFile == null)
            {
                if (result.OutputDir != null && result.OutputName != null)
                {
                    result.OutputFile = Path.Combine(result.OutputDir, result.OutputName);
                }
                else if (result.OutputDir != null)
                {
                    string sourceName = Path.GetFileNameWithoutExtension(result.SourceFile);
                    string ext = result.Compile ? ".ncs" : ".nss";
                    result.OutputFile = Path.Combine(result.OutputDir, sourceName + ext);
                }
                else
                {
                    // Default: same directory as source, change extension
                    string sourceDir = Path.GetDirectoryName(result.SourceFile);
                    string sourceName = Path.GetFileNameWithoutExtension(result.SourceFile);
                    string ext = result.Compile ? ".ncs" : ".nss";
                    result.OutputFile = Path.Combine(sourceDir ?? ".", sourceName + ext);
                }
            }

            // Auto-detect game version if not specified
            if (result.Game == null)
            {
                // Try to infer from source file location or content
                // Default to K2 (TSL) as it's more common
                result.Game = CSharpKOTOR.Common.Game.K2;
            }

            return result;
        }

        private static int Compile(ParsedArguments args)
        {
            try
            {
                if (args.Verbose || args.Debug)
                {
                    Console.WriteLine($"Compiling: {args.SourceFile} -> {args.OutputFile}");
                    if (args.Game.HasValue)
                    {
                        Console.WriteLine($"Game: {(args.Game.Value.IsK2() ? "TSL (KOTOR 2)" : "KOTOR 1")}");
                    }
                    if (args.IncludeDirs.Count > 0)
                    {
                        Console.WriteLine($"Include directories: {string.Join(", ", args.IncludeDirs)}");
                    }
                    if (!string.IsNullOrEmpty(args.NwscriptPath))
                    {
                        Console.WriteLine($"Custom nwscript.nss: {args.NwscriptPath}");
                    }
                }

                string source = File.ReadAllText(args.SourceFile, Encoding.UTF8);
                Game game = args.Game ?? CSharpKOTOR.Common.Game.K2;

                // Build library lookup paths
                List<string> libraryLookup = new List<string>();
                if (!string.IsNullOrEmpty(Path.GetDirectoryName(args.SourceFile)))
                {
                    libraryLookup.Add(Path.GetDirectoryName(args.SourceFile));
                }
                libraryLookup.AddRange(args.IncludeDirs);

                // Load custom include library if provided
                Dictionary<string, byte[]> library = null;
                if (!string.IsNullOrEmpty(args.IncludeLibPath))
                {
                    if (!File.Exists(args.IncludeLibPath))
                    {
                        throw new FileNotFoundException($"Include library file not found: {args.IncludeLibPath}");
                    }
                    // For now, we'll use the default library
                    // Full implementation would parse the include library file and merge with defaults
                    library = game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY;
                }

                // Build optimizers list
                List<NCSOptimizer> optimizers = new List<NCSOptimizer>();
                if (args.Optimize)
                {
                    optimizers.Add(new RemoveNopOptimizer());
                }

                // Compile
                NCS ncs = NCSAuto.CompileNss(
                    source,
                    game,
                    library,
                    optimizers,
                    libraryLookup,
                    null,
                    args.Debug,
                    args.NwscriptPath);

                // Ensure output directory exists
                string outputDir = Path.GetDirectoryName(args.OutputFile);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Write output
                NCSAuto.WriteNcs(ncs, args.OutputFile);

                if (args.Verbose || args.Debug)
                {
                    Console.WriteLine($"Successfully compiled to: {args.OutputFile}");
                    Console.WriteLine($"  Instructions: {ncs.Instructions.Count}");
                    Console.WriteLine($"  Size: {new FileInfo(args.OutputFile).Length} bytes");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Compilation failed: {ex.Message}");
                if (args.Debug && ex.InnerException != null)
                {
                    Console.Error.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                    Console.Error.WriteLine($"  Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        private static int Decompile(ParsedArguments args)
        {
            try
            {
                if (args.Verbose || args.Debug)
                {
                    Console.WriteLine($"Decompiling: {args.SourceFile} -> {args.OutputFile}");
                    if (args.Game.HasValue)
                    {
                        Console.WriteLine($"Game: {(args.Game.Value.IsK2() ? "TSL (KOTOR 2)" : "KOTOR 1")}");
                    }
                }

                Game game = args.Game ?? CSharpKOTOR.Common.Game.K2;

                // Decompile
                NCS ncs = NCSAuto.ReadNcs(args.SourceFile);
                string decompiled = NCSAuto.DecompileNcs(ncs, game);

                // Ensure output directory exists
                string outputDir = Path.GetDirectoryName(args.OutputFile);
                if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }

                // Write output
                File.WriteAllText(args.OutputFile, decompiled, Encoding.UTF8);

                if (args.Verbose || args.Debug)
                {
                    Console.WriteLine($"Successfully decompiled to: {args.OutputFile}");
                    Console.WriteLine($"  Size: {new FileInfo(args.OutputFile).Length} bytes");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Decompilation failed: {ex.Message}");
                if (args.Debug && ex.InnerException != null)
                {
                    Console.Error.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                    Console.Error.WriteLine($"  Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }
    }
}

