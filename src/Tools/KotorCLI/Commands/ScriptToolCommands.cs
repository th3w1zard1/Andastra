using System;
using System.CommandLine;
using KotorCLI.Logging;

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
            var decompileInput = new Argument<string>("input", "Input NCS file");
            decompileCmd.Add(decompileInput);
            var decompileOutput = new Option<string>(new[] { "-o", "--output" }, "Output NSS file");
            decompileCmd.Options.Add(decompileOutput);
            decompileCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(decompileInput);
                var output = parseResult.GetValue(decompileOutput);
                var logger = new StandardLogger();
                logger.Info("decompile not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(decompileCmd);

            var disassembleCmd = new Command("disassemble", "Disassemble NCS bytecode to text");
            var disassembleInput = new Argument<string>("input", "Input NCS file");
            disassembleCmd.Add(disassembleInput);
            var disassembleOutput = new Option<string>(new[] { "-o", "--output" }, "Output text file");
            disassembleCmd.Options.Add(disassembleOutput);
            disassembleCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(disassembleInput);
                var output = parseResult.GetValue(disassembleOutput);
                var logger = new StandardLogger();
                logger.Info("disassemble not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(disassembleCmd);

            var assembleCmd = new Command("assemble", "Assemble/compile NSS source to NCS bytecode");
            var assembleInput = new Argument<string>("input", "Input NSS file");
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
                logger.Info("assemble not yet implemented (use NSSComp tool or compile command)");
                Environment.Exit(0);
            });
            rootCommand.Add(assembleCmd);
        }
    }
}
