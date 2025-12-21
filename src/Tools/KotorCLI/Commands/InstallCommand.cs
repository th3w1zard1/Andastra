using System;
using System.CommandLine;
using System.IO;
using KotorCLI.Configuration;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Install command - Convert, compile, pack, and install target.
    /// </summary>
    public static class InstallCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var installCommand = new Command("install", "Convert, compile, pack, and install target");
            var targetsArgument = new Argument<string[]>("targets", () => Array.Empty<string>(), "Targets to install (use 'all' for all targets)");
            installCommand.Add(targetsArgument);
            var installDirOption = new Option<string>("--installDir", "The location of the KOTOR user directory");
            installCommand.Options.Add(installDirOption);
            var noPackOption = new Option<bool>("--noPack", "Do not re-pack the file (implies --noConvert and --noCompile)");
            installCommand.Options.Add(noPackOption);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before packing");
            installCommand.Options.Add(cleanOption);
            
            installCommand.SetAction(parseResult =>
            {
                var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                var installDir = parseResult.GetValue(installDirOption);
                var noPack = parseResult.GetValue(noPackOption);
                var clean = parseResult.GetValue(cleanOption);
                
                var logger = new StandardLogger();
                var exitCode = Execute(targets, installDir, noPack, clean, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(installCommand);
        }

        private static int Execute(string[] targetNames, string installDir, bool noPack, bool clean, ILogger logger)
        {
            logger.Info("Install command not yet fully implemented");
            logger.Info("Install command should:");
            logger.Info("  1. Call pack command (unless --noPack)");
            logger.Info("  2. Copy packed MOD/ERF/HAK file to KOTOR installation directory");
            logger.Info("  3. Handle installation directory detection (default: user's Documents/KOTOR or similar)");
            
            // TODO: Implement full install logic
            // This requires:
            // - Calling pack command logic
            // - Determining KOTOR installation/user directory
            // - Copying packed file to modules/ directory (for MOD) or appropriate location
            
            return 0;
        }
    }
}
