using System;
using Odyssey.Game.Core;

namespace Odyssey.Game
{
    /// <summary>
    /// Entry point for the Odyssey Engine game launcher.
    /// </summary>
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
                var settings = GameSettings.FromCommandLine(args);

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

                // Create and run the game
                using (var game = new OdysseyGame(settings))
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

