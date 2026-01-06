using System;
using System.CommandLine;
using System.IO;
using KotorCLI.Logging;
using Andastra.Parsing.Tools;

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
            var directoryOption = new Option<string>("--directory", "Directory containing BIF files");
            directoryOption.Required = true;
            keyPackCommand.Options.Add(directoryOption);
            var outputOption = new Option<string>("--output", "Output KEY file");
            outputOption.Required = true;
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

        // Matching PyKotor implementation at Tools/KotorCLI/src/kotorcli/commands/key_pack.py:14-51
        // Original: def cmd_key_pack(args: Namespace, logger: Logger) -> int:
        private static int Execute(string directory, string output, string bifDir, string filter, ILogger logger)
        {
            // Matching PyKotor: input_dir = pathlib.Path(args.directory)
            // Matching PyKotor: bif_dir = pathlib.Path(args.bif_dir) if hasattr(args, "bif_dir") and args.bif_dir else input_dir
            string inputDir = directory;
            string bifDirPath = string.IsNullOrEmpty(bifDir) ? inputDir : bifDir;
            string outputPath = output;

            // Matching PyKotor: if not input_dir.exists():
            if (!Directory.Exists(inputDir))
            {
                logger.Error($"Input directory not found: {inputDir}");
                return 1;
            }

            // Matching PyKotor: if not input_dir.is_dir():
            var inputDirInfo = new DirectoryInfo(inputDir);
            if (!inputDirInfo.Exists || (inputDirInfo.Attributes & FileAttributes.Directory) == 0)
            {
                logger.Error($"Input path is not a directory: {inputDir}");
                return 1;
            }

            try
            {
                // Matching PyKotor: create_key_from_directory(input_dir, bif_dir, output_path, file_filter=args.filter if hasattr(args, "filter") else None)
                ArchiveHelpers.CreateKeyFromDirectory(
                    inputDir,
                    bifDirPath,
                    outputPath,
                    filter);

                // Matching PyKotor: logger.info(f"Created KEY file: {output_path}")
                logger.Info($"Created KEY file: {outputPath}");
            }
            catch (Exception ex)
            {
                // Matching PyKotor: logger.exception("Failed to create KEY file")
                logger.Error($"Failed to create KEY file: {ex.Message}");
                if (ex.InnerException != null)
                {
                    logger.Error($"Inner exception: {ex.InnerException.Message}");
                }
                return 1;
            }

            return 0;
        }
    }
}
