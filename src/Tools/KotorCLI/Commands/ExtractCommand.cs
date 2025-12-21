using System;
using System.CommandLine;
using System.IO;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Extract command - Extract resources from archive files (KEY/BIF, RIM, ERF, etc.).
    /// </summary>
    public static class ExtractCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var extractCommand = new Command("extract", "Extract resources from archive files (KEY/BIF, RIM, ERF, etc.)");
            var fileOption = new Option<string>("--file", "Archive file to extract");
            fileOption.IsRequired = true;
            extractCommand.Options.Add(fileOption);
            var outputOption = new Option<string>(new[] { "-o", "--output" }, "Output directory (default: archive_name)");
            extractCommand.Options.Add(outputOption);
            var filterOption = new Option<string>("--filter", "Filter resources by name pattern (supports wildcards)");
            extractCommand.Options.Add(filterOption);
            
            extractCommand.SetAction(parseResult =>
            {
                var file = parseResult.GetValue(fileOption);
                var output = parseResult.GetValue(outputOption);
                var filter = parseResult.GetValue(filterOption);
                
                var logger = new StandardLogger();
                var exitCode = Execute(file, output, filter, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(extractCommand);
        }

        private static int Execute(string file, string output, string filter, ILogger logger)
        {
            logger.Info("Extract command not yet fully implemented");
            logger.Info($"Would extract: {file}");
            if (!string.IsNullOrEmpty(output))
            {
                logger.Info($"To: {output}");
            }
            
            // TODO: Implement extract logic
            // This requires:
            // - Detecting archive type (ERF, RIM, KEY/BIF)
            // - Reading archive
            // - Extracting resources to output directory
            // - Applying filter if specified
            
            return 0;
        }
    }
}
