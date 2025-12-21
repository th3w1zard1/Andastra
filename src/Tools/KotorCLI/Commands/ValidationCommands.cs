using System;
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
            var checkTxiInstall = new Option<string>(new[] { "-i", "--installation" }, "Path to KOTOR installation");
            checkTxiInstall.IsRequired = true;
            checkTxiCmd.Options.Add(checkTxiInstall);
            var texturesOption = new Option<string[]>(new[] { "-t", "--textures" }, "Texture names to check (without extension)");
            texturesOption.IsRequired = true;
            checkTxiCmd.Options.Add(texturesOption);
            checkTxiCmd.SetAction(parseResult =>
            {
                var install = parseResult.GetValue(checkTxiInstall);
                var textures = parseResult.GetValue(texturesOption);
                var logger = new StandardLogger();
                logger.Info("check-txi not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(checkTxiCmd);

            var check2DaCmd = new Command("check-2da", "Check if a 2DA file exists in installation");
            var check2DaName = new Option<string>("--2da", "2DA file name (without extension)");
            check2DaName.IsRequired = true;
            check2DaCmd.Options.Add(check2DaName);
            var check2DaInstall = new Option<string>(new[] { "-i", "--installation" }, "Path to KOTOR installation");
            check2DaInstall.IsRequired = true;
            check2DaCmd.Options.Add(check2DaInstall);
            check2DaCmd.SetAction(parseResult =>
            {
                var name = parseResult.GetValue(check2DaName);
                var install = parseResult.GetValue(check2DaInstall);
                var logger = new StandardLogger();
                logger.Info("check-2da not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(check2DaCmd);
        }
    }
}
