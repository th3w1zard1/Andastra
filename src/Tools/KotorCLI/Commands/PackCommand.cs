using System;
using System.CommandLine;
using System.IO;
using KotorCLI.Configuration;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Pack command - Convert, compile, and pack all sources for target.
    /// </summary>
    public static class PackCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var packCommand = new Command("pack", "Convert, compile, and pack all sources for target");
            var targetsArgument = new Argument<string[]>("targets", () => Array.Empty<string>(), "Targets to pack (use 'all' for all targets)");
            packCommand.Add(targetsArgument);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before packing");
            packCommand.Options.Add(cleanOption);
            var noConvertOption = new Option<bool>("--noConvert", "Do not convert updated json files");
            packCommand.Options.Add(noConvertOption);
            var noCompileOption = new Option<bool>("--noCompile", "Do not recompile updated scripts");
            packCommand.Options.Add(noCompileOption);
            
            packCommand.SetAction(parseResult =>
            {
                var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                var clean = parseResult.GetValue(cleanOption);
                var noConvert = parseResult.GetValue(noConvertOption);
                var noCompile = parseResult.GetValue(noCompileOption);
                
                var logger = new StandardLogger();
                var exitCode = Execute(targets, clean, noConvert, noCompile, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(packCommand);
        }

        private static int Execute(string[] targetNames, bool clean, bool noConvert, bool noCompile, ILogger logger)
        {
            // Pack command orchestrates: convert -> compile -> pack
            // TODO: STUB - For now, this is a placeholder that calls the individual commands
            
            logger.Info("Pack command not yet fully implemented");
            logger.Info("Pack command should:");
            logger.Info("  1. Call convert command (unless --noConvert)");
            logger.Info("  2. Call compile command (unless --noCompile)");
            logger.Info("  3. Pack all converted/compiled files into MOD/ERF/HAK");
            
            // TODO: Implement full pack logic
            // This requires:
            // - Calling convert command logic
            // - Calling compile command logic
            // - Reading all files from cache directory
            // - Packing them into ERF/MOD format
            // - Writing output file
            
            return 0;
        }
    }
}
