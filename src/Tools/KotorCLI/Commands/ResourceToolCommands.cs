using System;
using System.CommandLine;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Resource tool commands (texture-convert, sound-convert, model-convert).
    /// </summary>
    public static class ResourceToolCommands
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var textureCmd = new Command("texture-convert", "Convert texture files (TPC↔TGA)");
            var textureInput = new Argument<string>("input", "Input texture file (TPC or TGA)");
            textureCmd.Add(textureInput);
            var textureOutput = new Option<string>(new[] { "-o", "--output" }, "Output texture file");
            textureCmd.Options.Add(textureOutput);
            var txiOption = new Option<string>("--txi", "TXI file path (for TPC↔TGA conversion)");
            textureCmd.Options.Add(txiOption);
            textureCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(textureInput);
                var output = parseResult.GetValue(textureOutput);
                var txi = parseResult.GetValue(txiOption);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - texture-convert not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(textureCmd);

            var soundCmd = new Command("sound-convert", "Convert sound files (WAV↔clean WAV)");
            var soundInput = new Argument<string>("input", "Input WAV file");
            soundCmd.Add(soundInput);
            var soundOutput = new Option<string>(new[] { "-o", "--output" }, "Output WAV file");
            soundCmd.Options.Add(soundOutput);
            soundCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(soundInput);
                var output = parseResult.GetValue(soundOutput);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - sound-convert not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(soundCmd);

            var modelCmd = new Command("model-convert", "Convert model files (MDL↔ASCII)");
            var modelInput = new Argument<string>("input", "Input MDL file");
            modelCmd.Add(modelInput);
            var modelOutput = new Option<string>(new[] { "-o", "--output" }, "Output MDL file");
            modelCmd.Options.Add(modelOutput);
            var toAsciiOption = new Option<bool>("--to-ascii", "Convert to ASCII format");
            modelCmd.Options.Add(toAsciiOption);
            modelCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(modelInput);
                var output = parseResult.GetValue(modelOutput);
                var toAscii = parseResult.GetValue(toAsciiOption);
                var logger = new StandardLogger();
                logger.Info("TODO: STUB - model-convert not yet implemented");
                Environment.Exit(0);
            });
            rootCommand.Add(modelCmd);
        }
    }
}
