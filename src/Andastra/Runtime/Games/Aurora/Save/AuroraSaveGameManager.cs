using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Games.Common.Save;
using Andastra.Runtime.Core.Entities;

namespace Andastra.Runtime.Games.Aurora.Save
{
    /// <summary>
    /// Aurora Engine (Neverwinter Nights) save game manager implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Save Game Manager:
    /// - Inherits from BaseSaveGameManager for common save directory naming (shared with Odyssey)
    /// - Aurora-specific: Different save file format than Odyssey (not ERF-based)
    /// - Based on nwmain.exe save game system
    ///
    /// Engine-Specific Details (Aurora):
    /// - Save file format: Different from Odyssey (not ERF archive)
    /// - Uses format string "SAVES:%06d - %s" @ 0x140dfd418 (nwmain.exe)
    /// - Function @ 0x14056ab4e constructs save paths using the format string
    /// - Save directory naming: Same "%06d - %s" format as Odyssey (common functionality)
    ///
    /// Common Functionality (from BaseSaveGameManager):
    /// - Save directory naming: "%06d - %s" format (shared with Odyssey engine)
    /// - Save number auto-generation and parsing
    /// - Directory name formatting and parsing
    ///
    /// Implementation Status:
    /// - ✅ Save directory creation and naming (inherited from BaseSaveGameManager)
    /// - ✅ game.git GFF file creation and loading (Game Instance Template)
    /// - ✅ module_uuid.txt creation and loading (Module UUID)
    /// - ✅ nwsync.txt and nwsyncad.txt creation (NWN sync data, empty for single-player)
    /// - ⚠️ TODO: Full AreaState to GIT conversion (currently creates basic GIT structure)
    /// - ⚠️ TODO: GAM file support for game state (party, globals, etc.)
    /// - ⚠️ TODO: Full GIT to AreaState conversion (currently creates basic AreaState)
    /// </remarks>
    public class AuroraSaveGameManager : BaseSaveGameManager
    {
        private readonly IGameResourceProvider _resourceProvider;

        public AuroraSaveGameManager(IGameResourceProvider resourceProvider, string savesDirectory)
            : base(savesDirectory)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
        }

        /// <summary>
        /// Saves the current game state to a save file.
        /// </summary>
        /// <remarks>
        /// Aurora-specific implementation:
        /// - Aurora saves use GFF files directly in a directory (not ERF archives like Odyssey)
        /// - Save directory structure:
        ///   - game.git: Game Instance Template (GFF) containing area instance data
        ///   - module_uuid.txt: Module UUID
        ///   - nwsync.txt: NWN sync data
        ///   - nwsyncad.txt: NWN sync additional data
        /// - Based on nwmain.exe: SaveGame @ 0x14056a9b0, SaveGIT @ 0x140365db0
        /// - Format string "SAVES:%06d - %s" @ 0x140dfd418 (nwmain.exe)
        /// </remarks>
        public override async Task<bool> SaveGameAsync(SaveGameData saveData, string saveName, CancellationToken ct = default)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException("saveData");
            }

            if (string.IsNullOrEmpty(saveName))
            {
                throw new ArgumentException("Save name cannot be null or empty", "saveName");
            }

            try
            {
                // Auto-generate save number if not set (0 or negative)
                // Uses common logic from BaseSaveGameManager
                if (saveData.SaveNumber <= 0)
                {
                    saveData.SaveNumber = GetNextSaveNumber();
                }

                // Create save directory using common format (shared with Odyssey)
                // Based on nwmain.exe: Function @ 0x14056ab4e uses format string "SAVES:%06d - %s" @ 0x140dfd418
                string formattedSaveName = FormatSaveDirectoryName(saveData.SaveNumber, saveName);
                string saveDir = Path.Combine(_savesDirectory, formattedSaveName);
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }

                // Save game.git - Game Instance Template (GFF)
                // Based on nwmain.exe: SaveGIT @ 0x140365db0
                // GIT files contain area instance data (creatures, doors, placeables, triggers, waypoints, etc.)
                if (saveData.AreaStates != null && saveData.AreaStates.Count > 0)
                {
                    // For now, we'll create a basic GIT structure
                    // TODO: Full GIT implementation requires converting AreaState to GIT format
                    // This is a placeholder that creates a valid GIT file structure
                    var git = new GIT();
                    
                    // Set area properties from first area state (if available)
                    var firstArea = saveData.AreaStates.Values.FirstOrDefault();
                    if (firstArea != null)
                    {
                        // Area properties would be set here based on AreaState
                        // For now, we use defaults
                    }
                    
                    // Write game.git GFF file
                    string gameGitPath = Path.Combine(saveDir, "game.git");
                    GFF gitGff = GITHelpers.DismantleGit(git, Game.K1); // K1 format is closer to Aurora
                    gitGff.Content = GFFContent.GIT;
                    GFFAuto.WriteGff(gitGff, gameGitPath);
                }
                else
                {
                    // Create empty GIT file if no area states
                    var emptyGit = new GIT();
                    string gameGitPath = Path.Combine(saveDir, "game.git");
                    GFF gitGff = GITHelpers.DismantleGit(emptyGit, Game.K1);
                    gitGff.Content = GFFContent.GIT;
                    GFFAuto.WriteGff(gitGff, gameGitPath);
                }

                // Save module_uuid.txt - Module UUID
                // Based on nwmain.exe: Format string "%s%s%06d - %s%smodule_uuid.txt" @ 0x140dfd570
                string moduleUuidPath = Path.Combine(saveDir, "module_uuid.txt");
                string moduleUuid = saveData.CurrentModule ?? "default_module";
                File.WriteAllText(moduleUuidPath, moduleUuid);

                // Save nwsync.txt - NWN sync data (can be empty for single-player saves)
                // Based on nwmain.exe: Format string "%s%s%06d - %s%snwsync.txt" @ 0x140dfd590
                string nwsyncPath = Path.Combine(saveDir, "nwsync.txt");
                File.WriteAllText(nwsyncPath, ""); // Empty for single-player saves

                // Save nwsyncad.txt - NWN sync additional data (can be empty for single-player saves)
                // Based on nwmain.exe: Format string "%s%s%06d - %s%snwsyncad.txt" @ 0x140dfd5b0
                string nwsyncadPath = Path.Combine(saveDir, "nwsyncad.txt");
                File.WriteAllText(nwsyncadPath, ""); // Empty for single-player saves

                // TODO: Save game state (GAM format or custom GFF)
                // Aurora may use a GAM file for game state (party, globals, etc.)
                // This would require converting SaveGameData to GAM format

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuroraSaveGameManager] Error saving game: {ex.Message}");
                return await Task.FromResult(false);
            }
        }

        /// <summary>
        /// Loads a save game from a save file.
        /// </summary>
        /// <remarks>
        /// Aurora-specific implementation:
        /// - Loads GFF files directly from save directory (not ERF archives)
        /// - Reads game.git, module_uuid.txt, and reconstructs SaveGameData
        /// - Based on nwmain.exe: LoadGame @ 0x140565890, LoadGIT @ CNWSArea::LoadGIT
        /// </remarks>
        public override async Task<SaveGameData> LoadGameAsync(string saveName, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                throw new ArgumentException("Save name cannot be null or empty", "saveName");
            }

            try
            {
                // Try formatted name first (original engine format)
                // Uses common parsing logic from BaseSaveGameManager
                string saveDir = Path.Combine(_savesDirectory, saveName);
                
                // If formatted name doesn't exist, try to find it by parsing existing directories
                // This handles backward compatibility with saves created before this fix
                if (!Directory.Exists(saveDir))
                {
                    // Try to find save by matching the name part (after " - ")
                    string namePart = ParseSaveNameFromDirectory(saveName);
                    if (!string.IsNullOrEmpty(namePart))
                    {
                        foreach (string dir in Directory.GetDirectories(_savesDirectory))
                        {
                            string dirName = Path.GetFileName(dir);
                            string parsedName = ParseSaveNameFromDirectory(dirName);
                            if (parsedName == namePart || dirName == saveName)
                            {
                                saveDir = dir;
                                if (Directory.Exists(saveDir))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!Directory.Exists(saveDir))
                {
                    Console.WriteLine($"[AuroraSaveGameManager] Save directory not found: {saveDir}");
                    return await Task.FromResult<SaveGameData>(null);
                }

                var saveData = new SaveGameData();

                // Parse save number and name from directory name
                string dirName = Path.GetFileName(saveDir);
                saveData.SaveNumber = ParseSaveNumberFromDirectory(dirName);
                saveData.Name = ParseSaveNameFromDirectory(dirName);
                if (string.IsNullOrEmpty(saveData.Name))
                {
                    saveData.Name = dirName;
                }

                // Load module_uuid.txt - Module UUID
                string moduleUuidPath = Path.Combine(saveDir, "module_uuid.txt");
                if (File.Exists(moduleUuidPath))
                {
                    saveData.CurrentModule = File.ReadAllText(moduleUuidPath).Trim();
                }

                // Load game.git - Game Instance Template (GFF)
                // Based on nwmain.exe: LoadGIT @ CNWSArea::LoadGIT
                string gameGitPath = Path.Combine(saveDir, "game.git");
                if (File.Exists(gameGitPath))
                {
                    try
                    {
                        GFF gitGff = GFFAuto.ReadGff(gameGitPath);
                        GIT git = GITHelpers.ConstructGit(gitGff);
                        
                        // Convert GIT to AreaState (simplified - full conversion would require more work)
                        // For now, we create a basic area state
                        if (!string.IsNullOrEmpty(saveData.CurrentModule))
                        {
                            var areaState = new AreaState
                            {
                                AreaResRef = saveData.CurrentModule
                            };
                            saveData.AreaStates = new Dictionary<string, AreaState>
                            {
                                { saveData.CurrentModule, areaState }
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AuroraSaveGameManager] Error loading game.git: {ex.Message}");
                    }
                }

                // Set save time from directory modification time
                saveData.SaveTime = Directory.GetLastWriteTime(saveDir);

                // TODO: Load game state from GAM file or other GFF files
                // TODO: Load global variables, party state, etc.

                return await Task.FromResult(saveData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AuroraSaveGameManager] Error loading game: {ex.Message}");
                return await Task.FromResult<SaveGameData>(null);
            }
        }

        /// <summary>
        /// Lists all available save games.
        /// </summary>
        /// <remarks>
        /// Aurora-specific implementation:
        /// - Uses common directory name parsing from BaseSaveGameManager
        /// - Attempts to load metadata from game.git and module_uuid.txt
        /// - Based on nwmain.exe: Format "%06d - %s" (6-digit number - name)
        /// </remarks>
        public override IEnumerable<SaveGameInfo> ListSaves()
        {
            if (!Directory.Exists(_savesDirectory))
            {
                yield break;
            }

            foreach (string saveDir in Directory.GetDirectories(_savesDirectory))
            {
                string saveName = Path.GetFileName(saveDir);

                // Parse save number and name from directory name using common logic
                // Based on nwmain.exe: Format "%06d - %s" (6-digit number - name)
                // Uses common parsing methods from BaseSaveGameManager (shared with Odyssey)
                int saveNumber = ParseSaveNumberFromDirectory(saveName);
                string displayName = ParseSaveNameFromDirectory(saveName);
                if (string.IsNullOrEmpty(displayName))
                {
                    // Fallback: use directory name if parsing fails (backward compatibility)
                    displayName = saveName;
                }

                var info = new SaveGameInfo
                {
                    Name = displayName,
                    SaveTime = Directory.GetLastWriteTime(saveDir),
                    SavePath = saveDir,
                    SlotIndex = saveNumber
                };

                // Try to load additional metadata from module_uuid.txt
                string moduleUuidPath = Path.Combine(saveDir, "module_uuid.txt");
                if (File.Exists(moduleUuidPath))
                {
                    try
                    {
                        string moduleName = File.ReadAllText(moduleUuidPath).Trim();
                        if (!string.IsNullOrEmpty(moduleName))
                        {
                            info.ModuleName = moduleName;
                        }
                    }
                    catch
                    {
                        // Ignore errors reading module_uuid.txt
                    }
                }

                // TODO: Load additional metadata from game.git or GAM files
                // TODO: Extract player name, play time, etc. from save files

                yield return info;
            }
        }
    }
}

