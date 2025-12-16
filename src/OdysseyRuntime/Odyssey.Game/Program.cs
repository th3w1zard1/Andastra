using System;
using Odyssey.Game.Core;
using Odyssey.Graphics;

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
    /// - FUN_00404250 @ 0x00404250: Creates mutex "swkotor2" via CreateMutexA, initializes COM via CoInitialize, loads config.txt (FUN_00460ff0), loads swKotor2.ini (FUN_00630a90), creates engine objects, runs game loop
    /// - Mutex creation: CreateMutexA with name "swkotor2" prevents multiple instances, WaitForSingleObject checks if already running
    /// - Config loading: FUN_00460ff0 @ 0x00460ff0 loads and executes text files (config.txt, startup.txt)
    /// - INI loading: FUN_00630a90 @ 0x00630a90 loads INI file values, FUN_00631ea0 @ 0x00631ea0 parses INI sections, FUN_00630c20 cleans up INI structures
    /// - Sound initialization: Checks "Disable Sound" setting from INI, sets DAT_008b73c0 flag
    /// - Window creation: FUN_00403f70 creates main window, FUN_004015b0/FUN_00401610 initialize graphics
    /// - Game loop: PeekMessageA/GetMessageA for Windows message processing, TranslateMessage/DispatchMessageA for input
    /// - Game initialization: Detects KOTOR installation path, loads configuration, creates game instance
    /// - Command line: DAT_008ba024 = GetCommandLineA() stores command-line arguments
    /// - Exit: Returns 0 on success, 0xffffffff if mutex already exists, 1 on error
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

                // Determine graphics backend (default to MonoGame, can be overridden via command line)
                GraphicsBackendType backendType = GraphicsBackendType.MonoGame;
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--backend" && i + 1 < args.Length)
                    {
                        if (args[i + 1].Equals("stride", StringComparison.OrdinalIgnoreCase))
                        {
                            backendType = GraphicsBackendType.Stride;
                        }
                        else if (args[i + 1].Equals("monogame", StringComparison.OrdinalIgnoreCase))
                        {
                            backendType = GraphicsBackendType.MonoGame;
                        }
                        break;
                    }
                }

                Console.WriteLine("Graphics Backend: " + backendType);
                Console.WriteLine();

                // Create graphics backend
                IGraphicsBackend graphicsBackend = Core.GraphicsBackendFactory.CreateBackend(backendType);

                // Create and run the game using abstraction layer
                using (var game = new OdysseyGame(settings, graphicsBackend))
                {
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

