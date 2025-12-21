using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class ExtractCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var extractCommand = new Command("extract", "Extract resources from archive files (KEY/BIF, RIM, ERF, etc.)");
            var fileOption = new Option<string>(new[] { "--file" }, "Archive file to extract") { IsRequired = true };
            extractCommand.AddOption(fileOption);
            var outputOption = new Option<string>(new[] { "-o", "--output" }, "Output directory (default: archive_name)");
            extractCommand.AddOption(outputOption);
            extractCommand.SetHandler((string file, string output) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Extract command not yet implemented");
            }, fileOption, outputOption);
            rootCommand.AddCommand(extractCommand);
        }
    }
}

