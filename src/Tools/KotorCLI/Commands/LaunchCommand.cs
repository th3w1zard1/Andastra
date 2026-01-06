using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using KotorCLI.Configuration;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Launch command - Convert, compile, pack, install, and launch target in-game.
    /// Aliases: serve, play, test
    /// </summary>
    public static class LaunchCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            // launch, serve, play, test are all aliases for the same command
            foreach (string alias in new[] { "launch", "serve", "play", "test" })
            {
                var launchCommand = new Command(alias, "Convert, compile, pack, install, and launch target in-game");
                var targetsArgument = new Argument<string[]>("targets");
                targetsArgument.Description = "Target to launch";
                launchCommand.Add(targetsArgument);
                var gameBinOption = new Option<string>("--gameBin", "Path to the swkotor binary file");
                launchCommand.Options.Add(gameBinOption);
                var installDirOption = new Option<string>("--installDir", "The location of the KOTOR user directory");
                launchCommand.Options.Add(installDirOption);
                
                launchCommand.SetAction(parseResult =>
                {
                    var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                    var gameBin = parseResult.GetValue(gameBinOption);
                    var installDir = parseResult.GetValue(installDirOption);
                    
                    var logger = new StandardLogger();
                    var exitCode = Execute(targets, gameBin, installDir, logger);
                    Environment.Exit(exitCode);
                });

                rootCommand.Add(launchCommand);
            }
        }

        private static int Execute(string[] targetNames, string gameBin, string installDir, ILogger logger)
        {
            logger.Info("Launch command not yet fully implemented");
            logger.Info("Launch command should:");
            logger.Info("  1. Call install command");
            logger.Info("  2. Launch KOTOR game executable (swkotor.exe or swkotor2.exe)");
            logger.Info("  3. Pass appropriate command-line arguments to load the module");
            
            // TODO: Implement full launch logic
            // This requires:
            // - Calling install command logic
            // - Finding game executable (swkotor.exe or swkotor2.exe)
            // - Launching game with module parameter
            
            return 0;
        }
    }
}
