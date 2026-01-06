using System;
using System.CommandLine;
using System.IO;
using KotorCLI.Logging;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.NCS;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Script tool commands (decompile, disassemble, assemble).
    /// </summary>
    public static class ScriptToolCommands
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var decompileCmd = new Command("decompile", "Decompile NCS bytecode to NSS source");
            var decompileInput = new Argument<string>("input");
            decompileInput.Description = "Input NCS file";
            decompileCmd.Add(decompileInput);
            var decompileOutput = new Option<string>(new[] { "-o", "--output" }, "Output NSS file");
            decompileCmd.Options.Add(decompileOutput);
            var gameOption = new Option<string>(new[] { "-g", "--game" }, "Target game (k1 or k2). Defaults to k2.");
            gameOption.SetDefaultValue("k2");
            decompileCmd.Options.Add(gameOption);
            decompileCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(decompileInput);
                var output = parseResult.GetValue(decompileOutput);
                var game = parseResult.GetValue(gameOption);
                var logger = new StandardLogger();

                try
                {
                    // Validate input file
                    if (!File.Exists(input))
                    {
                        logger.Error($"Input file does not exist: {input}");
                        Environment.Exit(1);
                    }

                    // Determine game type
                    BioWareGame gameType = BioWareGame.KOTOR; // Default to KOTOR
                    if (string.Equals(game, "k1", StringComparison.OrdinalIgnoreCase))
                    {
                        gameType = BioWareGame.KOTOR;
                    }
                    else if (string.Equals(game, "k2", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(game, "tsl", StringComparison.OrdinalIgnoreCase))
                    {
                        gameType = BioWareGame.TSL;
                    }
                    else
                    {
                        logger.Error($"Invalid game type: {game}. Must be 'k1' or 'k2'.");
                        Environment.Exit(1);
                        return; // Unreachable but helps compiler
                    }

                    // Determine output file path
                    string outputFile = output;
                    if (string.IsNullOrEmpty(outputFile))
                    {
                        // Default: same directory as input, change extension to .nss
                        string inputDir = Path.GetDirectoryName(input);
                        string inputName = Path.GetFileNameWithoutExtension(input);
                        outputFile = Path.Combine(inputDir, inputName + ".nss");
                    }

                    // Ensure output directory exists
                    string outputDir = Path.GetDirectoryName(outputFile);
                    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }

                    logger.Info($"Decompiling: {input}");
                    logger.Info($"Game: {gameType}");
                    logger.Info($"Output: {outputFile}");

                    // Read NCS file
                    NCS ncs = NCSAuto.ReadNcs(input);

                    // Decompile to NSS source
                    string nssCode = NCSAuto.DecompileNcs(ncs, gameType);

                    // Write output
                    File.WriteAllText(outputFile, nssCode, System.Text.Encoding.UTF8);

                    logger.Info($"Successfully decompiled {nssCode.Length} characters");
                }
                catch (Exception ex)
                {
                    logger.Error($"Decompilation failed: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        logger.Error($"Inner exception: {ex.InnerException.Message}");
                    }
                    Environment.Exit(1);
                }
            });
            rootCommand.Add(decompileCmd);

            var disassembleCmd = new Command("disassemble", "Disassemble NCS bytecode to text");
            var disassembleInput = new Argument<string>("input");
            disassembleInput.Description = "Input NCS file";
            disassembleCmd.Add(disassembleInput);
            var disassembleOutput = new Option<string>(new[] { "-o", "--output" }, "Output text file");
            disassembleCmd.Options.Add(disassembleOutput);
            disassembleCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(disassembleInput);
                var output = parseResult.GetValue(disassembleOutput);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - disassemble not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(disassembleCmd);

            var assembleCmd = new Command("assemble", "Assemble/compile NSS source to NCS bytecode");
            var assembleInput = new Argument<string>("input");
            assembleInput.Description = "Input NSS file";
            assembleCmd.Add(assembleInput);
            var assembleOutput = new Option<string>(new[] { "-o", "--output" }, "Output NCS file");
            assembleCmd.Options.Add(assembleOutput);
            var includeOption = new Option<string[]>(new[] { "-I", "--include" }, "Include directory for #include files");
            assembleCmd.Options.Add(includeOption);
            var debugOption = new Option<bool>("--debug", "Enable debug output");
            assembleCmd.Options.Add(debugOption);
            assembleCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(assembleInput);
                var output = parseResult.GetValue(assembleOutput);
                var includes = parseResult.GetValue(includeOption);
                var debug = parseResult.GetValue(debugOption);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - assemble not yet implemented (use NSSComp tool or compile command)");
                Environment.Exit(0);
            });
            rootCommand.Add(assembleCmd);
        }
    }
}
