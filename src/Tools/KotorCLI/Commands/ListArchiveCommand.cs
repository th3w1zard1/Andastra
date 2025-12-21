using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class ListArchiveCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var listArchiveCommand = new Command("list-archive", "List contents of archive files (KEY/BIF, RIM, ERF, etc.)");
            listArchiveCommand.AddAlias("ls-archive");
            var fileOption = new Option<string>(new[] { "--file" }, "Archive file to list") { IsRequired = true };
            listArchiveCommand.AddOption(fileOption);
            listArchiveCommand.SetHandler((string file) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("List-archive command not yet implemented");
            }, fileOption);
            rootCommand.AddCommand(listArchiveCommand);
        }
    }
}

