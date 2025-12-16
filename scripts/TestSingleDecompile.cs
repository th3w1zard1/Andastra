using System;
using System.IO;
using AuroraEngine.Common.Formats.NCS.NCSDecomp;
using AuroraEngine.Common.Common;

class TestSingleDecompile
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: TestSingleDecompile <ncs-file>");
            Environment.Exit(1);
        }

        string ncsFile = args[0];
        if (!File.Exists(ncsFile))
        {
            Console.Error.WriteLine($"File not found: {ncsFile}");
            Environment.Exit(1);
        }

        try
        {
            FileDecompiler.isK2Selected = false; // K1
            FileDecompiler decompiler = new FileDecompiler();
            decompiler.LoadActionsData(false); // K1

            NcsFile ncs = new NcsFile(ncsFile);
            string output = Path.ChangeExtension(ncsFile, ".dec.nss");
            NcsFile outputFile = new NcsFile(output);

            decompiler.DecompileToFile(ncs, outputFile, System.Text.Encoding.UTF8, true);

            if (File.Exists(output))
            {
                Console.WriteLine("=== DECOMPILED OUTPUT ===");
                Console.WriteLine(File.ReadAllText(output));
            }
            else
            {
                Console.Error.WriteLine("Decompilation failed - no output file created");
                Environment.Exit(1);
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error: {e.Message}");
            Console.Error.WriteLine(e.StackTrace);
            Environment.Exit(1);
        }
    }
}

