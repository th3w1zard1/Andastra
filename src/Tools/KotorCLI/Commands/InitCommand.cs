using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class InitCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var initCommand = new Command("init", "Create a new kotorcli package");
            var dirArgument = new Argument<string>("dir", () => ".", "Directory to initialize (default: current directory)");
            initCommand.AddArgument(dirArgument);
            var fileArgument = new Argument<string>("file", () => null, "File to unpack into the new package");
            initCommand.AddArgument(fileArgument);
            var defaultOption = new Option<bool>("--default", "Skip package generation dialog");
            initCommand.AddOption(defaultOption);
            var vcsOption = new Option<string>("--vcs", () => "git", "Version control system to use");
            initCommand.AddOption(vcsOption);
            initCommand.SetHandler((string dir, string file, bool defaultMode, string vcs) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Init command not yet implemented");
            }, dirArgument, fileArgument, defaultOption, vcsOption);
            rootCommand.AddCommand(initCommand);
        }
    }
}

