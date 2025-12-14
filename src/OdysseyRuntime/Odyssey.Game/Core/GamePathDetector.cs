using System;
using System.IO;
using Microsoft.Win32;

namespace Odyssey.Game.Core
{
    /// <summary>
    /// Detects KOTOR installation paths from common locations.
    /// </summary>
    public static class GamePathDetector
    {
        /// <summary>
        /// Detect KOTOR installation path.
        /// </summary>
        public static string DetectKotorPath(KotorGame game)
        {
            // Try registry first
            string registryPath = TryRegistry(game);
            if (!string.IsNullOrEmpty(registryPath))
            {
                return registryPath;
            }

            // Try common Steam paths
            string steamPath = TrySteamPaths(game);
            if (!string.IsNullOrEmpty(steamPath))
            {
                return steamPath;
            }

            // Try GOG paths
            string gogPath = TryGogPaths(game);
            if (!string.IsNullOrEmpty(gogPath))
            {
                return gogPath;
            }

            // Try common installation paths
            string commonPath = TryCommonPaths(game);
            if (!string.IsNullOrEmpty(commonPath))
            {
                return commonPath;
            }

            return null;
        }

        private static string TryRegistry(KotorGame game)
        {
            try
            {
                string[] registryKeys;
                if (game == KotorGame.K1)
                {
                    registryKeys = new[]
                    {
                        @"SOFTWARE\BioWare\SW\KOTOR",
                        @"SOFTWARE\LucasArts\KotOR",
                        @"SOFTWARE\Wow6432Node\BioWare\SW\KOTOR",
                        @"SOFTWARE\Wow6432Node\LucasArts\KotOR"
                    };
                }
                else
                {
                    registryKeys = new[]
                    {
                        @"SOFTWARE\Obsidian\KOTOR2",
                        @"SOFTWARE\LucasArts\KotOR2",
                        @"SOFTWARE\Wow6432Node\Obsidian\KOTOR2",
                        @"SOFTWARE\Wow6432Node\LucasArts\KotOR2"
                    };
                }

                foreach (string keyPath in registryKeys)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null)
                        {
                            string path = key.GetValue("Path") as string;
                            if (!string.IsNullOrEmpty(path) && IsValidInstallation(path, game))
                            {
                                return path;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Registry access may fail on non-Windows
            }

            return null;
        }

        private static string TrySteamPaths(KotorGame game)
        {
            string steamApps = null;

            // Try to find Steam installation
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        steamApps = Path.Combine(key.GetValue("InstallPath") as string ?? "", "steamapps", "common");
                    }
                }

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        steamApps = Path.Combine(key.GetValue("InstallPath") as string ?? "", "steamapps", "common");
                    }
                }
            }
            catch { }

            // Common Steam library locations
            string[] steamLibraries = new[]
            {
                steamApps,
                @"C:\Program Files (x86)\Steam\steamapps\common",
                @"C:\Program Files\Steam\steamapps\common",
                @"D:\Steam\steamapps\common",
                @"D:\SteamLibrary\steamapps\common",
                @"E:\Steam\steamapps\common",
                @"E:\SteamLibrary\steamapps\common"
            };

            string gameName = game == KotorGame.K1
                ? "swkotor"
                : "Knights of the Old Republic II";

            foreach (string library in steamLibraries)
            {
                if (string.IsNullOrEmpty(library)) continue;

                string path = Path.Combine(library, gameName);
                if (IsValidInstallation(path, game))
                {
                    return path;
                }
            }

            return null;
        }

        private static string TryGogPaths(KotorGame game)
        {
            string[] gogPaths;
            if (game == KotorGame.K1)
            {
                gogPaths = new[]
                {
                    @"C:\GOG Games\Star Wars - KotOR",
                    @"C:\Program Files (x86)\GOG Galaxy\Games\Star Wars - KotOR",
                    @"D:\GOG Games\Star Wars - KotOR"
                };
            }
            else
            {
                gogPaths = new[]
                {
                    @"C:\GOG Games\Star Wars - KotOR2",
                    @"C:\Program Files (x86)\GOG Galaxy\Games\Star Wars - KotOR2",
                    @"D:\GOG Games\Star Wars - KotOR2"
                };
            }

            foreach (string path in gogPaths)
            {
                if (IsValidInstallation(path, game))
                {
                    return path;
                }
            }

            return null;
        }

        private static string TryCommonPaths(KotorGame game)
        {
            string[] commonPaths;
            if (game == KotorGame.K1)
            {
                commonPaths = new[]
                {
                    @"C:\Program Files (x86)\LucasArts\SWKotOR",
                    @"C:\Program Files\LucasArts\SWKotOR",
                    @"C:\Games\KotOR",
                    @"D:\Games\KotOR"
                };
            }
            else
            {
                commonPaths = new[]
                {
                    @"C:\Program Files (x86)\LucasArts\SWKotOR2",
                    @"C:\Program Files (x86)\Obsidian\KotOR2",
                    @"C:\Program Files\LucasArts\SWKotOR2",
                    @"C:\Games\KotOR2",
                    @"D:\Games\KotOR2"
                };
            }

            foreach (string path in commonPaths)
            {
                if (IsValidInstallation(path, game))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Verify a path is a valid KOTOR installation.
        /// </summary>
        public static bool IsValidInstallation(string path, KotorGame game)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }

            // Check for chitin.key (required)
            if (!File.Exists(Path.Combine(path, "chitin.key")))
            {
                return false;
            }

            // Check for game executable
            string exeName = game == KotorGame.K1 ? "swkotor.exe" : "swkotor2.exe";
            if (!File.Exists(Path.Combine(path, exeName)))
            {
                return false;
            }

            return true;
        }
    }
}

