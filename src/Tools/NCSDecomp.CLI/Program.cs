using System;
using System.IO;
using NcsDecompiler = BioWare.NET.NCS.Core.Decompiler.NcsDecompiler;

namespace NCSDecomp.CLI
{
    class Program
    {
        public static void Main(string[] args)
        {
            // Parse command line arguments
            CommandLineArgs cmdlineArgs = ParseArgs(args);

            if (cmdlineArgs.Help)
            {
                Console.WriteLine("NCSDecomp.CLI - NCS File Decompiler (Command Line)");
                Console.WriteLine("Usage: NCSDecomp.CLI [options] <file1.ncs> [file2.ncs ...]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --output-dir <path>  Output directory for decompiled files");
                Console.WriteLine("  --help               Show this help message");
                Console.WriteLine();
                Environment.Exit(0);
                return;
            }

            if (cmdlineArgs.Files == null || cmdlineArgs.Files.Length == 0)
            {
                Console.Error.WriteLine("[Error] No files specified");
                Console.Error.WriteLine("Use --help for usage information");
                Environment.Exit(1);
                return;
            }

            // Ensure output directory exists if provided
            if (!string.IsNullOrEmpty(cmdlineArgs.OutputDir))
            {
                if (!Directory.Exists(cmdlineArgs.OutputDir))
                {
                    try
                    {
                        Directory.CreateDirectory(cmdlineArgs.OutputDir);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Error] Cannot create output directory '{cmdlineArgs.OutputDir}': {ex.Message}");
                        Environment.Exit(1);
                        return;
                    }
                }
            }

            // Create decompiler
            var decompiler = new NcsDecompiler();

            int successCount = 0;
            int errorCount = 0;

            foreach (string filePath in cmdlineArgs.Files)
            {
                try
                {
                    if (!System.IO.File.Exists(filePath))
                    {
                        Console.Error.WriteLine($"[Error] File not found: {filePath}");
                        errorCount++;
                        continue;
                    }

                    Console.WriteLine($"[Info] Decompiling: {filePath}");

                    string generatedCode = decompiler.DecompileFromFile(filePath);
                    if (!string.IsNullOrEmpty(generatedCode))
                    {
                        // Output to file or console
                        string outputPath = Path.ChangeExtension(filePath, ".nss");
                        if (!string.IsNullOrEmpty(cmdlineArgs.OutputDir))
                        {
                            string fileName = Path.GetFileName(outputPath);
                            outputPath = Path.Combine(cmdlineArgs.OutputDir, fileName);
                        }

                        System.IO.File.WriteAllText(outputPath, generatedCode);
                        Console.WriteLine($"[Info] Decompiled code written to: {outputPath}");
                        successCount++;
                    }
                    else
                    {
                        Console.Error.WriteLine($"[Warning] No code generated for: {filePath}");
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[Error] Exception decompiling {filePath}: {ex.GetType().Name}: {ex.Message}");
                    errorCount++;
                }
            }

            Console.WriteLine($"[Info] Completed: {successCount} succeeded, {errorCount} failed");
            Environment.Exit(errorCount > 0 ? 1 : 0);
        }

        private static CommandLineArgs ParseArgs(string[] args)
        {
            var result = new CommandLineArgs();
            var fileList = new System.Collections.Generic.List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--output-dir" when i + 1 < args.Length:
                        result.OutputDir = args[++i];
                        break;
                    case "--help":
                        result.Help = true;
                        break;
                    default:
                        // Treat as file path if it doesn't start with --
                        if (!args[i].StartsWith("--"))
                        {
                            fileList.Add(args[i]);
                        }
                        break;
                }
            }

            result.Files = fileList.ToArray();
            return result;
        }

        private class CommandLineArgs
        {
            public string[] Files { get; set; }
            public string OutputDir { get; set; }
            public bool Help { get; set; }
        }
    }
}