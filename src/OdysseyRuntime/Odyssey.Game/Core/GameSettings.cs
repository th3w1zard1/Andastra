using System;
using Odyssey.Core;

namespace Odyssey.Game.Core
{
    /// <summary>
    /// Game settings and configuration with command-line parsing.
    /// </summary>
    public static class GameSettingsExtensions
    {
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
