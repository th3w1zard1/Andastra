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
            decompileCmd.AddArgument(decompileInput);
            var decompileOutput = new Option<string>(new[] { "-o", "--output" }, "Output NSS file");
            decompileCmd.AddOption(decompileOutput);
            decompileCmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("decompile not yet implemented"); }, decompileInput, decompileOutput);
            rootCommand.AddCommand(decompileCmd);

            var disassembleCmd = new Command("disassemble", "Disassemble NCS bytecode to text");
            var disassembleInput = new Argument<string>("input", "Input NCS file");
            disassembleCmd.AddArgument(disassembleInput);
            var disassembleOutput = new Option<string>(new[] { "-o", "--output" }, "Output text file");
            disassembleCmd.AddOption(disassembleOutput);
            disassembleCmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("disassemble not yet implemented"); }, disassembleInput, disassembleOutput);
            rootCommand.AddCommand(disassembleCmd);

            var assembleCmd = new Command("assemble", "Assemble/compile NSS source to NCS bytecode");
            var assembleInput = new Argument<string>("input", "Input NSS file");
            assembleCmd.AddArgument(assembleInput);
            var assembleOutput = new Option<string>(new[] { "-o", "--output" }, "Output NCS file");
            assembleCmd.AddOption(assembleOutput);
            assembleCmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("assemble not yet implemented"); }, assembleInput, assembleOutput);
            rootCommand.AddCommand(assembleCmd);
        }
    }
}

