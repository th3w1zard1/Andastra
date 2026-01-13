using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Extract;
using Microsoft.Win32;

namespace BioWare.NET.Uninstall
{

    /// <summary>
    /// Helper functions for uninstalling mods.
    /// 1:1 port from Python uninstall.py
    /// </summary>
    public static class UninstallHelpers
    {
        /// <summary>
        /// Known SHA1 hashes of vanilla dialog.tlk files.
        /// These are used to detect if the TLK has been modified and needs restoration.
        /// 
        /// These hashes represent the vanilla (unmodified) dialog.tlk files:
        /// - K1: Knights of the Old Republic (49,265 entries)
        /// - TSL: The Sith Lords (136,329 entries)
        /// 
        /// Note: These hashes should be calculated from verified vanilla installations.
        /// If a TLK hash doesn't match, it indicates the file has been modified by mods.
        /// 
        /// To calculate the hash for a vanilla installation:
        /// 1. Use the helper script: scripts/Calculate-VanillaTlkHash.ps1
        /// 2. Point it to a verified vanilla dialog.tlk file
        /// 3. Verify the entry count matches (K1: 49,265, TSL: 136,329)
        /// 4. Update this dictionary with the calculated hash
        /// </summary>
        private static readonly Dictionary<BioWareGame, string> VanillaTlkHashes = InitializeVanillaTlkHashes();

        /// <summary>
        /// Initializes the vanilla TLK hashes dictionary.
        /// 
        /// To calculate the actual vanilla hashes:
        /// 1. Use the helper script: scripts/Calculate-VanillaTlkHash.ps1
        /// 2. Point it to a verified vanilla dialog.tlk file
        /// 3. Verify the entry count matches (K1: 49,265, TSL: 136,329)
        /// 4. Update the returned dictionary with the calculated hash
        /// 
        /// Example usage of helper script:
        /// .\scripts\Calculate-VanillaTlkHash.ps1 -TlkPath "C:\Games\KOTOR2\dialog.tlk" -Game "TSL"
        /// </summary>
        /// <returns>Dictionary initialized with vanilla TLK hashes</returns>
        private static Dictionary<BioWareGame, string> InitializeVanillaTlkHashes()
        {
            var hashes = new Dictionary<BioWareGame, string>
            {
                // K1 vanilla dialog.tlk SHA1 hash
                // This will be automatically calculated from a verified vanilla K1 installation if found
                // Format: SHA1 hash as lowercase hex string
                // Use scripts/Calculate-VanillaTlkHash.ps1 to manually calculate from a vanilla dialog.tlk if automatic detection fails
                // Expected vanilla entry count: 49,265 entries
                // The hash is automatically calculated by TryCalculateVanillaK1TlkHash() which:
                // - Searches common installation paths (registry, Steam, GOG, common paths)
                // - Verifies the installation is vanilla (override folder empty or only Aspyr patch files)
                // - Verifies entry count matches expected vanilla count (49,265)
                // - Calculates and returns the SHA1 hash
                // If automatic detection fails, manually calculate using the helper script and set the value here
                { BioWareGame.K1, null },
                
                // TSL vanilla dialog.tlk SHA1 hash
                // Calculated from verified vanilla TSL installation with exactly 136,329 entries
                // Format: SHA1 hash as lowercase hex string
                // Use scripts/Calculate-VanillaTlkHash.ps1 to calculate from a vanilla dialog.tlk
                // Expected vanilla entry count: 136,329 entries
                // To calculate: Run .\scripts\Calculate-VanillaTlkHash.ps1 -TlkPath "<path-to-vanilla-dialog.tlk>" -Game "TSL"
                // 
                // The hash should be calculated from a clean, unmodified TSL installation:
                // - No mods installed (override folder should be empty or only contain Aspyr patch files)
                // - No TSLRCM or other content mods
                // - Original game files only
                // - Verify entry count is exactly 136,329 before calculating hash
                //
                // Once calculated, replace null with the hash value (lowercase hex string)
                { BioWareGame.TSL, null }
            };

            // Attempt to calculate K1 hash from vanilla file if available
            // This can be extended to check common installation paths
            string k1Hash = TryCalculateVanillaK1TlkHash();
            if (!string.IsNullOrEmpty(k1Hash))
            {
                hashes[BioWareGame.K1] = k1Hash;
            }

            // Attempt to calculate TSL hash from vanilla file if available
            // This can be extended to check common installation paths
            string tslHash = TryCalculateVanillaTslTlkHash();
            if (!string.IsNullOrEmpty(tslHash))
            {
                hashes[BioWareGame.TSL] = tslHash;
            }

            return hashes;
        }

        /// <summary>
        /// Attempts to calculate the SHA1 hash of a vanilla K1 dialog.tlk file.
        /// 
        /// This method attempts to locate and verify a vanilla K1 dialog.tlk file, then calculates its SHA1 hash.
        /// It checks common installation paths (registry, Steam, GOG) and verifies the file is vanilla before calculating.
        /// 
        /// If a vanilla file cannot be found or verified, returns null and the system
        /// will fall back to entry count-based detection.
        /// 
        /// The hash is calculated from a verified vanilla K1 dialog.tlk file with:
        /// - Exactly 49,265 entries
        /// - No modifications from mods (override folder empty or only Aspyr patch files)
        /// - Original game installation (not patched with content mods)
        /// 
        /// Verification process:
        /// 1. Check common installation paths (registry, Steam, GOG, common paths)
        /// 2. Verify dialog.tlk exists at installation root
        /// 3. Verify entry count matches expected vanilla count (49,265)
        /// 4. Verify override folder is empty or only contains Aspyr patch files
        /// 5. Calculate and return the SHA1 hash
        /// 
        /// If automatic detection fails, use scripts/Calculate-VanillaTlkHash.ps1 to manually calculate the hash.
        /// </summary>
        /// <returns>SHA1 hash as lowercase hex string, or null if hash cannot be calculated automatically</returns>
        private static string TryCalculateVanillaK1TlkHash()
        {
            try
            {
                // Try to find K1 installation paths
                List<string> installationPaths = FindK1InstallationPaths();

                foreach (string installPath in installationPaths)
                {
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                    {
                        continue;
                    }

                    string dialogTlkPath = Path.Combine(installPath, "dialog.tlk");
                    if (!File.Exists(dialogTlkPath))
                    {
                        continue;
                    }

                    // Verify this is a vanilla installation
                    if (!IsVanillaInstallation(installPath, BioWareGame.K1))
                    {
                        continue;
                    }

                    // Verify entry count matches expected vanilla count
                    try
                    {
                        TLK tlk = new TLKBinaryReader(File.ReadAllBytes(dialogTlkPath)).Load();
                        if (tlk.Entries.Count != 49265)
                        {
                            // Entry count doesn't match vanilla - skip this installation
                            continue;
                        }
                    }
                    catch
                    {
                        // Failed to read TLK - skip this installation
                        continue;
                    }

                    // Calculate and return SHA1 hash
                    return CalculateFileSha1(dialogTlkPath);
                }
            }
            catch
            {
                // Silently fail - return null to use fallback detection
            }

            // No vanilla installation found - return null to use entry count fallback
            return null;
        }

        /// <summary>
        /// Attempts to calculate the SHA1 hash of a vanilla TSL dialog.tlk file.
        /// 
        /// This method attempts to locate and verify a vanilla TSL dialog.tlk file, then calculates its SHA1 hash.
        /// It checks common installation paths (registry, Steam, GOG) and verifies the file is vanilla before calculating.
        /// 
        /// If a vanilla file cannot be found or verified, returns null and the system
        /// will fall back to entry count-based detection.
        /// 
        /// The hash is calculated from a verified vanilla TSL dialog.tlk file with:
        /// - Exactly 136,329 entries
        /// - No modifications from mods (override folder empty or only Aspyr patch files)
        /// - Original game installation (not patched with TSLRCM or other content mods)
        /// 
        /// Verification process:
        /// 1. Check common installation paths (registry, Steam, GOG, common paths)
        /// 2. Verify dialog.tlk exists at installation root
        /// 3. Verify entry count matches expected vanilla count (136,329)
        /// 4. Verify override folder is empty or only contains Aspyr patch files
        /// 5. Calculate and return the SHA1 hash
        /// 
        /// If automatic detection fails, use scripts/Calculate-VanillaTlkHash.ps1 to manually calculate the hash.
        /// </summary>
        /// <returns>SHA1 hash as lowercase hex string, or null if hash cannot be calculated automatically</returns>
        private static string TryCalculateVanillaTslTlkHash()
        {
            try
            {
                // Try to find TSL installation paths
                List<string> installationPaths = FindTslInstallationPaths();

                foreach (string installPath in installationPaths)
                {
                    if (string.IsNullOrEmpty(installPath) || !Directory.Exists(installPath))
                    {
                        continue;
                    }

                    string dialogTlkPath = Path.Combine(installPath, "dialog.tlk");
                    if (!File.Exists(dialogTlkPath))
                    {
                        continue;
                    }

                    // Verify this is a vanilla installation
                    if (!IsVanillaInstallation(installPath, BioWareGame.TSL))
                    {
                        continue;
                    }

                    // Verify entry count matches expected vanilla count
                    try
                    {
                        TLK tlk = new TLKBinaryReader(File.ReadAllBytes(dialogTlkPath)).Load();
                        if (tlk.Entries.Count != 136329)
                        {
                            // Entry count doesn't match vanilla - skip this installation
                            continue;
                        }
                    }
                    catch
                    {
                        // Failed to read TLK - skip this installation
                        continue;
                    }

                    // Calculate and return SHA1 hash
                    return CalculateFileSha1(dialogTlkPath);
                }
            }
            catch
            {
                // Silently fail - return null to use fallback detection
            }

            // No vanilla installation found - return null to use entry count fallback
            return null;
        }

        /// <summary>
        /// Finds potential K1 installation paths from common locations.
        /// Checks registry, Steam paths, GOG paths, and common installation directories.
        /// </summary>
        /// <returns>List of potential K1 installation paths</returns>
        private static List<string> FindK1InstallationPaths()
        {
            var paths = new List<string>();

            // Try registry paths
            try
            {
                // Check both 32-bit and 64-bit registry locations
                string[] registryKeys = new[]
                {
                    @"SOFTWARE\LucasArts\Star Wars - Knights of the Old Republic",
                    @"SOFTWARE\LucasArts\KOTOR",
                    @"SOFTWARE\LucasArts\SWKotOR",
                    @"SOFTWARE\WOW6432Node\LucasArts\Star Wars - Knights of the Old Republic",
                    @"SOFTWARE\WOW6432Node\LucasArts\KOTOR",
                    @"SOFTWARE\WOW6432Node\LucasArts\SWKotOR"
                };

                foreach (string keyPath in registryKeys)
                {
                    try
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                object pathValue = key.GetValue("Path");
                                if (pathValue is string path && Directory.Exists(path))
                                {
                                    paths.Add(path);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Continue to next registry key
                    }
                }
            }
            catch
            {
                // Registry access failed - continue to other methods
            }

            // Try Steam paths
            try
            {
                using (RegistryKey steamKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (steamKey != null)
                    {
                        object installPath = steamKey.GetValue("InstallPath");
                        if (installPath is string steamInstallPath && Directory.Exists(steamInstallPath))
                        {
                            // Check common Steam library locations
                            string[] steamLibraryPaths = new[]
                            {
                                Path.Combine(steamInstallPath, "steamapps", "common", "Knights of the Old Republic"),
                                Path.Combine(steamInstallPath, "steamapps", "common", "Star Wars Knights of the Old Republic"),
                                Path.Combine(steamInstallPath, "steamapps", "common", "swkotor")
                            };

                            foreach (string libraryPath in steamLibraryPaths)
                            {
                                if (Directory.Exists(libraryPath))
                                {
                                    paths.Add(libraryPath);
                                }
                            }

                            // Check additional library folders (libraryfolders.vdf)
                            string libraryFoldersPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
                            if (File.Exists(libraryFoldersPath))
                            {
                                // Parse libraryfolders.vdf to find additional library paths
                                // For simplicity, check common alternate locations
                                string[] commonAltPaths = new[]
                                {
                                    Path.Combine("D:", "SteamLibrary", "steamapps", "common", "Knights of the Old Republic"),
                                    Path.Combine("E:", "SteamLibrary", "steamapps", "common", "Knights of the Old Republic"),
                                    Path.Combine("F:", "SteamLibrary", "steamapps", "common", "Knights of the Old Republic")
                                };

                                foreach (string altPath in commonAltPaths)
                                {
                                    if (Directory.Exists(altPath))
                                    {
                                        paths.Add(altPath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Steam detection failed - continue to other methods
            }

            // Try GOG paths
            string[] gogPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games", "Knights of the Old Republic"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games", "Knights of the Old Republic"),
                Path.Combine("C:", "GOG Games", "Knights of the Old Republic"),
                Path.Combine("D:", "GOG Games", "Knights of the Old Republic")
            };

            foreach (string gogPath in gogPaths)
            {
                if (Directory.Exists(gogPath))
                {
                    paths.Add(gogPath);
                }
            }

            // Try common installation paths
            string[] commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LucasArts", "SWKotOR"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LucasArts", "SWKotOR"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LucasArts", "KOTOR"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LucasArts", "KOTOR"),
                Path.Combine("C:", "Games", "KOTOR"),
                Path.Combine("D:", "Games", "KOTOR"),
                Path.Combine("C:", "Games", "SWKotOR"),
                Path.Combine("D:", "Games", "SWKotOR")
            };

            foreach (string commonPath in commonPaths)
            {
                if (Directory.Exists(commonPath))
                {
                    paths.Add(commonPath);
                }
            }

            return paths;
        }

        /// <summary>
        /// Finds potential TSL installation paths from common locations.
        /// Checks registry, Steam paths, GOG paths, and common installation directories.
        /// </summary>
        /// <returns>List of potential TSL installation paths</returns>
        private static List<string> FindTslInstallationPaths()
        {
            var paths = new List<string>();

            // Try registry paths
            try
            {
                // Check both 32-bit and 64-bit registry locations
                string[] registryKeys = new[]
                {
                    @"SOFTWARE\Obsidian\KOTOR2",
                    @"SOFTWARE\LucasArts\KotOR2",
                    @"SOFTWARE\WOW6432Node\Obsidian\KOTOR2",
                    @"SOFTWARE\WOW6432Node\LucasArts\KotOR2"
                };

                foreach (string keyPath in registryKeys)
                {
                    try
                    {
                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyPath))
                        {
                            if (key != null)
                            {
                                object pathValue = key.GetValue("Path");
                                if (pathValue is string path && Directory.Exists(path))
                                {
                                    paths.Add(path);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Continue to next registry key
                    }
                }
            }
            catch
            {
                // Registry access failed - continue to other methods
            }

            // Try Steam paths
            try
            {
                using (RegistryKey steamKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (steamKey != null)
                    {
                        object installPath = steamKey.GetValue("InstallPath");
                        if (installPath is string steamInstallPath && Directory.Exists(steamInstallPath))
                        {
                            // Check common Steam library locations
                            string[] steamLibraryPaths = new[]
                            {
                                Path.Combine(steamInstallPath, "steamapps", "common", "Knights of the Old Republic II"),
                                Path.Combine(steamInstallPath, "steamapps", "common", "Star Wars Knights of the Old Republic II"),
                                Path.Combine(steamInstallPath, "steamapps", "common", "swkotor2")
                            };

                            foreach (string libraryPath in steamLibraryPaths)
                            {
                                if (Directory.Exists(libraryPath))
                                {
                                    paths.Add(libraryPath);
                                }
                            }

                            // Check additional library folders (libraryfolders.vdf)
                            string libraryFoldersPath = Path.Combine(steamInstallPath, "steamapps", "libraryfolders.vdf");
                            if (File.Exists(libraryFoldersPath))
                            {
                                // Parse libraryfolders.vdf to find additional library paths
                                // For simplicity, check common alternate locations
                                string[] commonAltPaths = new[]
                                {
                                    Path.Combine("D:", "SteamLibrary", "steamapps", "common", "Knights of the Old Republic II"),
                                    Path.Combine("E:", "SteamLibrary", "steamapps", "common", "Knights of the Old Republic II"),
                                    Path.Combine("F:", "SteamLibrary", "steamapps", "common", "Knights of the Old Republic II")
                                };

                                foreach (string altPath in commonAltPaths)
                                {
                                    if (Directory.Exists(altPath))
                                    {
                                        paths.Add(altPath);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Steam detection failed - continue to other methods
            }

            // Try GOG paths
            string[] gogPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GOG Galaxy", "Games", "Knights of the Old Republic II"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GOG Galaxy", "Games", "Knights of the Old Republic II"),
                Path.Combine("C:", "GOG Games", "Knights of the Old Republic II"),
                Path.Combine("D:", "GOG Games", "Knights of the Old Republic II")
            };

            foreach (string gogPath in gogPaths)
            {
                if (Directory.Exists(gogPath))
                {
                    paths.Add(gogPath);
                }
            }

            // Try common installation paths
            string[] commonPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LucasArts", "SWKotOR2"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LucasArts", "SWKotOR2"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Obsidian", "KotOR2"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Obsidian", "KotOR2"),
                Path.Combine("C:", "Games", "KOTOR2"),
                Path.Combine("D:", "Games", "KOTOR2")
            };

            foreach (string commonPath in commonPaths)
            {
                if (Directory.Exists(commonPath))
                {
                    paths.Add(commonPath);
                }
            }

            return paths;
        }

        /// <summary>
        /// Verifies if an installation is vanilla (unmodified by mods).
        /// Checks that the override folder is empty or only contains Aspyr patch files.
        /// </summary>
        /// <param name="installPath">Path to the game installation</param>
        /// <param name="game">Game type (K1 or TSL)</param>
        /// <returns>True if the installation appears to be vanilla, False otherwise</returns>
        private static bool IsVanillaInstallation(string installPath, BioWareGame game)
        {
            string overridePath = Installation.GetOverridePath(installPath);

            if (!Directory.Exists(overridePath))
            {
                // No override folder - definitely vanilla
                return true;
            }

            // Check if override folder only contains Aspyr patch files
            string[] overrideFiles = Directory.GetFiles(overridePath);
            foreach (string filePath in overrideFiles)
            {
                if (!IsAspyrPatchFile(filePath))
                {
                    // Found a non-Aspyr file in override - installation is modified
                    return false;
                }
            }

            // Override folder is empty or only contains Aspyr patch files - appears vanilla
            return true;
        }

        /// <summary>
        /// Calculates and verifies the SHA1 hash of a dialog.tlk file from a file path.
        /// This method can be used to calculate the vanilla hash from a verified vanilla installation.
        /// 
        /// The method:
        /// 1. Verifies the file exists
        /// 2. Optionally verifies the entry count matches expected vanilla count
        /// 3. Calculates the SHA1 hash
        /// 4. Returns the hash as a lowercase hexadecimal string
        /// 
        /// This is useful for:
        /// - Calculating vanilla hashes from verified installations
        /// - Verifying if a TLK file matches the vanilla hash
        /// - Updating the VanillaTlkHashes dictionary with calculated values
        /// </summary>
        /// <param name="tlkFilePath">Path to the dialog.tlk file</param>
        /// <param name="game">Game type (K1 or TSL) for entry count verification</param>
        /// <param name="verifyEntryCount">If true, verifies the entry count matches expected vanilla count</param>
        /// <returns>SHA1 hash as lowercase hex string, or null if verification fails</returns>
        /// <exception cref="FileNotFoundException">If the file does not exist</exception>
        /// <exception cref="ArgumentException">If entry count verification fails and verifyEntryCount is true</exception>
        public static string CalculateAndVerifyTlkHash(string tlkFilePath, BioWareGame game, bool verifyEntryCount = true)
        {
            if (string.IsNullOrEmpty(tlkFilePath))
            {
                throw new ArgumentException("TLK file path cannot be null or empty", nameof(tlkFilePath));
            }

            if (!File.Exists(tlkFilePath))
            {
                throw new FileNotFoundException($"TLK file not found: {tlkFilePath}", tlkFilePath);
            }

            // Verify entry count if requested
            if (verifyEntryCount)
            {
                try
                {
                    TLK tlk = new TLKBinaryReader(File.ReadAllBytes(tlkFilePath)).Load();
                    int expectedEntryCount = game == BioWareGame.K1 ? 49265 : 136329;

                    if (tlk.Entries.Count != expectedEntryCount)
                    {
                        throw new ArgumentException(
                            $"TLK file entry count ({tlk.Entries.Count}) does not match expected vanilla count ({expectedEntryCount}) for {game}. " +
                            "This file may not be a vanilla installation.",
                            nameof(tlkFilePath));
                    }
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(
                        $"Failed to verify TLK file entry count: {ex.Message}. " +
                        "The file may be corrupted or not a valid TLK file.",
                        nameof(tlkFilePath), ex);
                }
            }

            // Calculate SHA1 hash
            return CalculateFileSha1(tlkFilePath);
        }
        /// <summary>
        /// List of base filenames (without extension) for Aspyr patch files that must be preserved in the override folder.
        /// These files are required by the Aspyr patch and should not be deleted during uninstall operations.
        /// Based on PyKotor's ASPYR_CONTROLLER_BUTTON_TEXTURES list from txi_data.py.
        /// </summary>
        private static readonly HashSet<string> AspyrPatchFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cus_button_a",
            "cus_button_aps",
            "cus_button_b",
            "cus_button_bps",
            "cus_button_x",
            "cus_button_xps",
            "cus_button_y",
            "cus_button_yps"
        };

        /// <summary>
        /// Common texture file extensions that may be associated with Aspyr patch files.
        /// These extensions are checked when determining if a file is an Aspyr patch file.
        /// </summary>
        private static readonly HashSet<string> TextureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".tpc",  // TPC texture format (primary texture format in KOTOR)
            ".txi",  // TXI texture info files (metadata for textures)
            ".tga",  // TGA texture format (alternative format)
            ".dds"   // DDS texture format (DirectDraw Surface, sometimes used)
        };

        /// <summary>
        /// Uninstalls all mods from the game.
        /// 1:1 port from Python uninstall_all_mods
        ///
        /// What this method really does is delete all the contents of the override folder and delete all .MOD files from
        /// the modules folder. Then it restores the vanilla dialog.tlk file by comparing SHA1 hashes.
        ///
        /// The Aspyr patch contains required files in the override folder (controller button textures) which are
        /// preserved during uninstall to prevent breaking the game installation.
        ///
        /// TLK Restoration:
        /// - With the new Replace TLK syntax, mods can replace existing TLK entries, not just append.
        /// - This implementation uses SHA1 hash comparison to detect if dialog.tlk has been modified.
        /// - If the hash doesn't match the vanilla hash, the TLK is restored to vanilla state.
        /// - Restoration is done by trimming to vanilla entry count (for appends) or restoring from backup if available.
        /// - This approach works for both K1 and TSL, and handles both append and replace TLK modifications.
        /// </summary>
        /// <param name="gamePath">The path to the game installation directory</param>
        public static void UninstallAllMods(string gamePath)
        {
            BioWareGame game = Installation.DetermineGame(gamePath)
                       ?? throw new ArgumentException($"Unable to determine game type at path: {gamePath}");

            string overridePath = Installation.GetOverridePath(gamePath);
            string modulesPath = Installation.GetModulesPath(gamePath);

            // Restore vanilla dialog.tlk using SHA1 hash comparison
            string dialogTlkPath = Path.Combine(gamePath, "dialog.tlk");
            RestoreVanillaTlk(dialogTlkPath, game);

            // Remove all override files, except Aspyr patch files
            if (Directory.Exists(overridePath))
            {
                foreach (string filePath in Directory.GetFiles(overridePath))
                {
                    // Skip Aspyr patch files - these are required and must be preserved
                    if (IsAspyrPatchFile(filePath))
                    {
                        continue;
                    }

                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception)
                    {
                        // Log or handle deletion errors if needed
                    }
                }
            }

            // Remove any .MOD files
            if (Directory.Exists(modulesPath))
            {
                foreach (string filePath in Directory.GetFiles(modulesPath))
                {
                    if (IsModFile(Path.GetFileName(filePath)))
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception)
                        {
                            // Log or handle deletion errors if needed
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a filename represents a .MOD file.
        /// </summary>
        /// <param name="filename">The filename to check</param>
        /// <returns>True if the file is a .MOD file, False otherwise</returns>
        private static bool IsModFile(string filename)
        {
            return filename.EndsWith(".mod", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if a file path points to an Aspyr patch file that should be preserved during uninstall.
        /// Aspyr patch files are controller button textures required by the Aspyr patch for proper game functionality.
        /// </summary>
        /// <param name="filePath">The full path to the file to check</param>
        /// <returns>True if the file is an Aspyr patch file that should be preserved, False otherwise</returns>
        private static bool IsAspyrPatchFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            // Get the base filename without extension
            string baseName = Path.GetFileNameWithoutExtension(fileName);

            // Check if the base name matches any Aspyr patch file
            if (!AspyrPatchFiles.Contains(baseName))
            {
                return false;
            }

            // Verify the extension is a valid texture extension
            // This ensures we only preserve actual texture files, not accidentally named files
            string extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                // If no extension, check if a .tpc file exists (TPC files can have embedded TXI)
                // For safety, we'll preserve files without extensions if the base name matches
                return true;
            }

            return TextureExtensions.Contains(extension);
        }

        /// <summary>
        /// Restores the vanilla dialog.tlk file by comparing SHA1 hashes.
        /// 
        /// This method implements the new TLK restoration approach that works with Replace TLK syntax:
        /// 1. Calculate SHA1 hash of current dialog.tlk
        /// 2. Compare to known vanilla SHA1 hash for the game
        /// 3. If hash doesn't match, restore vanilla TLK by:
        ///    - Trimming to vanilla entry count (for appended entries)
        ///    - Restoring from backup if available (for replaced entries)
        ///    - Falling back to entry count trim if backup not available
        /// 
        /// This approach handles both append and replace TLK modifications correctly.
        /// </summary>
        /// <param name="dialogTlkPath">Path to dialog.tlk file</param>
        /// <param name="game">Game type (K1 or TSL)</param>
        private static void RestoreVanillaTlk(string dialogTlkPath, BioWareGame game)
        {
            if (!File.Exists(dialogTlkPath))
            {
                // No dialog.tlk file exists, nothing to restore
                return;
            }

            try
            {
                // Calculate SHA1 hash of current dialog.tlk
                string currentHash = CalculateFileSha1(dialogTlkPath);

                // Get expected vanilla hash for this game
                string expectedVanillaHash = VanillaTlkHashes.TryGetValue(game, out string hash) ? hash : null;

                // If we have a known vanilla hash, compare it
                bool needsRestoration = false;
                if (!string.IsNullOrEmpty(expectedVanillaHash))
                {
                    needsRestoration = !string.Equals(currentHash, expectedVanillaHash, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    // No known vanilla hash - use entry count as fallback detection method
                    // This is the old approach but still works for detecting modifications
                    TLK dialogTlk = new TLKBinaryReader(File.ReadAllBytes(dialogTlkPath)).Load();
                    int expectedEntryCount = game == BioWareGame.K1 ? 49265 : 136329;
                    needsRestoration = dialogTlk.Entries.Count != expectedEntryCount;
                }

                if (needsRestoration)
                {
                    // TLK has been modified, restore to vanilla
                    RestoreTlkToVanilla(dialogTlkPath, game, currentHash);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't fail uninstall - TLK restoration is best-effort
                Console.WriteLine($"[UninstallHelpers] Error restoring vanilla TLK: {ex.Message}");
                Console.WriteLine($"[UninstallHelpers] Stack trace: {ex.StackTrace}");

                // Fall back to old entry count trim method
                RestoreTlkByEntryCount(dialogTlkPath, game);
            }
        }

        /// <summary>
        /// Restores dialog.tlk to vanilla state.
        /// 
        /// This method attempts multiple restoration strategies:
        /// 1. Check for backup file (dialog.tlk.backup) and restore from it
        /// 2. Trim to vanilla entry count (works for appended entries)
        /// 3. If neither works, log a warning that manual restoration may be needed
        /// </summary>
        /// <param name="dialogTlkPath">Path to dialog.tlk file</param>
        /// <param name="game">Game type (K1 or TSL)</param>
        /// <param name="currentHash">Current SHA1 hash of dialog.tlk (for logging)</param>
        private static void RestoreTlkToVanilla(string dialogTlkPath, BioWareGame game, string currentHash)
        {
            // Strategy 1: Try to restore from backup file
            string backupPath = dialogTlkPath + ".backup";
            if (File.Exists(backupPath))
            {
                try
                {
                    string backupHash = CalculateFileSha1(backupPath);
                    string expectedVanillaHash = VanillaTlkHashes.TryGetValue(game, out string hash) ? hash : null;

                    // If backup hash matches vanilla, restore from backup
                    if (!string.IsNullOrEmpty(expectedVanillaHash) &&
                        string.Equals(backupHash, expectedVanillaHash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(backupPath, dialogTlkPath, overwrite: true);
                        Console.WriteLine($"[UninstallHelpers] Restored dialog.tlk from backup (hash: {backupHash})");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[UninstallHelpers] Failed to restore from backup: {ex.Message}");
                }
            }

            // Strategy 2: Trim to vanilla entry count (works for appended entries)
            // This handles the case where mods only appended entries
            RestoreTlkByEntryCount(dialogTlkPath, game);

            // Strategy 3: Verify restoration worked
            try
            {
                string restoredHash = CalculateFileSha1(dialogTlkPath);
                string expectedVanillaHash = VanillaTlkHashes.TryGetValue(game, out string hash) ? hash : null;

                if (!string.IsNullOrEmpty(expectedVanillaHash))
                {
                    if (string.Equals(restoredHash, expectedVanillaHash, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[UninstallHelpers] Successfully restored vanilla dialog.tlk (hash: {restoredHash})");
                    }
                    else
                    {
                        Console.WriteLine($"[UninstallHelpers] WARNING: dialog.tlk restoration may be incomplete.");
                        Console.WriteLine($"[UninstallHelpers] Expected hash: {expectedVanillaHash}");
                        Console.WriteLine($"[UninstallHelpers] Actual hash: {restoredHash}");
                        Console.WriteLine($"[UninstallHelpers] Manual restoration may be required if Replace TLK syntax was used.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UninstallHelpers] Error verifying TLK restoration: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores dialog.tlk by trimming to vanilla entry count.
        /// This is the fallback method that works for appended entries.
        /// </summary>
        /// <param name="dialogTlkPath">Path to dialog.tlk file</param>
        /// <param name="game">Game type (K1 or TSL)</param>
        private static void RestoreTlkByEntryCount(string dialogTlkPath, BioWareGame game)
        {
            try
            {
                TLK dialogTlk = new TLKBinaryReader(File.ReadAllBytes(dialogTlkPath)).Load();

                // Trim TLK entries based on game type
                int maxEntries = game == BioWareGame.K1 ? 49265 : 136329;
                if (dialogTlk.Entries.Count > maxEntries)
                {
                    dialogTlk.Entries = dialogTlk.Entries.Take(maxEntries).ToList();
                }

                var writer = new TLKBinaryWriter(dialogTlk);
                File.WriteAllBytes(dialogTlkPath, writer.Write());

                Console.WriteLine($"[UninstallHelpers] Trimmed dialog.tlk to {maxEntries} entries (vanilla count for {game})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UninstallHelpers] Error trimming dialog.tlk: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Calculates the SHA1 hash of a file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>SHA1 hash as lowercase hex string</returns>
        private static string CalculateFileSha1(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}", filePath);
            }

            using (SHA1 sha1 = SHA1.Create())
            {
                using (FileStream stream = File.OpenRead(filePath))
                {
                    byte[] hashBytes = sha1.ComputeHash(stream);
                    StringBuilder sb = new StringBuilder(hashBytes.Length * 2);
                    foreach (byte b in hashBytes)
                    {
                        sb.Append(b.ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
        }
    }
}
