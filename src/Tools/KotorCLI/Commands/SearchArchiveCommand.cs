using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class SearchArchiveCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var searchArchiveCommand = new Command("search-archive", "Search for resources in archive files");
            searchArchiveCommand.AddAlias("grep-archive");
            var fileOption = new Option<string>(new[] { "-f", "--file" }, "Archive file to search") { IsRequired = true };
            searchArchiveCommand.AddOption(fileOption);
            var patternArgument = new Argument<string>("pattern", "Search pattern (supports wildcards)");
            searchArchiveCommand.AddArgument(patternArgument);
            searchArchiveCommand.SetHandler((string file, string pattern) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Search-archive command not yet implemented");
            }, fileOption, patternArgument);
            rootCommand.AddCommand(searchArchiveCommand);
        }
    }
}

