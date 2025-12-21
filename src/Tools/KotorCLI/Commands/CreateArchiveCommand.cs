using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class CreateArchiveCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var createArchiveCommand = new Command("create-archive", "Create archive (ERF, RIM) from directory");
            createArchiveCommand.AddAlias("pack-archive");
            var directoryOption = new Option<string>(new[] { "-d", "--directory" }, "Directory to pack") { IsRequired = true };
            createArchiveCommand.AddOption(directoryOption);
            var outputOption = new Option<string>(new[] { "-o", "--output" }, "Output archive file") { IsRequired = true };
            createArchiveCommand.AddOption(outputOption);
            createArchiveCommand.SetHandler((string directory, string output) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Create-archive command not yet implemented");
            }, directoryOption, outputOption);
            rootCommand.AddCommand(createArchiveCommand);
        }
    }
}

