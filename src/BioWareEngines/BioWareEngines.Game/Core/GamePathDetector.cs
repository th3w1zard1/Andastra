using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using BioWareEngines.Core;

namespace BioWareEngines.Game.Core
{
    /// <summary>
    /// Detects KOTOR installation paths from common locations.
    /// </summary>
    /// <remarks>
    /// Game Path Detection:
    /// - Based on swkotor2.exe installation path detection system
    /// - Located via string references: Original engine reads installation path from Windows registry
    /// - Registry access: Uses Windows Registry API (RegOpenKeyEx, RegQueryValueEx) for path lookup
    /// - Registry keys: K1 uses "SOFTWARE\BioWare\SW\KOTOR" or "SOFTWARE\LucasArts\KotOR"
    /// - Registry keys: K2 uses "SOFTWARE\Obsidian\KOTOR2" or "SOFTWARE\LucasArts\KotOR2"
    /// - Registry value: "Path" entry contains installation directory path (HKEY_LOCAL_MACHINE)
    /// - Wow6432Node: 64-bit Windows registry redirector for 32-bit applications (checks both locations)
    /// - Validation: Checks for chitin.key (keyfile) and game executable (swkotor.exe/swkotor2.exe)
    /// - chitin.key: Keyfile containing resource file mappings and encryption keys (required for resource loading)
    /// - Executable validation: Checks for swkotor.exe (K1) or swkotor2.exe (K2) in installation directory
    /// - Engine initialization: FUN_00404250 @ 0x00404250 reads installation path during startup
    /// - Path resolution: FUN_00633270 @ 0x00633270 resolves resource paths based on installation directory
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

