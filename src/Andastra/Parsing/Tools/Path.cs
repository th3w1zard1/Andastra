using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Andastra.Parsing;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/path.py:1383-1457
    // Original: def get_default_paths() -> dict[str, dict[Game, list[str]]]:
    /// <summary>
    /// Gets default hardcoded paths for KOTOR installations on different platforms.
    /// </summary>
    public static class PathTools
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/path.py:1383-1457
        // Original: def get_default_paths() -> dict[str, dict[Game, list[str]]]:
        /// <summary>
        /// Gets default hardcoded paths for KOTOR installations on different platforms.
        /// Note: Some paths (e.g., App Store versions) are incomplete and need community input.
        /// </summary>
        public static Dictionary<string, Dictionary<BioWareGame, List<string>>> GetDefaultPaths()
        {
            return new Dictionary<string, Dictionary<BioWareGame, List<string>>>
            {
                {
                    "Windows",
                    new Dictionary<BioWareGame, List<string>>
                    {
                        {
                            BioWareGame.K1,
                            new List<string>
                            {
                                @"C:\Program Files\Steam\steamapps\common\swkotor",
                                @"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
                                @"C:\Program Files\LucasArts\SWKotOR",
                                @"C:\Program Files (x86)\LucasArts\SWKotOR",
                                @"C:\GOG Games\Star Wars - KotOR",
                                @"C:\Amazon Games\Library\Star Wars - Knights of the Old",
                            }
                        },
                        {
                            BioWareGame.K2,
                            new List<string>
                            {
                                @"C:\Program Files\Steam\steamapps\common\Knights of the Old Republic II",
                                @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II",
                                @"C:\Program Files\LucasArts\SWKotOR2",
                                @"C:\Program Files (x86)\LucasArts\SWKotOR2",
                                @"C:\GOG Games\Star Wars - KotOR2",
                            }
                        }
                    }
                },
                {
                    "Darwin",
                    new Dictionary<BioWareGame, List<string>>
                    {
                        {
                            BioWareGame.K1,
                            new List<string>
                            {
                                "~/Library/Application Support/Steam/steamapps/common/swkotor/Knights of the Old Republic.app/Contents/Assets",
                                "~/Library/Applications/Steam/steamapps/common/swkotor/Knights of the Old Republic.app/Contents/Assets/",
                                // App Store version path not yet determined - needs community input
                            }
                        },
                        {
                            BioWareGame.K2,
                            new List<string>
                            {
                                "~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/Knights of the Old Republic II.app/Contents/Assets",
                                "~/Library/Applications/Steam/steamapps/common/Knights of the Old Republic II/Star Wars™: Knights of the Old Republic II.app/Contents/GameData",
                                "~/Library/Application Support/Steam/steamapps/common/Knights of the Old Republic II/KOTOR2.app/Contents/GameData/",
                                "~/Applications/Knights of the Old Republic 2.app/Contents/Resources/transgaming/c_drive/Program Files/SWKotOR2/",
                                "/Applications/Knights of the Old Republic 2.app/Contents/Resources/transgaming/c_drive/Program Files/SWKotOR2/",
                                // App Store version path not yet determined - needs community input
                            }
                        }
                    }
                },
                {
                    "Linux",
                    new Dictionary<BioWareGame, List<string>>
                    {
                        {
                            BioWareGame.K1,
                            new List<string>
                            {
                                "~/.local/share/steam/common/steamapps/swkotor",
                                "~/.local/share/steam/common/swkotor",
                                "~/.steam/debian-installation/steamapps/common/swkotor",
                                "~/.steam/root/steamapps/common/swkotor",
                                "~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/swkotor",
                                "/mnt/C/Program Files/Steam/steamapps/common/swkotor",
                                "/mnt/C/Program Files (x86)/Steam/steamapps/common/swkotor",
                                "/mnt/C/Program Files/LucasArts/SWKotOR",
                                "/mnt/C/Program Files (x86)/LucasArts/SWKotOR",
                                "/mnt/C/GOG Games/Star Wars - KotOR",
                                "/mnt/C/Amazon Games/Library/Star Wars - Knights of the Old",
                            }
                        },
                        {
                            BioWareGame.K2,
                            new List<string>
                            {
                                "~/.local/share/Steam/common/steamapps/Knights of the Old Republic II",
                                "~/.local/share/Steam/common/steamapps/kotor2",
                                "~/.local/share/aspyr-media/kotor2",
                                "~/.local/share/aspyr-media/Knights of the Old Republic II",
                                "~/.local/share/Steam/common/Knights of the Old Republic II",
                                "~/.steam/debian-installation/steamapps/common/Knights of the Old Republic II",
                                "~/.steam/debian-installation/steamapps/common/kotor2",
                                "~/.steam/root/steamapps/common/Knights of the Old Republic II",
                                "~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Knights of the Old Republic II/steamassets",
                                "/mnt/C/Program Files/Steam/steamapps/common/Knights of the Old Republic II",
                                "/mnt/C/Program Files (x86)/Steam/steamapps/common/Knights of the Old Republic II",
                                "/mnt/C/Program Files/LucasArts/SWKotOR2",
                                "/mnt/C/Program Files (x86)/LucasArts/SWKotOR2",
                                "/mnt/C/GOG Games/Star Wars - KotOR2",
                            }
                        }
                    }
                }
            };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/path.py:1460-1507
        // Original: def find_kotor_paths_from_default() -> dict[Game, list[CaseAwarePath]]:
        /// <summary>
        /// Finds paths to Knights of the Old Republic game data directories.
        /// </summary>
        /// <returns>A dictionary mapping Games to lists of existing path locations.</returns>
        /// <remarks>
        /// Processing Logic:
        /// - Gets default hardcoded path locations from a lookup table
        /// - Resolves paths and filters out non-existing ones
        /// - On Windows, also searches the registry for additional locations
        /// - Returns results as lists for each Game rather than sets
        /// </remarks>
        public static Dictionary<BioWareGame, List<CaseAwarePath>> FindKotorPathsFromDefault()
        {
            string osStr = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Windows" :
                          RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Darwin" :
                          RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : "Unknown";

            // Build hardcoded default kotor locations
            Dictionary<string, Dictionary<BioWareGame, List<string>>> rawLocations = GetDefaultPaths();
            Dictionary<BioWareGame, HashSet<CaseAwarePath>> locations = new Dictionary<BioWareGame, HashSet<CaseAwarePath>>();

            if (rawLocations.TryGetValue(osStr, out Dictionary<BioWareGame, List<string>> osPaths))
            {
                foreach (KeyValuePair<BioWareGame, List<string>> gamePaths in osPaths)
                {
                    HashSet<CaseAwarePath> gameLocations = new HashSet<CaseAwarePath>();
                    foreach (string path in gamePaths.Value)
                    {
                        try
                        {
                            // Expand user directory (~)
                            string expandedPath = path;
                            if (path.StartsWith("~"))
                            {
                                string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                expandedPath = path.Replace("~", homeDir);
                            }

                            CaseAwarePath casePath = new CaseAwarePath(expandedPath);
                            string resolvedPath = casePath.GetResolvedPath();
                            if (Directory.Exists(resolvedPath))
                            {
                                gameLocations.Add(new CaseAwarePath(resolvedPath));
                            }
                        }
                        catch
                        {
                            // Skip invalid paths
                        }
                    }
                    locations[gamePaths.Key] = gameLocations;
                }
            }
            else
            {
                // Initialize empty sets for unknown OS
                locations[BioWareGame.K1] = new HashSet<CaseAwarePath>();
                locations[BioWareGame.K2] = new HashSet<CaseAwarePath>();
            }

            // Build kotor locations by registry (if on windows)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Get registry paths for K1 and K2
                List<Tuple<string, string>> k1RegPaths = Registry.WinregKey(BioWareGame.K1);
                List<Tuple<string, string>> k2RegPaths = Registry.WinregKey(BioWareGame.K2);

                foreach (Tuple<string, string> regPath in k1RegPaths)
                {
                    string pathStr = Registry.ResolveRegKeyToPath(regPath.Item1, regPath.Item2);
                    if (!string.IsNullOrEmpty(pathStr))
                    {
                        try
                        {
                            CaseAwarePath path = new CaseAwarePath(pathStr);
                            string resolvedPath = path.GetResolvedPath();
                            if (Directory.Exists(resolvedPath))
                            {
                                if (!locations.ContainsKey(BioWareGame.K1))
                                {
                                    locations[BioWareGame.K1] = new HashSet<CaseAwarePath>();
                                }
                                locations[BioWareGame.K1].Add(new CaseAwarePath(resolvedPath));
                            }
                        }
                        catch
                        {
                            // Skip invalid paths
                        }
                    }
                }

                foreach (Tuple<string, string> regPath in k2RegPaths)
                {
                    string pathStr = Registry.ResolveRegKeyToPath(regPath.Item1, regPath.Item2);
                    if (!string.IsNullOrEmpty(pathStr))
                    {
                        try
                        {
                            CaseAwarePath path = new CaseAwarePath(pathStr);
                            string resolvedPath = path.GetResolvedPath();
                            if (Directory.Exists(resolvedPath))
                            {
                                if (!locations.ContainsKey(BioWareGame.K2))
                                {
                                    locations[BioWareGame.K2] = new HashSet<CaseAwarePath>();
                                }
                                locations[BioWareGame.K2].Add(new CaseAwarePath(resolvedPath));
                            }
                        }
                        catch
                        {
                            // Skip invalid paths
                        }
                    }
                }

                // Check for Amazon K1 path
                string amazonK1PathStr = Registry.FindSoftwareKey("AmazonGames/Star Wars - Knights of the Old");
                if (!string.IsNullOrEmpty(amazonK1PathStr) && Directory.Exists(amazonK1PathStr))
                {
                    if (!locations.ContainsKey(BioWareGame.K1))
                    {
                        locations[BioWareGame.K1] = new HashSet<CaseAwarePath>();
                    }
                    locations[BioWareGame.K1].Add(new CaseAwarePath(amazonK1PathStr));
                }
            }

            // Don't return nested sets, return as lists
            return new Dictionary<BioWareGame, List<CaseAwarePath>>
            {
                { BioWareGame.K1, locations.ContainsKey(BioWareGame.K1) ? locations[BioWareGame.K1].ToList() : new List<CaseAwarePath>() },
                { BioWareGame.K2, locations.ContainsKey(BioWareGame.K2) ? locations[BioWareGame.K2].ToList() : new List<CaseAwarePath>() }
            };
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/path.py:51-67
        // Original: def is_filesystem_case_sensitive(path: os.PathLike | str) -> bool | None:
        /// <summary>
        /// Check if the filesystem at the given path is case-sensitive.
        /// This function creates a temporary file to test the filesystem behavior.
        /// </summary>
        public static bool? IsFilesystemCaseSensitive(string path)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    string testFile = Path.Combine(tempDir, "case_test_file");
                    File.Create(testFile).Close();

                    // Attempt to access the same file with a different case to check case sensitivity
                    string testFileUpper = Path.Combine(tempDir, "CASE_TEST_FILE");
                    bool caseSensitive = !File.Exists(testFileUpper);
                    return caseSensitive;
                }
                finally
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
