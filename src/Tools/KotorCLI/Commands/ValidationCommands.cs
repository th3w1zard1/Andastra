using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Validation and investigation commands.
    /// </summary>
    public static class ValidationCommands
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var checkTxiCmd = new Command("check-txi", "Check if TXI files exist for specific textures");
            var checkTxiInstall = new Option<string>(new[] { "-i", "--installation" }, "Path to KOTOR installation") { IsRequired = true };
            checkTxiCmd.AddOption(checkTxiInstall);
            checkTxiCmd.SetHandler((string install) => { var logger = new StandardLogger(); logger.Info("check-txi not yet implemented"); }, checkTxiInstall);
            rootCommand.AddCommand(checkTxiCmd);

            var check2DaCmd = new Command("check-2da", "Check if a 2DA file exists in installation");
            var check2DaName = new Option<string>("--2da", "2DA file name (without extension)") { IsRequired = true };
            check2DaCmd.AddOption(check2DaName);
            var check2DaInstall = new Option<string>(new[] { "-i", "--installation" }, "Path to KOTOR installation") { IsRequired = true };
            check2DaCmd.AddOption(check2DaInstall);
            check2DaCmd.SetHandler((string name, string install) => { var logger = new StandardLogger(); logger.Info("check-2da not yet implemented"); }, check2DaName, check2DaInstall);
            rootCommand.AddCommand(check2DaCmd);
        }
    }
}

