using System;

namespace Odyssey.Game.Core
{
    /// <summary>
    /// Which KOTOR game to run.
    /// </summary>
    public enum KotorGame
    {
        K1,
        K2
    }

    /// <summary>
    /// Game settings and configuration.
    /// </summary>
    public class GameSettings
    {
        /// <summary>
        /// Which game (K1 or K2).
        /// </summary>
        public KotorGame Game { get; set; } = KotorGame.K1;

        /// <summary>
        /// Path to the KOTOR installation.
        /// </summary>
        public string GamePath { get; set; }

        /// <summary>
        /// Starting module override (null = use default starting module).
        /// </summary>
        public string StartModule { get; set; }

        /// <summary>
        /// Save game to load (null = new game).
        /// </summary>
        public string LoadSave { get; set; }

        /// <summary>
        /// Window width.
        /// </summary>
        public int Width { get; set; } = 1280;

        /// <summary>
        /// Window height.
        /// </summary>
        public int Height { get; set; } = 720;

        /// <summary>
        /// Fullscreen mode.
        /// </summary>
        public bool Fullscreen { get; set; } = false;

        /// <summary>
        /// Enable debug rendering.
        /// </summary>
        public bool DebugRender { get; set; } = false;

        /// <summary>
        /// Skip intro videos.
        /// </summary>
        public bool SkipIntro { get; set; } = true;

        /// <summary>
        /// Parse command line arguments into settings.
        /// </summary>
        public static GameSettings FromCommandLine(string[] args)
        {
            var settings = new GameSettings();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();

                switch (arg)
                {
                    case "--k1":
                    case "-k1":
                        settings.Game = KotorGame.K1;
                        break;

                    case "--k2":
                    case "-k2":
                    case "--tsl":
                        settings.Game = KotorGame.K2;
                        break;

                    case "--path":
                    case "-p":
                        if (i + 1 < args.Length)
                        {
                            settings.GamePath = args[++i];
                        }
                        break;

                    case "--module":
                    case "-m":
                        if (i + 1 < args.Length)
                        {
                            settings.StartModule = args[++i];
                        }
                        break;

                    case "--load":
                    case "-l":
                        if (i + 1 < args.Length)
                        {
                            settings.LoadSave = args[++i];
                        }
                        break;

                    case "--width":
                    case "-w":
                        if (i + 1 < args.Length)
                        {
                            int.TryParse(args[++i], out int width);
                            if (width > 0) settings.Width = width;
                        }
                        break;

                    case "--height":
                    case "-h":
                        if (i + 1 < args.Length)
                        {
                            int.TryParse(args[++i], out int height);
                            if (height > 0) settings.Height = height;
                        }
                        break;

                    case "--fullscreen":
                    case "-f":
                        settings.Fullscreen = true;
                        break;

                    case "--debug":
                    case "-d":
                        settings.DebugRender = true;
                        break;

                    case "--no-intro":
                        settings.SkipIntro = true;
                        break;

                    case "--help":
                    case "-?":
                        PrintHelp();
                        Environment.Exit(0);
                        break;
                }
            }

            return settings;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Odyssey Engine - KOTOR Recreation");
            Console.WriteLine();
            Console.WriteLine("Usage: Odyssey.Game [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --k1, -k1           Run KOTOR 1 (default)");
            Console.WriteLine("  --k2, -k2, --tsl    Run KOTOR 2 (TSL)");
            Console.WriteLine("  --path, -p <path>   Path to KOTOR installation");
            Console.WriteLine("  --module, -m <name> Start at specific module");
            Console.WriteLine("  --load, -l <save>   Load save game");
            Console.WriteLine("  --width, -w <n>     Window width (default: 1280)");
            Console.WriteLine("  --height, -h <n>    Window height (default: 720)");
            Console.WriteLine("  --fullscreen, -f    Run in fullscreen");
            Console.WriteLine("  --debug, -d         Enable debug rendering");
            Console.WriteLine("  --no-intro          Skip intro videos");
            Console.WriteLine("  --help, -?          Show this help");
        }
    }
}

