using System;
using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Search-archive command - Search for resources in archive files.
    /// </summary>
    public static class SearchArchiveCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var searchArchiveCommand = new Command("search-archive", "Search for resources in archive files");
            var fileOption = new Option<string>("--file", "Archive file to search");
            fileOption.Required = true;
            searchArchiveCommand.Options.Add(fileOption);
            var patternArgument = new Argument<string>("pattern");
            patternArgument.Description = "Search pattern (supports wildcards)";
            searchArchiveCommand.Add(patternArgument);
            var caseSensitiveOption = new Option<bool>("--case-sensitive", "Case-sensitive search");
            searchArchiveCommand.Options.Add(caseSensitiveOption);
            var searchContentOption = new Option<bool>("--content", "Search in resource content (not just names)");
            searchArchiveCommand.Options.Add(searchContentOption);

            searchArchiveCommand.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(fileOption);
                var pattern = parseResult.GetValue(patternArgument);
                var caseSensitive = parseResult.GetValue(caseSensitiveOption);
                var searchContent = parseResult.GetValue(searchContentOption);

                var logger = new StandardLogger();
                var exitCode = Execute(file, pattern, caseSensitive, searchContent, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(searchArchiveCommand);
        }

        private static int Execute(string file, string pattern, bool caseSensitive, bool searchContent, ILogger logger)
        {
            logger.Info("Search-archive command not yet fully implemented");
            logger.Info($"Would search: {file}");
            logger.Info($"Pattern: {pattern}");

            // TODO: Implement search-archive logic
            // This requires:
            // - Detecting archive type (ERF, RIM, KEY/BIF)
            // - Reading archive
            // - Searching resource names (and optionally content) for pattern
            // - Returning matching resources

            return 0;
        }
    }
}
