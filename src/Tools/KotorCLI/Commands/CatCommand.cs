using System;
using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Cat command - Display resource contents to stdout.
    /// </summary>
    public static class CatCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var catCommand = new Command("cat", "Display resource contents to stdout");
            var archiveArgument = new Argument<string>("archive");
            archiveArgument.Description = "Archive file (ERF, RIM)";
            catCommand.Add(archiveArgument);
            var resourceArgument = new Argument<string>("resource");
            resourceArgument.Description = "Resource reference name";
            catCommand.Add(resourceArgument);
            var typeOption = new Option<string>("--type", "Resource type extension (optional, will try to detect)");
            catCommand.Options.Add(typeOption);
            
            catCommand.SetAction(parseResult =>
            {
                var archive = parseResult.GetValue(archiveArgument);
                var resource = parseResult.GetValue(resourceArgument);
                var type = parseResult.GetValue(typeOption);
                
                var logger = new StandardLogger();
                var exitCode = Execute(archive, resource, type, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(catCommand);
        }

        private static int Execute(string archive, string resource, string type, ILogger logger)
        {
            logger.Info("Cat command not yet fully implemented");
            logger.Info($"Would display resource: {resource} from {archive}");
            
            // TODO: Implement cat logic
            // This requires:
            // - Detecting archive type (ERF, RIM)
            // - Reading archive
            // - Finding resource by name (and type if specified)
            // - Outputting resource content to stdout
            
            return 0;
        }
    }
}
