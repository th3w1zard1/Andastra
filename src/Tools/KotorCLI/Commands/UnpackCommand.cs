using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class UnpackCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var unpackCommand = new Command("unpack", "Unpack a file into the project source tree");
            var targetArgument = new Argument<string>("target", () => null, "Target to unpack");
            unpackCommand.AddArgument(targetArgument);
            var fileArgument = new Argument<string>("file", () => null, "File to unpack");
            unpackCommand.AddArgument(fileArgument);
            var fileOption = new Option<string>("--file", "File or directory to unpack into the target's source tree");
            unpackCommand.AddOption(fileOption);
            var removeDeletedOption = new Option<bool>("--removeDeleted", "Remove source files not present in the file being unpacked");
            unpackCommand.AddOption(removeDeletedOption);
            unpackCommand.SetHandler((string target, string file, string fileOpt, bool removeDeleted) =>
            {
                var logger = new StandardLogger();
                // TODO: Implement
                logger.Info("Unpack command not yet implemented");
            }, targetArgument, fileArgument, fileOption, removeDeletedOption);
            rootCommand.AddCommand(unpackCommand);
        }
    }
}

