using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Common.Script;
using BioWare.NET.Resource.Formats.NCS;
using BioWare.NET.Resource.Formats.NCS.Optimizers;

namespace NSSComp
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
            Console.WriteLine("NSSComp - Unified NSS Compiler/Decompiler");
            Console.WriteLine("Drop-in compatible with all nwnnsscomp.exe variants");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  nsscomp [OPTIONS]");
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
            Console.WriteLine("INCLUDES (compile mode):");
            Console.WriteLine("  -i, --include <dir>    Add include directory (can be specified multiple times)");
            Console.WriteLine("  -I <dir>                Alternative include directory flag");
            Console.WriteLine();
            Console.WriteLine("INPUT (decompile mode):");
            Console.WriteLine("  -i, --input <path>     Input .ncs file or directory (can repeat for multiple files)");
            Console.WriteLine();
            Console.WriteLine("OPTIMIZATION (compile mode):");
            Console.WriteLine("  -O, --optimize          Enable optimizations");
            Console.WriteLine("  --no-optimize           Disable optimizations");
            Console.WriteLine();
            Console.WriteLine("OUTPUT DIRECTORY (decompile mode):");
            Console.WriteLine("  -O, --out-dir <dir>    Output directory (defaults to input directory)");
            Console.WriteLine("  --outputdir <dir>      Alternative output directory flag");
            Console.WriteLine();
            Console.WriteLine("NWSCRIPT:");
            Console.WriteLine("  --nwscript <file>      Path to custom nwscript.nss file");
            Console.WriteLine("                         (default: uses ScriptDefs.cs)");
            Console.WriteLine();
            Console.WriteLine("INCLUDE LIBRARY:");
            Console.WriteLine("  --include-lib <file>   Path to custom include library file");
            Console.WriteLine("                         (default: uses ScriptLib.cs)");
            Console.WriteLine();
            Console.WriteLine("DECOMPILATION OPTIONS:");
            Console.WriteLine("  -r, --recursive        Recurse into directories when inputs are dirs");
            Console.WriteLine("  --stdout               Write decompiled source to stdout (single file only)");
            Console.WriteLine("  --overwrite            Overwrite existing files");
            Console.WriteLine("  --quiet                Suppress success logs");
            Console.WriteLine("  --fail-fast            Stop on first decompile failure");
            Console.WriteLine("  --prefix <text>        Prefix for generated filenames");
            Console.WriteLine("  --suffix <text>        Suffix for generated filenames");
            Console.WriteLine("  --ext <ext>            Output extension (default: .nss)");
            Console.WriteLine("  --encoding <name>      Output charset (default: UTF-8)");
            Console.WriteLine();
            Console.WriteLine("OTHER:");
            Console.WriteLine("  -v, --verbose          Verbose output");
            Console.WriteLine("  --debug                Enable debug output");
            Console.WriteLine("  -h, --help             Show this help message");
            Console.WriteLine("  --version              Show version information");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  # Compile (TSLPatcher style)");
            Console.WriteLine("  nsscomp -c script.nss -o script.ncs");
            Console.WriteLine();
            Console.WriteLine("  # Compile (KOTOR Tool style)");
            Console.WriteLine("  nsscomp -c script.nss --outputdir . -o script.ncs -g 2");
            Console.WriteLine();
            Console.WriteLine("  # Compile (V1 style)");
            Console.WriteLine("  nsscomp -c script.nss script.ncs");
            Console.WriteLine();
            Console.WriteLine("  # Compile with includes");
            Console.WriteLine("  nsscomp -c script.nss -o script.ncs -i ./includes");
            Console.WriteLine();
            Console.WriteLine("  # Decompile single file");
            Console.WriteLine("  nsscomp -d script.ncs -o script.nss");
            Console.WriteLine();
            Console.WriteLine("  # Decompile to stdout");
            Console.WriteLine("  nsscomp -d script.ncs --stdout");
            Console.WriteLine();
            Console.WriteLine("  # Decompile directory recursively");
            Console.WriteLine("  nsscomp -d -i scripts_dir -r -O output_dir");
            Console.WriteLine();
            Console.WriteLine("  # Decompile multiple files");
            Console.WriteLine("  nsscomp -d -i file1.ncs -i file2.ncs -O output");
            Console.WriteLine();
            Console.WriteLine("  # Compile with custom nwscript.nss");
            Console.WriteLine("  nsscomp -c script.nss -o script.ncs --nwscript custom_nwscript.nss");
        }

        private static void PrintVersion()
        {
            Console.WriteLine("NSSComp 1.0.0");
            Console.WriteLine("Unified NSS Compiler/Decompiler for KOTOR and TSL");
            Console.WriteLine("Drop-in compatible with all nwnnsscomp.exe variants");
        }

        private class ParsedArguments
        {
            public bool Compile { get; set; }
            public bool Decompile { get; set; }
            public string SourceFile { get; set; }
            public List<string> InputFiles { get; set; } = new List<string>();
            public string OutputFile { get; set; }
            public string OutputDir { get; set; }
            public string OutputName { get; set; }
            public BioWareGame? Game { get; set; }
            public List<string> IncludeDirs { get; set; } = new List<string>();
            public bool Optimize { get; set; } = true;
            public string NwscriptPath { get; set; }
            public string IncludeLibPath { get; set; }
            public bool Verbose { get; set; }
            public bool Debug { get; set; }
            public bool ShowHelp { get; set; }
            public bool ShowVersion { get; set; }
            // Decompilation-specific options
            public bool Recursive { get; set; }
            public bool Stdout { get; set; }
            public bool Overwrite { get; set; }
            public bool Quiet { get; set; }
            public bool FailFast { get; set; }
            public string Prefix { get; set; }
            public string Suffix { get; set; }
            public string OutputExt { get; set; } = ".nss";
            public string Encoding { get; set; } = "UTF-8";
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
            bool expectingInput = false;
            bool expectingNwscript = false;
            bool expectingIncludeLib = false;
            bool expectingPrefix = false;
            bool expectingSuffix = false;
            bool expectingExt = false;
            bool expectingEncoding = false;

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
                        result.Game = BioWare.NET.Common.BioWareGame.K1;
                    }
                    else if (arg == "2" || arg == "k2" || arg.Equals("kotor2", StringComparison.OrdinalIgnoreCase) || arg.Equals("tsl", StringComparison.OrdinalIgnoreCase))
                    {
                        result.Game = BioWare.NET.Common.BioWareGame.K2;
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
                    // Context-dependent: -i means include dir in compile mode, input file in decompile mode
                    // If mode not yet determined, check if it's a directory (include) or file (input)
                    if (result.Compile)
                    {
                        result.IncludeDirs.Add(arg);
                    }
                    else if (result.Decompile)
                    {
                        result.InputFiles.Add(arg);
                    }
                        else
                        {
                            // Mode not determined yet - check if it's a directory or file
                            if (Directory.Exists(arg))
                            {
                                result.IncludeDirs.Add(arg);
                            }
                            else if (File.Exists(arg))
                            {
                                // Try to infer mode from file extension
                                string ext = Path.GetExtension(arg)?.ToLowerInvariant();
                                if (ext == ".nss")
                                {
                                    // .nss file suggests compile mode - treat as source file
                                    if (result.SourceFile == null)
                                    {
                                        result.SourceFile = arg;
                                        result.Compile = true;
                                    }
                                    else
                                    {
                                        Console.Error.WriteLine($"ERROR: Multiple source files specified. Already have: {result.SourceFile}");
                                        return null;
                                    }
                                }
                                else if (ext == ".ncs")
                                {
                                    // .ncs file suggests decompile mode - treat as input file
                                    result.InputFiles.Add(arg);
                                    result.Decompile = true;
                                }
                                else
                                {
                                    // Unknown extension - could be either, defer decision
                                    result.InputFiles.Add(arg);
                                }
                            }
                            else
                            {
                                Console.Error.WriteLine($"ERROR: Path not found: {arg}");
                                return null;
                            }
                        }
                    expectingInclude = false;
                    continue;
                }

                if (expectingInput)
                {
                    result.InputFiles.Add(arg);
                    expectingInput = false;
                    continue;
                }

                if (expectingPrefix)
                {
                    result.Prefix = arg;
                    expectingPrefix = false;
                    continue;
                }

                if (expectingSuffix)
                {
                    result.Suffix = arg;
                    expectingSuffix = false;
                    continue;
                }

                if (expectingExt)
                {
                    result.OutputExt = arg.StartsWith(".") ? arg : "." + arg;
                    expectingExt = false;
                    continue;
                }

                if (expectingEncoding)
                {
                    result.Encoding = arg;
                    expectingEncoding = false;
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
                else if (arg == "-O" || arg == "--out-dir")
                {
                    // Context-dependent: -O means optimize in compile mode, output dir in decompile mode
                    if (result.Compile)
                    {
                        result.Optimize = true;
                    }
                    else if (result.Decompile)
                    {
                        expectingOutputDir = true;
                    }
                    else
                    {
                        // Mode not determined - check if next arg is a directory path
                        if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        {
                            // Likely output directory
                            expectingOutputDir = true;
                        }
                        else
                        {
                            // No value, likely optimize flag
                            result.Optimize = true;
                        }
                    }
                }
                else if (arg == "--optimize")
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
                    result.Game = BioWare.NET.Common.BioWareGame.K1;
                }
                else if (arg == "-k2" || arg == "--kotor2" || arg == "--tsl")
                {
                    result.Game = BioWare.NET.Common.BioWareGame.K2;
                }
                else if (arg == "-i" || arg == "--include" || arg == "--input")
                {
                    // Context-dependent: -i means include dir in compile mode, input file in decompile mode
                    // Use --input explicitly for decompile mode, -i/--include for compile mode
                    if (arg == "--input")
                    {
                        expectingInput = true;
                    }
                    else
                    {
                        expectingInclude = true;
                    }
                }
                else if (arg == "-I")
                {
                    // Always means include directory
                    expectingInclude = true;
                }
                else if (arg == "-r" || arg == "--recursive")
                {
                    result.Recursive = true;
                }
                else if (arg == "--stdout")
                {
                    result.Stdout = true;
                }
                else if (arg == "--overwrite")
                {
                    result.Overwrite = true;
                }
                else if (arg == "--quiet")
                {
                    result.Quiet = true;
                }
                else if (arg == "--fail-fast")
                {
                    result.FailFast = true;
                }
                else if (arg == "--prefix")
                {
                    expectingPrefix = true;
                }
                else if (arg == "--suffix")
                {
                    expectingSuffix = true;
                }
                else if (arg == "--ext")
                {
                    expectingExt = true;
                }
                else if (arg == "--encoding")
                {
                    expectingEncoding = true;
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
            // Pattern 2: -c source -o output (TSLPatcher/NSSCOMP style)
            // Pattern 3: source output (implicit compile)
            // Pattern 4: source (output inferred from source name)
            // Pattern 5: -d -i file1.ncs -i file2.ncs (multiple inputs)
            if (positionalArgs.Count > 0)
            {
                if (result.Decompile && result.InputFiles.Count == 0)
                {
                    // In decompile mode, positional args are input files
                    result.InputFiles.AddRange(positionalArgs);
                }
                else
                {
                    // In compile mode or single file mode, first is source, second is output
                    if (result.SourceFile == null && result.InputFiles.Count == 0)
                    {
                        result.SourceFile = positionalArgs[0];
                    }
                    if (positionalArgs.Count > 1 && result.OutputFile == null)
                    {
                        result.OutputFile = positionalArgs[1];
                    }
                }
            }

            // If compile/decompile not specified, infer from file extensions
            if (!result.Compile && !result.Decompile)
            {
                // Check input files first (decompile mode)
                if (result.InputFiles.Count > 0)
                {
                    string firstInput = result.InputFiles[0];
                    if (File.Exists(firstInput))
                    {
                        string ext = Path.GetExtension(firstInput)?.ToLowerInvariant();
                        if (ext == ".ncs")
                        {
                            result.Decompile = true;
                        }
                        else if (ext == ".nss")
                        {
                            result.Compile = true;
                            // Move to SourceFile
                            result.SourceFile = firstInput;
                            result.InputFiles.RemoveAt(0);
                        }
                    }
                }

                // Check SourceFile (compile mode)
                if (!result.Compile && !result.Decompile && result.SourceFile != null)
                {
                    string ext = Path.GetExtension(result.SourceFile)?.ToLowerInvariant();
                    if (ext == ".nss")
                    {
                        result.Compile = true;
                    }
                    else if (ext == ".ncs")
                    {
                        result.Decompile = true;
                        // Move to InputFiles
                        result.InputFiles.Add(result.SourceFile);
                        result.SourceFile = null;
                    }
                    else
                    {
                        Console.Error.WriteLine("ERROR: Cannot infer operation from file extension. Please specify -c or -d");
                        return null;
                    }
                }

                if (!result.Compile && !result.Decompile)
                {
                    Console.Error.WriteLine("ERROR: Must specify operation (-c or -d) or provide file with .nss or .ncs extension");
                    return null;
                }
            }

            // Resolve ambiguous -i arguments now that we know the mode
            if (result.Compile && result.InputFiles.Count > 0)
            {
                // In compile mode, any files in InputFiles should be moved to IncludeDirs if they're directories
                // or treated as source files
                foreach (string input in result.InputFiles)
                {
                    if (Directory.Exists(input))
                    {
                        result.IncludeDirs.Add(input);
                    }
                    else if (result.SourceFile == null && File.Exists(input))
                    {
                        result.SourceFile = input;
                    }
                }
                result.InputFiles.Clear();
            }

            // Validate required arguments
            if (result.Decompile)
            {
                // In decompile mode, we need input files
                if (result.InputFiles.Count == 0 && result.SourceFile == null)
                {
                    Console.Error.WriteLine("ERROR: Input file(s) required for decompilation. Use -i/--input or positional arguments.");
                    return null;
                }

                // If SourceFile is set but InputFiles is empty, add it
                if (result.SourceFile != null && result.InputFiles.Count == 0)
                {
                    result.InputFiles.Add(result.SourceFile);
                }

                // Validate input files exist
                foreach (string inputFile in result.InputFiles)
                {
                    if (!File.Exists(inputFile) && !Directory.Exists(inputFile))
                    {
                        Console.Error.WriteLine($"ERROR: Input file/directory not found: {inputFile}");
                        return null;
                    }
                }
            }
            else
            {
                // In compile mode, we need a source file
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
                result.Game = BioWare.NET.Common.BioWareGame.K2;
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
                BioWareGame game = args.Game ?? BioWare.NET.Common.BioWareGame.K2;

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
                    // TODO: STUB - For now, we'll use the default library
                    // TODO:  Full implementation would parse the include library file and merge with defaults
                    library = game.IsK1() ? ScriptLib.KOTOR_LIBRARY : ScriptLib.TSL_LIBRARY;
                }

                // Build optimizers list
                List<NCSOptimizer> optimizers = new List<NCSOptimizer>();
                if (args.Optimize)
                {
                    optimizers.Add(new RemoveNopOptimizer());
                }

                // Compile
                // TODO: nwscriptPath parameter not supported in current CompileNss signature
                // The nwscriptPath would need to be added to libraryLookup if needed
                if (args.NwscriptPath != null && !string.IsNullOrEmpty(args.NwscriptPath))
                {
                    string nwscriptDir = System.IO.Path.GetDirectoryName(args.NwscriptPath);
                    if (!string.IsNullOrEmpty(nwscriptDir) && (libraryLookup == null || !libraryLookup.Contains(nwscriptDir)))
                    {
                        libraryLookup = libraryLookup ?? new List<string>();
                        libraryLookup.Add(nwscriptDir);
                    }
                }
                NCS ncs = NCSAuto.CompileNss(
                    source,
                    game,
                    optimizers,
                    libraryLookup,
                    args.Debug);

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
                BioWareGame game = args.Game ?? BioWare.NET.Common.BioWareGame.K2;
                Encoding encoding = GetEncoding(args.Encoding);

                // Collect all input files
                List<string> inputFiles = new List<string>();
                foreach (string input in args.InputFiles)
                {
                    if (File.Exists(input) && input.ToLowerInvariant().EndsWith(".ncs"))
                    {
                        inputFiles.Add(input);
                    }
                    else if (Directory.Exists(input))
                    {
                        CollectNcsFiles(input, args.Recursive, inputFiles);
                    }
                }

                if (inputFiles.Count == 0)
                {
                    Console.Error.WriteLine("ERROR: No .ncs files found in input");
                    return 1;
                }

                if (args.Stdout && inputFiles.Count > 1)
                {
                    Console.Error.WriteLine("ERROR: --stdout can only be used with a single input file");
                    return 1;
                }

                if (args.Verbose || args.Debug)
                {
                    Console.WriteLine($"Decompiling {inputFiles.Count} file(s)");
                    if (args.Game.HasValue)
                    {
                        Console.WriteLine($"Game: {(args.Game.Value.IsK2() ? "TSL (KOTOR 2)" : "KOTOR 1")}");
                    }
                    if (!string.IsNullOrEmpty(args.NwscriptPath))
                    {
                        Console.WriteLine($"Custom nwscript.nss: {args.NwscriptPath}");
                    }
                }

                int successCount = 0;
                int errorCount = 0;

                foreach (string inputFile in inputFiles)
                {
                    try
                    {
                        // Determine output file
                        string outputFile;
                        if (args.Stdout)
                        {
                            outputFile = null; // Will write to stdout
                        }
                        else if (inputFiles.Count == 1 && !string.IsNullOrEmpty(args.OutputFile))
                        {
                            outputFile = args.OutputFile;
                        }
                        else
                        {
                            // Generate output filename
                            string inputDir = Path.GetDirectoryName(inputFile);
                            string inputName = Path.GetFileNameWithoutExtension(inputFile);
                            string outputDir = args.OutputDir ?? inputDir ?? ".";

                            string outputName = (args.Prefix ?? "") + inputName + (args.Suffix ?? "") + args.OutputExt;
                            outputFile = Path.Combine(outputDir, outputName);
                        }

                        // Decompile
                        NCS ncs = NCSAuto.ReadNcs(inputFile);
                        string decompiled = NCSAuto.DecompileNcs(ncs, game);

                        // Write output
                        if (args.Stdout)
                        {
                            Console.WriteLine($"// {Path.GetFileName(inputFile)}");
                            Console.WriteLine(decompiled);
                        }
                        else
                        {
                            // Ensure output directory exists
                            string outputDir = Path.GetDirectoryName(outputFile);
                            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                            {
                                Directory.CreateDirectory(outputDir);
                            }

                            // Check if file exists
                            if (File.Exists(outputFile) && !args.Overwrite)
                            {
                                Console.Error.WriteLine($"ERROR: Output file already exists: {outputFile} (use --overwrite to overwrite)");
                                if (args.FailFast)
                                {
                                    return 1;
                                }
                                errorCount++;
                                continue;
                            }

                            File.WriteAllText(outputFile, decompiled, encoding);

                            if (!args.Quiet)
                            {
                                Console.WriteLine($"Decompiled: {inputFile} -> {outputFile}");
                            }
                            if (args.Verbose || args.Debug)
                            {
                                Console.WriteLine($"  Size: {new FileInfo(outputFile).Length} bytes");
                            }
                        }

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to decompile {inputFile}: {ex.Message}");
                        if (args.Debug && ex.InnerException != null)
                        {
                            Console.Error.WriteLine($"  Inner exception: {ex.InnerException.Message}");
                            Console.Error.WriteLine($"  Stack trace: {ex.StackTrace}");
                        }
                        errorCount++;
                        if (args.FailFast)
                        {
                            return 1;
                        }
                    }
                }

                if (!args.Quiet && !args.Stdout)
                {
                    Console.WriteLine($"Completed: {successCount} succeeded, {errorCount} failed");
                }

                return errorCount > 0 ? 1 : 0;
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

        private static void CollectNcsFiles(string directory, bool recursive, List<string> files)
        {
            try
            {
                foreach (string file in Directory.GetFiles(directory, "*.ncs", SearchOption.TopDirectoryOnly))
                {
                    files.Add(file);
                }

                if (recursive)
                {
                    foreach (string subdir in Directory.GetDirectories(directory))
                    {
                        CollectNcsFiles(subdir, true, files);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Error scanning directory {directory}: {ex.Message}");
            }
        }

        private static Encoding GetEncoding(string encodingName)
        {
            try
            {
                return Encoding.GetEncoding(encodingName);
            }
            catch
            {
                Console.Error.WriteLine($"Warning: Unknown encoding '{encodingName}', using UTF-8");
                return Encoding.UTF8;
            }
        }
    }
}

