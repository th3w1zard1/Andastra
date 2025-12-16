using System;
using Odyssey.Game.Core;

namespace Odyssey.Game
{
    /// <summary>
    /// Entry point for the Odyssey Engine game launcher.
    /// </summary>
    /// <remarks>
    /// Program Entry Point:
    /// - Based on swkotor2.exe: entry @ 0x0076e2dd (PE entry point)
    /// - Main initialization: FUN_00404250 @ 0x00404250 (WinMain equivalent, initializes game)
    /// - Located via string references: "swkotor2" @ 0x007b575c (executable name), "KotOR2" @ 0x0080c210 (game title)
    /// - Original implementation: Entry point calls GetVersionExA, initializes heap, calls FUN_00404250
    /// - FUN_00404250: Creates mutex "swkotor2", loads config.txt (FUN_00460ff0), loads swKotor2.ini, initializes engine, runs game loop
    /// - Config loading: FUN_00460ff0 @ 0x00460ff0 loads and executes text files (config.txt, startup.txt)
    /// - INI loading: FUN_00630a90 @ 0x00630a90 loads INI file values, FUN_00631ea0 parses INI sections
    /// - Game initialization: Detects KOTOR installation path, loads configuration, creates game instance
    /// - Command line: DAT_008ba024 = GetCommandLineA() stores command-line arguments
    /// </remarks>
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("Odyssey Engine - KOTOR Recreation");
                Console.WriteLine("==================================");
                Console.WriteLine();

                // Parse command line arguments
                var settings = GameSettingsExtensions.FromCommandLine(args);

                // Detect KOTOR installation if not specified
                if (string.IsNullOrEmpty(settings.GamePath))
                {
                    settings.GamePath = GamePathDetector.DetectKotorPath(settings.Game);
                    if (string.IsNullOrEmpty(settings.GamePath))
                    {
                        Console.Error.WriteLine("ERROR: Could not detect KOTOR installation.");
                        Console.Error.WriteLine("Please specify the game path with --path <path>");
                        return 1;
                    }
                }

                Console.WriteLine("Game: " + settings.Game);
                Console.WriteLine("Path: " + settings.GamePath);
                Console.WriteLine();

                // Create and run the game (MonoGame version)
                using (var game = new OdysseyGame(settings))
                {
                    // MonoGame Game.Run() starts the game loop and blocks until the game exits
                    // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Game.html
                    // Method signature: void Run()
                    game.Run();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FATAL ERROR: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }
    }
}

