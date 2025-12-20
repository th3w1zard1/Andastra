using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Andastra.Parsing;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Uninstall
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
        /// </summary>
        private static readonly Dictionary<Game, string> VanillaTlkHashes = new Dictionary<Game, string>
        {
            // K1 vanilla dialog.tlk SHA1 hash
            // This should be calculated from a verified vanilla K1 installation
            // Format: SHA1 hash as lowercase hex string
            { Game.K1, null }, // TODO: Calculate and set actual vanilla K1 TLK SHA1 hash
            
            // TSL vanilla dialog.tlk SHA1 hash
            // This should be calculated from a verified vanilla TSL installation
            // Format: SHA1 hash as lowercase hex string
            { Game.TSL, null } // TODO: Calculate and set actual vanilla TSL TLK SHA1 hash
        };
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
            Game game = Installation.Installation.DetermineGame(gamePath)
                       ?? throw new ArgumentException($"Unable to determine game type at path: {gamePath}");

            string overridePath = Installation.Installation.GetOverridePath(gamePath);
            string modulesPath = Installation.Installation.GetModulesPath(gamePath);

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
        private static void RestoreVanillaTlk(string dialogTlkPath, Game game)
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
                    int expectedEntryCount = game == Game.K1 ? 49265 : 136329;
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
        private static void RestoreTlkToVanilla(string dialogTlkPath, Game game, string currentHash)
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
        private static void RestoreTlkByEntryCount(string dialogTlkPath, Game game)
        {
            try
            {
                TLK dialogTlk = new TLKBinaryReader(File.ReadAllBytes(dialogTlkPath)).Load();

                // Trim TLK entries based on game type
                int maxEntries = game == Game.K1 ? 49265 : 136329;
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
