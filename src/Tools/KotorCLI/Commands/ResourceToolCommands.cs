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
            textureCmd.AddArgument(textureInput);
            var textureOutput = new Option<string>(new[] { "-o", "--output" }, "Output texture file");
            textureCmd.AddOption(textureOutput);
            textureCmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("texture-convert not yet implemented"); }, textureInput, textureOutput);
            rootCommand.AddCommand(textureCmd);

            var soundCmd = new Command("sound-convert", "Convert sound files (WAV↔clean WAV)");
            var soundInput = new Argument<string>("input", "Input WAV file");
            soundCmd.AddArgument(soundInput);
            var soundOutput = new Option<string>(new[] { "-o", "--output" }, "Output WAV file");
            soundCmd.AddOption(soundOutput);
            soundCmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("sound-convert not yet implemented"); }, soundInput, soundOutput);
            rootCommand.AddCommand(soundCmd);

            var modelCmd = new Command("model-convert", "Convert model files (MDL↔ASCII)");
            var modelInput = new Argument<string>("input", "Input MDL file");
            modelCmd.AddArgument(modelInput);
            var modelOutput = new Option<string>(new[] { "-o", "--output" }, "Output MDL file");
            modelCmd.AddOption(modelOutput);
            modelCmd.SetHandler((string input, string output) => { var logger = new StandardLogger(); logger.Info("model-convert not yet implemented"); }, modelInput, modelOutput);
            rootCommand.AddCommand(modelCmd);
        }
    }
}

