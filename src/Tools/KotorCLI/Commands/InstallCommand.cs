using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class InstallCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var installCommand = new Command("install", "Convert, compile, pack, and install target");
            var targetsArgument = new Argument<string[]>("targets", () => new string[0], "Targets to install (use 'all' for all targets)");
            installCommand.AddArgument(targetsArgument);
            var installDirOption = new Option<string>("--installDir", "The location of the KOTOR user directory");
            installCommand.AddOption(installDirOption);
            installCommand.SetHandler((string[] targets, string installDir) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Install command not yet implemented");
            }, targetsArgument, installDirOption);
            rootCommand.AddCommand(installCommand);
        }
    }
}

