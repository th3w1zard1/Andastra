using System;
using System.CommandLine;
using System.IO;
using KotorCLI.Logging;
using Andastra.Parsing.Formats.WAV;

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
            var textureInput = new Argument<string>("input");
            textureInput.Description = "Input texture file (TPC or TGA)";
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
            var soundInput = new Argument<string>("input");
            soundInput.Description = "Input WAV file";
            soundCmd.Add(soundInput);
            var soundOutput = new Option<string>(new[] { "-o", "--output" }, "Output WAV file");
            soundCmd.Options.Add(soundOutput);
            var forceOverwrite = new Option<bool>(new[] { "-f", "--force" }, "Force overwrite output file if it exists");
            soundCmd.Options.Add(forceOverwrite);
            soundCmd.SetAction(parseResult =>
            {
                var input = parseResult.GetValue(soundInput);
                var output = parseResult.GetValue(soundOutput);
                var force = parseResult.GetValue(forceOverwrite);
                var logger = new StandardLogger();

                try
                {
                    // Validate input file
                    if (string.IsNullOrEmpty(input))
                    {
                        logger.Error("Input file path is required");
                        Environment.Exit(1);
                    }

                    if (!File.Exists(input))
                    {
                        logger.Error($"Input file does not exist: {input}");
                        Environment.Exit(1);
                    }

                    // Determine output file path
                    string outputPath = output;
                    if (string.IsNullOrEmpty(outputPath))
                    {
                        // Generate output filename by adding "_clean" suffix before extension
                        string inputDir = Path.GetDirectoryName(input) ?? "";
                        string inputName = Path.GetFileNameWithoutExtension(input);
                        string inputExt = Path.GetExtension(input);
                        outputPath = Path.Combine(inputDir, $"{inputName}_clean{inputExt}");
                    }

                    // Check if output file exists
                    if (File.Exists(outputPath) && !force)
                    {
                        logger.Error($"Output file already exists: {outputPath}. Use --force to overwrite.");
                        Environment.Exit(1);
                    }

                    // Create output directory if it doesn't exist
                    string outputDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                    {
                        Directory.CreateDirectory(outputDir);
                    }

                    logger.Info($"Converting sound file: {input} -> {outputPath}");

                    // Read input WAV file
                    byte[] inputData = File.ReadAllBytes(input);
                    logger.Info($"Read {inputData.Length} bytes from input file");

                    // Detect audio format and deobfuscate
                    var deobfuscationResult = WAVObfuscation.GetDeobfuscationResult(inputData);
                    byte[] deobfuscatedData = deobfuscationResult.Item1;
                    DeobfuscationResult formatType = deobfuscationResult.Item2;

                    logger.Info($"Detected format: {formatType}");

                    // Parse WAV file
                    WAV wavFile;
                    try
                    {
                        wavFile = WAVAuto.ReadWav(deobfuscatedData);
                        logger.Info($"Parsed WAV: {wavFile.Channels} channels, {wavFile.SampleRate}Hz, {wavFile.BitsPerSample} bits, {wavFile.Data.Length} bytes of audio data");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to parse WAV file: {ex.Message}");
                        Environment.Exit(1);
                        return;
                    }

                    // Get clean/playable bytes (standard RIFF/WAVE format)
                    byte[] cleanWavData;
                    try
                    {
                        cleanWavData = WAVAuto.GetPlayableBytes(wavFile);
                        logger.Info($"Generated clean WAV data: {cleanWavData.Length} bytes");
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to generate clean WAV data: {ex.Message}");
                        Environment.Exit(1);
                        return;
                    }

                    // Validate the clean WAV data has proper RIFF header
                    if (cleanWavData.Length < 12 ||
                        cleanWavData[0] != 0x52 || cleanWavData[1] != 0x49 ||
                        cleanWavData[2] != 0x46 || cleanWavData[3] != 0x46) // "RIFF"
                    {
                        logger.Error("Generated clean WAV data does not have valid RIFF header");
                        Environment.Exit(1);
                        return;
                    }

                    // Write output file
                    File.WriteAllBytes(outputPath, cleanWavData);
                    logger.Info($"Successfully converted sound file to: {outputPath}");

                    // Log format details
                    string formatDescription = formatType switch
                    {
                        DeobfuscationResult.Standard => "Standard RIFF/WAVE format (no header removed)",
                        DeobfuscationResult.SFX_Header => "KOTOR SFX format (470-byte header removed)",
                        DeobfuscationResult.MP3_In_WAV => "MP3-in-WAV format (58-byte header removed)",
                        _ => "Unknown format"
                    };
                    logger.Info($"Input format: {formatDescription}");

                    // Log audio details
                    string audioType = wavFile.AudioFormat == AudioFormat.MP3 ? "MP3" : "PCM";
                    logger.Info($"Audio type: {audioType}, Channels: {wavFile.Channels}, Sample Rate: {wavFile.SampleRate}Hz, Bits: {wavFile.BitsPerSample}");

                }
                catch (Exception ex)
                {
                    logger.Error($"Sound conversion failed: {ex.Message}");
                    logger.Error($"Stack trace: {ex.StackTrace}");
                    Environment.Exit(1);
                }
            });
            rootCommand.Add(soundCmd);

            var modelCmd = new Command("model-convert", "Convert model files (MDL↔ASCII)");
            var modelInput = new Argument<string>("input");
            modelInput.Description = "Input MDL file";
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
