using System;
using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Key-pack command - Create KEY file from directory containing BIF files.
    /// </summary>
    public static class KeyPackCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var keyPackCommand = new Command("key-pack", "Create KEY file from directory containing BIF files");
            keyPackCommand.AddAlias("create-key");
            var directoryOption = new Option<string>(new[] { "-d", "--directory" }, "Directory containing BIF files");
            directoryOption.IsRequired = true;
            keyPackCommand.Options.Add(directoryOption);
            var outputOption = new Option<string>(new[] { "-o", "--output" }, "Output KEY file");
            outputOption.IsRequired = true;
            keyPackCommand.Options.Add(outputOption);
            var bifDirOption = new Option<string>("--bif-dir", "Directory where BIF files are located (for relative paths in KEY)");
            keyPackCommand.Options.Add(bifDirOption);
            var filterOption = new Option<string>("--filter", "Filter BIF files by pattern (supports wildcards)");
            keyPackCommand.Options.Add(filterOption);
            
            keyPackCommand.SetAction(parseResult =>
            {
                var directory = parseResult.GetValue(directoryOption);
                var output = parseResult.GetValue(outputOption);
                var bifDir = parseResult.GetValue(bifDirOption);
                var filter = parseResult.GetValue(filterOption);
                
                var logger = new StandardLogger();
                var exitCode = Execute(directory, output, bifDir, filter, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(keyPackCommand);
        }

        private static int Execute(string directory, string output, string bifDir, string filter, ILogger logger)
        {
            logger.Info("key-pack command not yet fully implemented");
            logger.Info($"Would pack KEY from: {directory}");
            logger.Info($"To: {output}");
            
            // TODO: Implement key-pack logic
            // This requires:
            // - Finding all BIF files in directory
            // - Applying filter if specified
            // - Creating KEY file with BIF file references
            // - Writing output KEY file
            
            return 0;
        }
    }
}
