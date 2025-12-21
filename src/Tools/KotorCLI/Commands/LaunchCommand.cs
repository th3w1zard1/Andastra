using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class LaunchCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            // launch, serve, play, test are all aliases for the same command
            foreach (string alias in new[] { "launch", "serve", "play", "test" })
            {
                var launchCommand = new Command(alias, "Convert, compile, pack, install, and launch target in-game");
                var targetsArgument = new Argument<string[]>("targets", () => new string[0], "Target to launch");
                launchCommand.AddArgument(targetsArgument);
                launchCommand.SetHandler((string[] targets) =>
                {
                    var logger = new StandardLogger();
                    // TODO: Implement
                    logger.Info("Launch command not yet implemented");
                }, targetsArgument);
                rootCommand.AddCommand(launchCommand);
            }
        }
    }
}

