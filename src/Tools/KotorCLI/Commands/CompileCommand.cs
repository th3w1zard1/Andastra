using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class CompileCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var compileCommand = new Command("compile", "Compile all nss sources for target");
            var targetsArgument = new Argument<string[]>("targets", () => new string[0], "Targets to compile (use 'all' for all targets)");
            compileCommand.AddArgument(targetsArgument);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before compiling");
            compileCommand.AddOption(cleanOption);
            var fileOption = new Option<string[]>(new[] { "-f", "--file" }, "Compile specific file(s)");
            compileCommand.AddOption(fileOption);
            compileCommand.SetHandler((string[] targets, bool clean, string[] files) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Compile command not yet implemented");
            }, targetsArgument, cleanOption, fileOption);
            rootCommand.AddCommand(compileCommand);
        }
    }
}

