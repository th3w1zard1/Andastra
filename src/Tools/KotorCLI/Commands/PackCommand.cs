using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class PackCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var packCommand = new Command("pack", "Convert, compile, and pack all sources for target");
            var targetsArgument = new Argument<string[]>("targets", () => new string[0], "Targets to pack (use 'all' for all targets)");
            packCommand.AddArgument(targetsArgument);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before packing");
            packCommand.AddOption(cleanOption);
            packCommand.SetHandler((string[] targets, bool clean) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Pack command not yet implemented");
            }, targetsArgument, cleanOption);
            rootCommand.AddCommand(packCommand);
        }
    }
}

