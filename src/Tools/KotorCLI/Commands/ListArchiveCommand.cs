using System;
using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// List-archive command - List contents of archive files (KEY/BIF, RIM, ERF, etc.).
    /// </summary>
    public static class ListArchiveCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var listArchiveCommand = new Command("list-archive", "List contents of archive files (KEY/BIF, RIM, ERF, etc.)");
            var fileOption = new Option<string>("--file", "Archive file to list");
            fileOption.Required = true;
            listArchiveCommand.Options.Add(fileOption);
            var verboseOption = new Option<bool>("--verbose", "Show detailed resource information");
            listArchiveCommand.Options.Add(verboseOption);
            var filterOption = new Option<string>("--filter", "Filter resources by name pattern (supports wildcards)");
            listArchiveCommand.Options.Add(filterOption);

            listArchiveCommand.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(fileOption);
                var verbose = parseResult.GetValue(verboseOption);
                var filter = parseResult.GetValue(filterOption);

                var logger = new StandardLogger();
                var exitCode = Execute(file, verbose, filter, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(listArchiveCommand);
        }

        private static int Execute(string file, bool verbose, string filter, ILogger logger)
        {
            logger.Info("List-archive command not yet fully implemented");
            logger.Info($"Would list: {file}");

            // TODO: Implement list-archive logic
            // This requires:
            // - Detecting archive type (ERF, RIM, KEY/BIF)
            // - Reading archive
            // - Listing resources (with details if verbose)
            // - Applying filter if specified

            return 0;
        }
    }
}
