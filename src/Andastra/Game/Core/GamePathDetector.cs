using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BioWare.NET.Common;
using Andastra.Runtime.Core;
using Microsoft.Win32;
using GameType = BioWare.NET.Common.BioWareGame;

namespace Andastra.Game.Core
{
    /// <summary>
    /// Detects KOTOR installation paths from common locations.
    /// </summary>
    /// <remarks>
    /// Game Path Detection:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) installation path detection system
    /// - Located via string references: Original engine reads installation path from Windows registry
    /// - Registry access: Uses Windows Registry API (RegOpenKeyEx, RegQueryValueEx) for path lookup
    /// - Registry keys: K1 uses "SOFTWARE\BioWare\SW\KOTOR" or "SOFTWARE\LucasArts\KotOR"
    /// - Registry keys: K2 uses "SOFTWARE\Obsidian\KOTOR2" or "SOFTWARE\LucasArts\KotOR2"
    /// - Registry value: "Path" entry contains installation directory path (HKEY_LOCAL_MACHINE)
    /// - Wow6432Node: 64-bit Windows registry redirector for 32-bit applications (checks both locations)
    /// - Validation: Checks for chitin.key (keyfile) and game executable (swkotor.exe/swkotor2.exe)
    /// - chitin.key: Keyfile containing resource file mappings and encryption keys (required for resource loading)
    /// - Executable validation: Checks for swkotor.exe (K1) or swkotor2.exe (K2) in installation directory
    /// - Engine initialization: 0x00404250 @ 0x00404250 reads installation path during startup
    /// - Path resolution: 0x00633270 @ 0x00633270 resolves resource paths based on installation directory
    /// - This implementation: Enhanced with Steam, GOG, and environment variable detection
    /// - Note: Original engine primarily used registry lookup (HKEY_LOCAL_MACHINE), this adds modern distribution platform support
    /// - Steam detection: Checks Steam registry key "SOFTWARE\Valve\Steam" for InstallPath, then searches steamapps\common
    /// - GOG detection: Checks common GOG installation paths (C:\GOG Games, Program Files (x86)\GOG Galaxy\Games)
    /// </remarks>
    public static class GamePathDetector
    {
        /// <summary>
        /// Detect KOTOR installation path.
        /// Checks in order: environment variables (.env/K1_PATH), registry, Steam paths, GOG paths, common paths.
        /// </summary>
        public static string DetectKotorPath(KotorGame game)
        {
            // Try environment variable first (supports .env file via K1_PATH or K2_PATH)
            string envPath = TryEnvironmentVariable(game);
            if (!string.IsNullOrEmpty(envPath))
            {
                return envPath;
            }

            // Try registry
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

        /// <summary>
        /// Finds all KOTOR installation paths from default locations.
        /// Similar to FindKotorPathsFromDefault in HoloPatcher.UI/Core.cs.
        /// </summary>
        public static List<string> FindKotorPathsFromDefault(KotorGame game)
        {
            var paths = new List<string>();

            // Try environment variable first
            string envPath = TryEnvironmentVariable(game);
            if (!string.IsNullOrEmpty(envPath) && !paths.Contains(envPath))
            {
                paths.Add(envPath);
            }

            // Try registry paths
            string registryPath = TryRegistry(game);
            if (!string.IsNullOrEmpty(registryPath) && !paths.Contains(registryPath))
            {
                paths.Add(registryPath);
            }

            // Try Steam paths (check multiple library locations)
            string[] steamLibraries = new[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common",
                @"C:\Program Files\Steam\steamapps\common",
                @"D:\Steam\steamapps\common",
                @"D:\SteamLibrary\steamapps\common",
                @"E:\Steam\steamapps\common",
                @"E:\SteamLibrary\steamapps\common"
            };

            string gameName = game == KotorGame.K1 ? "swkotor" : "Knights of the Old Republic II";
            foreach (string library in steamLibraries)
            {
                if (string.IsNullOrEmpty(library)) continue;
                string path = Path.Combine(library, gameName);
                if (IsValidInstallation(path, game) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            // Try GOG paths
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
                if (IsValidInstallation(path, game) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            // Try common paths
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
                if (IsValidInstallation(path, game) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Tries to get the game path from environment variables.
        /// Supports K1_PATH for K1 and K2_PATH for K2.
        /// Also loads .env file from repository root if available.
        /// </summary>
        private static string TryEnvironmentVariable(KotorGame game)
        {
            // Load .env file if it exists in the repository root
            LoadEnvFile();

            // Get environment variable based on game
            string envVarName = game == KotorGame.K1 ? "K1_PATH" : "K2_PATH";
            string path = Environment.GetEnvironmentVariable(envVarName);

            if (!string.IsNullOrEmpty(path) && IsValidInstallation(path, game))
            {
                return path;
            }

            return null;
        }

        /// <summary>
        /// Loads .env file from repository root if it exists.
        /// Format: KEY=value (one per line, # for comments, blank lines ignored)
        /// Searches in: current working directory, executable directory, and walking up to find .git/.env
        /// </summary>
        private static void LoadEnvFile()
        {
            try
            {
                string envPath = null;

                // Try current working directory first (for development/testing)
                string workingDir = Environment.CurrentDirectory;
                string candidatePath = Path.Combine(workingDir, ".env");
                if (File.Exists(candidatePath))
                {
                    envPath = candidatePath;
                }

                // Try executable directory
                if (envPath == null)
                {
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    candidatePath = Path.Combine(exeDir, ".env");
                    if (File.Exists(candidatePath))
                    {
                        envPath = candidatePath;
                    }
                }

                // Walk up from executable directory to find .env (look for .git as indicator of repo root)
                if (envPath == null)
                {
                    DirectoryInfo dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                    for (int i = 0; i < 10 && dir != null; i++)
                    {
                        candidatePath = Path.Combine(dir.FullName, ".env");
                        if (File.Exists(candidatePath))
                        {
                            envPath = candidatePath;
                            break;
                        }

                        // Also check for .git to know we're at repo root
                        string gitPath = Path.Combine(dir.FullName, ".git");
                        if (Directory.Exists(gitPath) || File.Exists(gitPath))
                        {
                            // We're at repo root, if .env exists here, use it
                            if (File.Exists(candidatePath))
                            {
                                envPath = candidatePath;
                                break;
                            }
                        }

                        dir = dir.Parent;
                    }
                }

                // Also try workspace root environment variable (for CI/CD)
                if (envPath == null)
                {
                    string workspaceRoot = Environment.GetEnvironmentVariable("WORKSPACE_ROOT");
                    if (string.IsNullOrEmpty(workspaceRoot))
                    {
                        workspaceRoot = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
                    }
                    if (!string.IsNullOrEmpty(workspaceRoot))
                    {
                        candidatePath = Path.Combine(workspaceRoot, ".env");
                        if (File.Exists(candidatePath))
                        {
                            envPath = candidatePath;
                        }
                    }
                }

                if (envPath == null || !File.Exists(envPath))
                {
                    return;
                }

                // Load .env file
                string[] lines = File.ReadAllLines(envPath);
                foreach (string line in lines)
                {
                    string trimmed = line.Trim();

                    // Skip empty lines and comments
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    // Parse KEY=value
                    int equalsIndex = trimmed.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = trimmed.Substring(0, equalsIndex).Trim();
                        string value = trimmed.Substring(equalsIndex + 1).Trim();

                        // Remove quotes if present
                        if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                            (value.StartsWith("'") && value.EndsWith("'")))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }

                        // Set environment variable for current process
                        Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
                    }
                }
            }
            catch
            {
                // Silently fail if .env can't be loaded
            }
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
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
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

#pragma warning disable CA1416 // Validate platform compatibility
            // Try to find Steam installation
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        steamApps = Path.Combine(key.GetValue("InstallPath") as string ?? "", "steamapps", "common");
                    }
                }

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        steamApps = Path.Combine(key.GetValue("InstallPath") as string ?? "", "steamapps", "common");
                    }
                }
#pragma warning restore CA1416 // Validate platform compatibility
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

        /// <summary>
        /// Finds all installation paths for NWN, DA, ME games from default locations.
        /// Similar to FindKotorPathsFromDefault but for other BioWare games.
        /// </summary>
        public static List<string> FindGamePathsFromDefault(GameType game)
        {
            var paths = new List<string>();

            // Try environment variable first
            string envPath = TryEnvironmentVariableForGame(game);
            if (!string.IsNullOrEmpty(envPath) && !paths.Contains(envPath))
            {
                paths.Add(envPath);
            }

            // Try registry paths
            string registryPath = TryRegistryForGame(game);
            if (!string.IsNullOrEmpty(registryPath) && !paths.Contains(registryPath))
            {
                paths.Add(registryPath);
            }

            // Try Steam paths (check multiple library locations)
            List<string> steamPaths = TrySteamPathsForGame(game);
            foreach (string path in steamPaths)
            {
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            // Try GOG paths
            List<string> gogPaths = TryGogPathsForGame(game);
            foreach (string path in gogPaths)
            {
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            // Try common paths
            List<string> commonPaths = TryCommonPathsForGame(game);
            foreach (string path in commonPaths)
            {
                if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Tries to get the game path from environment variables.
        /// Supports NWN_PATH, NWN2_PATH, DA_PATH, DA2_PATH, ME_PATH, ME2_PATH, ME3_PATH.
        /// </summary>
        private static string TryEnvironmentVariableForGame(GameType game)
        {
            // Load .env file if it exists
            LoadEnvFile();

            string envVarName = GetEnvironmentVariableName(game);
            if (string.IsNullOrEmpty(envVarName))
            {
                return null;
            }

            string path = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(path) && IsValidGameInstallation(path, game))
            {
                return path;
            }

            return null;
        }

        /// <summary>
        /// Gets the environment variable name for a game.
        /// </summary>
        private static string GetEnvironmentVariableName(GameType game)
        {
            switch (game)
            {
                case BioWareGame.NWN:
                    return "NWN_PATH";
                case BioWareGame.NWN2:
                    return "NWN2_PATH";
                case BioWareGame.DA:
                    return "DA_PATH";
                case BioWareGame.DA2:
                    return "DA2_PATH";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Tries to get the game path from Windows registry.
        /// </summary>
        private static string TryRegistryForGame(GameType game)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return null;
            }

            try
            {
                string[] registryKeys = GetRegistryKeysForGame(game);
                if (registryKeys == null || registryKeys.Length == 0)
                {
                    return null;
                }

                foreach (string keyPath in registryKeys)
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                    {
                        if (key != null)
                        {
                            // Try common registry value names
                            string[] valueNames = { "Path", "InstallPath", "Install Dir", "INSTALLDIR", "Location" };
                            foreach (string valueName in valueNames)
                            {
                                string path = key.GetValue(valueName) as string;
                                if (!string.IsNullOrEmpty(path) && IsValidGameInstallation(path, game))
                                {
                                    return path;
                                }
                            }
                        }
                    }

                    // Also try Wow6432Node for 64-bit Windows
                    string wow64KeyPath = keyPath.Replace(@"SOFTWARE\", @"SOFTWARE\Wow6432Node\");
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(wow64KeyPath))
                    {
                        if (key != null)
                        {
                            string[] valueNames = { "Path", "InstallPath", "Install Dir", "INSTALLDIR", "Location" };
                            foreach (string valueName in valueNames)
                            {
                                string path = key.GetValue(valueName) as string;
                                if (!string.IsNullOrEmpty(path) && IsValidGameInstallation(path, game))
                                {
                                    return path;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Registry access may fail
            }

            return null;
        }

        /// <summary>
        /// Gets registry key paths for a game.
        /// </summary>
        private static string[] GetRegistryKeysForGame(GameType game)
        {
            switch (game)
            {
                case BioWareGame.NWN:
                    return new[]
                    {
                        @"SOFTWARE\BioWare\NWN\Neverwinter",
                        @"SOFTWARE\BioWare\Neverwinter Nights",
                        @"SOFTWARE\Wizards of the Coast\Neverwinter Nights"
                    };
                case BioWareGame.NWN2:
                    return new[]
                    {
                        @"SOFTWARE\Obsidian\Neverwinter Nights 2",
                        @"SOFTWARE\Atari\Neverwinter Nights 2"
                    };
                case BioWareGame.DA:
                    return new[]
                    {
                        @"SOFTWARE\BioWare\Dragon Age",
                        @"SOFTWARE\BioWare\Dragon Age Origins",
                        @"SOFTWARE\Electronic Arts\Dragon Age Origins"
                    };
                case BioWareGame.DA2:
                    return new[]
                    {
                        @"SOFTWARE\BioWare\Dragon Age 2",
                        @"SOFTWARE\Electronic Arts\Dragon Age II"
                    };
                default:
                    return null;
            }
        }

        /// <summary>
        /// Tries to find game paths in Steam library locations.
        /// </summary>
        private static List<string> TrySteamPathsForGame(GameType game)
        {
            var paths = new List<string>();
            string steamApps = null;

            // Try to find Steam installation from registry
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    {
                        if (key != null)
                        {
                            steamApps = Path.Combine(key.GetValue("InstallPath") as string ?? "", "steamapps", "common");
                        }
                    }

                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam"))
                    {
                        if (key != null)
                        {
                            steamApps = Path.Combine(key.GetValue("InstallPath") as string ?? "", "steamapps", "common");
                        }
                    }
                }
                catch { }
            }

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

            string gameName = GetSteamGameName(game);
            if (string.IsNullOrEmpty(gameName))
            {
                return paths;
            }

            foreach (string library in steamLibraries)
            {
                if (string.IsNullOrEmpty(library)) continue;

                string path = Path.Combine(library, gameName);
                if (IsValidGameInstallation(path, game))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Gets the Steam game folder name for a game.
        /// </summary>
        private static string GetSteamGameName(GameType game)
        {
            switch (game)
            {
                case BioWareGame.NWN:
                    return "Neverwinter Nights";
                case BioWareGame.NWN2:
                    return "Neverwinter Nights 2";
                case BioWareGame.DA:
                    return "Dragon Age Origins";
                case BioWareGame.DA2:
                    return "Dragon Age II";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Tries to find game paths in GOG installation locations.
        /// </summary>
        private static List<string> TryGogPathsForGame(GameType game)
        {
            var paths = new List<string>();
            string[] gogPaths = GetGogPathsForGame(game);

            foreach (string path in gogPaths)
            {
                if (IsValidGameInstallation(path, game))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Gets common GOG installation paths for a game.
        /// </summary>
        private static string[] GetGogPathsForGame(GameType game)
        {
            switch (game)
            {
                case BioWareGame.NWN:
                    return new[]
                    {
                        @"C:\GOG Games\Neverwinter Nights",
                        @"C:\Program Files (x86)\GOG Galaxy\Games\Neverwinter Nights",
                        @"D:\GOG Games\Neverwinter Nights"
                    };
                case BioWareGame.NWN2:
                    return new[]
                    {
                        @"C:\GOG Games\Neverwinter Nights 2",
                        @"C:\Program Files (x86)\GOG Galaxy\Games\Neverwinter Nights 2",
                        @"D:\GOG Games\Neverwinter Nights 2"
                    };
                case BioWareGame.DA:
                    return new[]
                    {
                        @"C:\GOG Games\Dragon Age Origins",
                        @"C:\Program Files (x86)\GOG Galaxy\Games\Dragon Age Origins",
                        @"D:\GOG Games\Dragon Age Origins"
                    };
                case BioWareGame.DA2:
                    return new[]
                    {
                        @"C:\GOG Games\Dragon Age II",
                        @"C:\Program Files (x86)\GOG Galaxy\Games\Dragon Age II",
                        @"D:\GOG Games\Dragon Age II"
                    };
                default:
                    return new string[0];
            }
        }

        /// <summary>
        /// Tries to find game paths in common installation locations.
        /// </summary>
        private static List<string> TryCommonPathsForGame(GameType game)
        {
            var paths = new List<string>();
            string[] commonPaths = GetCommonPathsForGame(game);

            foreach (string path in commonPaths)
            {
                if (IsValidGameInstallation(path, game))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        /// <summary>
        /// Gets common installation paths for a game.
        /// </summary>
        private static string[] GetCommonPathsForGame(GameType game)
        {
            switch (game)
            {
                case BioWareGame.NWN:
                    return new[]
                    {
                        @"C:\Program Files (x86)\Neverwinter Nights",
                        @"C:\Program Files\Neverwinter Nights",
                        @"C:\Games\Neverwinter Nights",
                        @"D:\Games\Neverwinter Nights"
                    };
                case BioWareGame.NWN2:
                    return new[]
                    {
                        @"C:\Program Files (x86)\Neverwinter Nights 2",
                        @"C:\Program Files\Neverwinter Nights 2",
                        @"C:\Program Files (x86)\Obsidian\Neverwinter Nights 2",
                        @"C:\Games\Neverwinter Nights 2",
                        @"D:\Games\Neverwinter Nights 2"
                    };
                case BioWareGame.DA:
                    return new[]
                    {
                        @"C:\Program Files (x86)\Dragon Age",
                        @"C:\Program Files\Dragon Age",
                        @"C:\Program Files (x86)\BioWare\Dragon Age",
                        @"C:\Games\Dragon Age Origins",
                        @"D:\Games\Dragon Age Origins"
                    };
                case BioWareGame.DA2:
                    return new[]
                    {
                        @"C:\Program Files (x86)\Dragon Age 2",
                        @"C:\Program Files\Dragon Age 2",
                        @"C:\Program Files (x86)\BioWare\Dragon Age 2",
                        @"C:\Games\Dragon Age II",
                        @"D:\Games\Dragon Age II"
                    };
                default:
                    return new string[0];
            }
        }

        /// <summary>
        /// Verifies a path is a valid game installation.
        /// Uses the same validation logic as GameLauncher.ValidateInstallation.
        /// </summary>
        public static bool IsValidGameInstallation(string path, GameType game)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }

            switch (game)
            {
                case BioWareGame.NWN:
                    // Validate Neverwinter Nights installation
                    string nwnChitinKey = Path.Combine(path, "chitin.key");
                    string nwnExe = Path.Combine(path, "nwmain.exe");
                    string nwnExeUpper = Path.Combine(path, "NWMAIN.EXE");
                    string nwnGuiErf = Path.Combine(path, "gui_32bit.erf");
                    string nwnDataDir = Path.Combine(path, "data");

                    return File.Exists(nwnChitinKey) &&
                           (File.Exists(nwnExe) || File.Exists(nwnExeUpper)) &&
                           File.Exists(nwnGuiErf) &&
                           Directory.Exists(nwnDataDir);

                case BioWareGame.NWN2:
                    // Validate Neverwinter Nights 2 installation
                    string nwn2Exe = Path.Combine(path, "nwn2main.exe");
                    string nwn2ExeUpper = Path.Combine(path, "NWN2MAIN.EXE");
                    string nwn2DataDir = Path.Combine(path, "data");
                    string nwn2TwoDaZip = Path.Combine(path, "2da.zip");
                    string nwn2ActorsZip = Path.Combine(path, "actors.zip");
                    string nwn2ModelsZip = Path.Combine(path, "nwn2_models.zip");
                    string nwn2ScriptsZip = Path.Combine(path, "scripts.zip");

                    return (File.Exists(nwn2Exe) || File.Exists(nwn2ExeUpper)) &&
                           Directory.Exists(nwn2DataDir) &&
                           File.Exists(nwn2TwoDaZip) &&
                           File.Exists(nwn2ActorsZip) &&
                           File.Exists(nwn2ModelsZip) &&
                           File.Exists(nwn2ScriptsZip);

                case BioWareGame.DA:
                    // Validate Dragon Age: Origins installation
                    // Based on xoreos/src/engines/dragonage/probes.cpp:69-75
                    // Required files:
                    // - daoriginslauncher.exe: Launcher executable (Windows retail, mandatory)
                    // - daorigins.exe: Main game executable (mandatory)
                    // - packages directory: Eclipse Engine package structure (mandatory)
                    // - data directory: Contains game data files (mandatory)
                    // - data/global.rim: Global resource archive (mandatory, DA:O specific)
                    string daLauncherExe = Path.Combine(path, "daoriginslauncher.exe");
                    string daLauncherExeUpper = Path.Combine(path, "DAORIGINSLAUNCHER.EXE");
                    string daOriginsExe = Path.Combine(path, "daorigins.exe");
                    string daOriginsExeUpper = Path.Combine(path, "DAORIGINS.EXE");
                    string daPackagesDir = Path.Combine(path, "packages");
                    string daDataDir = Path.Combine(path, "data");
                    string daGlobalRim = Path.Combine(daDataDir, "global.rim");

                    // Check for launcher (Windows retail) OR main executable
                    bool hasLauncher = File.Exists(daLauncherExe) || File.Exists(daLauncherExeUpper);
                    bool hasExe = File.Exists(daOriginsExe) || File.Exists(daOriginsExeUpper);
                    bool hasPackagesDir = Directory.Exists(daPackagesDir);
                    bool hasDataDir = Directory.Exists(daDataDir);
                    bool hasGlobalRim = File.Exists(daGlobalRim);

                    // All mandatory files/directories must exist
                    // Either launcher OR main executable is acceptable (different distribution methods)
                    return (hasLauncher || hasExe) && hasPackagesDir && hasDataDir && hasGlobalRim;

                case BioWareGame.DA2:
                    // Validate Dragon Age II installation
                    // Based on xoreos/src/engines/dragonage2/probes.cpp:72-89
                    // Required files:
                    // - dragonage2launcher.exe: Launcher executable (Windows retail, mandatory)
                    // - dragonage2.exe: Main executable in bin_ship (Windows Origin, mandatory)
                    // - DragonAge2.exe: Main game executable (mandatory)
                    // - modules/campaign_base/campaign_base.cif: Campaign base module (mandatory, DA2 specific)
                    string da2LauncherExe = Path.Combine(path, "dragonage2launcher.exe");
                    string da2LauncherExeUpper = Path.Combine(path, "DRAGONAGE2LAUNCHER.EXE");
                    string da2Exe = Path.Combine(path, "DragonAge2.exe");
                    string da2ExeUpper = Path.Combine(path, "DRAGONAGE2.EXE");
                    string da2ExeLower = Path.Combine(path, "dragonage2.exe");
                    string da2BinShipExe = Path.Combine(path, "bin_ship", "dragonage2.exe");
                    string da2BinShipExeUpper = Path.Combine(path, "bin_ship", "DRAGONAGE2.EXE");
                    string da2CampaignBaseCif = Path.Combine(path, "modules", "campaign_base", "campaign_base.cif");

                    // Check for launcher (Windows retail) OR main executable (various locations)
                    bool hasDa2Launcher = File.Exists(da2LauncherExe) || File.Exists(da2LauncherExeUpper);
                    bool hasDa2Exe = File.Exists(da2Exe) || File.Exists(da2ExeUpper) || File.Exists(da2ExeLower);
                    bool hasDa2BinShipExe = File.Exists(da2BinShipExe) || File.Exists(da2BinShipExeUpper);
                    bool hasCampaignBaseCif = File.Exists(da2CampaignBaseCif);

                    // All mandatory files must exist
                    // Either launcher OR main executable is acceptable (different distribution methods)
                    return (hasDa2Launcher || hasDa2Exe || hasDa2BinShipExe) && hasCampaignBaseCif;

                default:
                    return false;
            }
        }
    }
}

