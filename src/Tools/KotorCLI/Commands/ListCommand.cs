using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class ListCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var listCommand = new Command("list", "List all targets defined in kotorcli.cfg");
            var targetsArgument = new Argument<string[]>("targets", () => new string[0], "Specific targets to list");
            listCommand.AddArgument(targetsArgument);
            var quietOption = new Option<bool>("--quiet", "List only target names");
            listCommand.AddOption(quietOption);
            var verboseOption = new Option<bool>("--verbose", "List source files as well");
            listCommand.AddOption(verboseOption);
            listCommand.SetHandler((string[] targets, bool quiet, bool verbose) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("List command not yet implemented");
            }, targetsArgument, quietOption, verboseOption);
            rootCommand.AddCommand(listCommand);
        }
    }
}

