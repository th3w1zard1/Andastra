using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    public static class CatCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            // Cat command is already added in UtilityCommands, but we keep this for consistency
            // It's a no-op here since UtilityCommands handles it
        }
    }
}

