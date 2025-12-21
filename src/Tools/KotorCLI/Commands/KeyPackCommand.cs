using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class KeyPackCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var keyPackCommand = new Command("key-pack", "Create KEY file from directory containing BIF files");
            keyPackCommand.AddAlias("create-key");
            var directoryOption = new Option<string>(new[] { "-d", "--directory" }, "Directory containing BIF files") { IsRequired = true };
            keyPackCommand.AddOption(directoryOption);
            var outputOption = new Option<string>(new[] { "-o", "--output" }, "Output KEY file") { IsRequired = true };
            keyPackCommand.AddOption(outputOption);
            keyPackCommand.SetHandler((string directory, string output) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("key-pack command not yet implemented");
            }, directoryOption, outputOption);
            rootCommand.AddCommand(keyPackCommand);
        }
    }
}

