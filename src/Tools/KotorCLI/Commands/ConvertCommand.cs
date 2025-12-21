using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class ConvertCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var convertCommand = new Command("convert", "Convert all JSON sources to their GFF counterparts");
            var targetsArgument = new Argument<string[]>("targets", () => new string[0], "Targets to convert (use 'all' for all targets)");
            convertCommand.AddArgument(targetsArgument);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before converting");
            convertCommand.AddOption(cleanOption);
            convertCommand.SetHandler((string[] targets, bool clean) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Convert command not yet implemented");
            }, targetsArgument, cleanOption);
            rootCommand.AddCommand(convertCommand);
        }
    }
}

