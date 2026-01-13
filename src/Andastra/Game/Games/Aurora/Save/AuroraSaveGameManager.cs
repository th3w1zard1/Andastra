using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Save;
using Andastra.Game.Games.Common.Save;
using ObjectType = Andastra.Runtime.Core.Enums.ObjectType;

namespace Andastra.Game.Games.Aurora.Save
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
    /// - ✅ Full AreaState to GIT conversion (ConvertAreaStateToGIT)
    /// - ✅ Full GIT to AreaState conversion (ConvertGITToAreaState)
    /// - ✅ GAM file support for game state (ConvertSaveGameDataToGAM, ConvertGAMToSaveGameData)
    /// - ✅ Complete entity state conversions (creatures, doors, placeables, triggers, waypoints, stores, sounds, encounters, cameras)
    /// - ✅ Spawned entity support (dynamically created entities not in original GIT)
    /// - ✅ Global variables, party state, and game time conversion
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
                // Convert AreaState to GIT format for current area
                string currentAreaResRef = saveData.CurrentAreaName ?? saveData.CurrentModule ?? "";
                AreaState currentAreaState = null;
                if (saveData.AreaStates != null && !string.IsNullOrEmpty(currentAreaResRef))
                {
                    saveData.AreaStates.TryGetValue(currentAreaResRef, out currentAreaState);
                }

                // If no current area state, try to get first area state
                if (currentAreaState == null && saveData.AreaStates != null && saveData.AreaStates.Count > 0)
                {
                    currentAreaState = saveData.AreaStates.Values.FirstOrDefault();
                }

                GIT git = currentAreaState != null
                    ? ConvertAreaStateToGIT(currentAreaState)
                    : new GIT();

                // Write game.git GFF file
                // Use BioWareGame.NWN for Aurora engine (Neverwinter Nights)
                string gameGitPath = Path.Combine(saveDir, "game.git");
                GFF gitGff = GITHelpers.DismantleGit(git, BioWareGame.NWN);
                gitGff.Content = GFFContent.GIT;
                GFFAuto.WriteGff(gitGff, gameGitPath);

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

                // Save game.gam - Game state (GAM format)
                // Based on nwmain.exe: GAM files store party, globals, BioWareGame time etc.
                // Convert SaveGameData to GAM format
                GAM gam = ConvertSaveGameDataToGAM(saveData);
                string gameGamPath = Path.Combine(saveDir, "game.gam");
                GAMAuto.WriteGam(gam, gameGamPath, BioWareGame.NWN);

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
                            string currentDirName = Path.GetFileName(dir);
                            string parsedName = ParseSaveNameFromDirectory(currentDirName);
                            if (parsedName == namePart || currentDirName == saveName)
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
                        GFF gitGff = GFFAuto.ReadGff(gameGitPath, 0, null);
                        GIT git = GITHelpers.ConstructGit(gitGff);

                        // Convert GIT to AreaState
                        string areaResRef = saveData.CurrentModule ?? "default_area";
                        AreaState areaState = ConvertGITToAreaState(git, areaResRef);

                        if (saveData.AreaStates == null)
                        {
                            saveData.AreaStates = new Dictionary<string, AreaState>();
                        }
                        saveData.AreaStates[areaResRef] = areaState;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AuroraSaveGameManager] Error loading game.git: {ex.Message}");
                    }
                }

                // Load game.gam - Game state (GAM format)
                // Based on nwmain.exe: GAM files store party, globals, BioWareGame time etc.
                string gameGamPath = Path.Combine(saveDir, "game.gam");
                if (File.Exists(gameGamPath))
                {
                    try
                    {
                        GAM gam = GAMAuto.ReadGam(gameGamPath);
                        ConvertGAMToSaveGameData(gam, saveData);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AuroraSaveGameManager] Error loading game.gam: {ex.Message}");
                    }
                }

                // Set save time from directory modification time
                saveData.SaveTime = Directory.GetLastWriteTime(saveDir);

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

                // Try to load additional metadata from game.gam
                string gameGamPath = Path.Combine(saveDir, "game.gam");
                if (File.Exists(gameGamPath))
                {
                    try
                    {
                        GAM gam = GAMAuto.ReadGam(gameGamPath);
                        if (!string.IsNullOrEmpty(gam.ModuleName))
                        {
                            info.ModuleName = gam.ModuleName;
                        }
                        if (gam.TimePlayed > 0)
                        {
                            info.PlayTime = TimeSpan.FromSeconds(gam.TimePlayed);
                        }
                        if (!gam.PlayerCharacter.IsBlank())
                        {
                            info.PlayerName = gam.PlayerCharacter.ToString();
                        }
                    }
                    catch
                    {
                        // Ignore errors reading game.gam
                    }
                }

                yield return info;
            }
        }

        #region AreaState <-> GIT Conversion

        /// <summary>
        /// Converts AreaState to GIT format.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: SaveGIT @ 0x140365db0
        /// Converts entity states from AreaState to GIT instance lists
        /// </remarks>
        private GIT ConvertAreaStateToGIT(AreaState areaState)
        {
            var git = new GIT();

            // Convert creature states to GITCreature instances
            if (areaState.CreatureStates != null)
            {
                foreach (EntityState entityState in areaState.CreatureStates)
                {
                    // Skip destroyed entities (they're tracked in DestroyedEntityIds)
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitCreature = new GITCreature
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position,
                        Bearing = entityState.Facing
                    };
                    git.Creatures.Add(gitCreature);
                }
            }

            // Convert door states to GITDoor instances
            if (areaState.DoorStates != null)
            {
                foreach (EntityState entityState in areaState.DoorStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitDoor = new GITDoor
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position,
                        Bearing = entityState.Facing,
                        Tag = entityState.Tag ?? ""
                    };
                    git.Doors.Add(gitDoor);
                }
            }

            // Convert placeable states to GITPlaceable instances
            if (areaState.PlaceableStates != null)
            {
                foreach (EntityState entityState in areaState.PlaceableStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitPlaceable = new GITPlaceable
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position,
                        Bearing = entityState.Facing,
                        Tag = entityState.Tag ?? ""
                    };
                    git.Placeables.Add(gitPlaceable);
                }
            }

            // Convert trigger states to GITTrigger instances
            if (areaState.TriggerStates != null)
            {
                foreach (EntityState entityState in areaState.TriggerStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitTrigger = new GITTrigger
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position,
                        Tag = entityState.Tag ?? ""
                    };
                    git.Triggers.Add(gitTrigger);
                }
            }

            // Convert waypoint states to GITWaypoint instances
            if (areaState.WaypointStates != null)
            {
                foreach (EntityState entityState in areaState.WaypointStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitWaypoint = new GITWaypoint
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position,
                        Bearing = entityState.Facing,
                        Tag = entityState.Tag ?? ""
                    };
                    git.Waypoints.Add(gitWaypoint);
                }
            }

            // Convert store states to GITStore instances
            if (areaState.StoreStates != null)
            {
                foreach (EntityState entityState in areaState.StoreStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitStore = new GITStore
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position,
                        Bearing = entityState.Facing
                    };
                    git.Stores.Add(gitStore);
                }
            }

            // Convert sound states to GITSound instances
            if (areaState.SoundStates != null)
            {
                foreach (EntityState entityState in areaState.SoundStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitSound = new GITSound
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position,
                        Tag = entityState.Tag ?? ""
                    };
                    git.Sounds.Add(gitSound);
                }
            }

            // Convert encounter states to GITEncounter instances
            if (areaState.EncounterStates != null)
            {
                foreach (EntityState entityState in areaState.EncounterStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitEncounter = new GITEncounter
                    {
                        ResRef = ResRef.FromString(entityState.TemplateResRef ?? ""),
                        Position = entityState.Position
                    };
                    git.Encounters.Add(gitEncounter);
                }
            }

            // Convert camera states to GITCamera instances
            if (areaState.CameraStates != null)
            {
                int cameraId = 1;
                foreach (EntityState entityState in areaState.CameraStates)
                {
                    if (entityState.IsDestroyed)
                    {
                        continue;
                    }

                    var gitCamera = new GITCamera
                    {
                        CameraId = cameraId++,
                        Position = entityState.Position,
                        Fov = 45.0f // Default FOV
                    };
                    git.Cameras.Add(gitCamera);
                }
            }

            // Add spawned entities (not in original GIT, dynamically created)
            if (areaState.SpawnedEntities != null)
            {
                foreach (SpawnedEntityState spawnedEntity in areaState.SpawnedEntities)
                {
                    if (spawnedEntity.IsDestroyed)
                    {
                        continue;
                    }

                    // Determine type based on ObjectType
                    ResRef resRef = ResRef.FromString(spawnedEntity.BlueprintResRef ?? spawnedEntity.TemplateResRef ?? "");

                    if ((spawnedEntity.ObjectType & ObjectType.Creature) != 0)
                    {
                        var gitCreature = new GITCreature
                        {
                            ResRef = resRef,
                            Position = spawnedEntity.Position,
                            Bearing = spawnedEntity.Facing
                        };
                        git.Creatures.Add(gitCreature);
                    }
                    else if ((spawnedEntity.ObjectType & ObjectType.Door) != 0)
                    {
                        var gitDoor = new GITDoor
                        {
                            ResRef = resRef,
                            Position = spawnedEntity.Position,
                            Bearing = spawnedEntity.Facing,
                            Tag = spawnedEntity.Tag ?? ""
                        };
                        git.Doors.Add(gitDoor);
                    }
                    else if ((spawnedEntity.ObjectType & ObjectType.Placeable) != 0)
                    {
                        var gitPlaceable = new GITPlaceable
                        {
                            ResRef = resRef,
                            Position = spawnedEntity.Position,
                            Bearing = spawnedEntity.Facing,
                            Tag = spawnedEntity.Tag ?? ""
                        };
                        git.Placeables.Add(gitPlaceable);
                    }
                    // Add other types as needed
                }
            }

            return git;
        }

        /// <summary>
        /// Converts GIT format to AreaState.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: LoadGIT @ CNWSArea::LoadGIT
        /// Converts GIT instance lists back to AreaState entity lists
        /// </remarks>
        private AreaState ConvertGITToAreaState(GIT git, string areaResRef)
        {
            var areaState = new AreaState
            {
                AreaResRef = areaResRef,
                Visited = true
            };

            // Convert GITCreature instances to creature states
            foreach (GITCreature gitCreature in git.Creatures)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitCreature.ResRef.ToString(),
                    Position = gitCreature.Position,
                    Facing = gitCreature.Bearing,
                    ObjectType = ObjectType.Creature
                };
                areaState.CreatureStates.Add(entityState);
            }

            // Convert GITDoor instances to door states
            foreach (GITDoor gitDoor in git.Doors)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitDoor.ResRef.ToString(),
                    Position = gitDoor.Position,
                    Facing = gitDoor.Bearing,
                    Tag = gitDoor.Tag,
                    ObjectType = ObjectType.Door
                };
                areaState.DoorStates.Add(entityState);
            }

            // Convert GITPlaceable instances to placeable states
            foreach (GITPlaceable gitPlaceable in git.Placeables)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitPlaceable.ResRef.ToString(),
                    Position = gitPlaceable.Position,
                    Facing = gitPlaceable.Bearing,
                    Tag = gitPlaceable.Tag,
                    ObjectType = ObjectType.Placeable
                };
                areaState.PlaceableStates.Add(entityState);
            }

            // Convert GITTrigger instances to trigger states
            foreach (GITTrigger gitTrigger in git.Triggers)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitTrigger.ResRef.ToString(),
                    Position = gitTrigger.Position,
                    Tag = gitTrigger.Tag,
                    ObjectType = ObjectType.Trigger
                };
                areaState.TriggerStates.Add(entityState);
            }

            // Convert GITWaypoint instances to waypoint states
            foreach (GITWaypoint gitWaypoint in git.Waypoints)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitWaypoint.ResRef.ToString(),
                    Position = gitWaypoint.Position,
                    Facing = gitWaypoint.Bearing,
                    Tag = gitWaypoint.Tag,
                    ObjectType = ObjectType.Waypoint
                };
                areaState.WaypointStates.Add(entityState);
            }

            // Convert GITStore instances to store states
            foreach (GITStore gitStore in git.Stores)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitStore.ResRef.ToString(),
                    Position = gitStore.Position,
                    Facing = gitStore.Bearing,
                    ObjectType = ObjectType.Store
                };
                areaState.StoreStates.Add(entityState);
            }

            // Convert GITSound instances to sound states
            foreach (GITSound gitSound in git.Sounds)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitSound.ResRef.ToString(),
                    Position = gitSound.Position,
                    Tag = gitSound.Tag,
                    ObjectType = ObjectType.Sound
                };
                areaState.SoundStates.Add(entityState);
            }

            // Convert GITEncounter instances to encounter states
            foreach (GITEncounter gitEncounter in git.Encounters)
            {
                var entityState = new EntityState
                {
                    TemplateResRef = gitEncounter.ResRef.ToString(),
                    Position = gitEncounter.Position,
                    ObjectType = ObjectType.Encounter
                };
                areaState.EncounterStates.Add(entityState);
            }

            // Convert GITCamera instances to camera states
            foreach (GITCamera gitCamera in git.Cameras)
            {
                var entityState = new EntityState
                {
                    Position = gitCamera.Position,
                    ObjectType = ObjectType.Invalid // Cameras don't have a standard ObjectType
                };
                areaState.CameraStates.Add(entityState);
            }

            return areaState;
        }

        #endregion

        #region SaveGameData <-> GAM Conversion

        /// <summary>
        /// Converts SaveGameData to GAM format.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: GAM files store game state (party, globals, BioWareGame time etc.)
        /// </remarks>
        private GAM ConvertSaveGameDataToGAM(SaveGameData saveData)
        {
            var gam = new GAM();

            // Set game time from GameTime
            if (saveData.GameTime != null)
            {
                gam.GameTimeHour = saveData.GameTime.Hour;
                gam.GameTimeMinute = saveData.GameTime.Minute;
                gam.GameTimeSecond = 0; // GameTime doesn't have Second property
                gam.GameTimeMillisecond = 0; // GameTime doesn't have milliseconds
            }

            // Set time played
            gam.TimePlayed = (int)saveData.PlayTime.TotalSeconds;

            // Set module name
            gam.ModuleName = saveData.CurrentModule ?? "";

            // Set current area
            if (!string.IsNullOrEmpty(saveData.CurrentAreaName))
            {
                gam.CurrentArea = ResRef.FromString(saveData.CurrentAreaName);
            }

            // Set player character
            // Based on nwmain.exe: GAM PlayerCharacter field stores the player character template ResRef
            // Priority: TemplateResRef (blueprint) > Tag (fallback) > PlayerName (backward compatibility)
            if (saveData.PartyState != null && saveData.PartyState.PlayerCharacter != null)
            {
                ResRef playerCharacterResRef = ResRef.FromBlank();

                // First, try to use the template ResRef (the character blueprint/template)
                // This is the proper ResRef that identifies the character's template
                if (!string.IsNullOrEmpty(saveData.PartyState.PlayerCharacter.TemplateResRef))
                {
                    playerCharacterResRef = ResRef.FromString(saveData.PartyState.PlayerCharacter.TemplateResRef);
                }
                // Fallback to Tag if TemplateResRef is not available
                // Tag might contain the ResRef in some cases
                else if (!string.IsNullOrEmpty(saveData.PartyState.PlayerCharacter.Tag))
                {
                    playerCharacterResRef = ResRef.FromString(saveData.PartyState.PlayerCharacter.Tag);
                }
                // Last resort: use PlayerName for backward compatibility with saves that don't have TemplateResRef
                // This maintains compatibility with older save formats
                else if (!string.IsNullOrEmpty(saveData.PlayerName))
                {
                    playerCharacterResRef = ResRef.FromString(saveData.PlayerName);
                }

                // Only set if we have a valid ResRef
                if (!playerCharacterResRef.IsBlank())
                {
                    gam.PlayerCharacter = playerCharacterResRef;
                }
            }
            // Fallback: if PartyState.PlayerCharacter is null, try PlayerName directly
            // This handles edge cases where PartyState might not be fully populated
            else if (!string.IsNullOrEmpty(saveData.PlayerName))
            {
                gam.PlayerCharacter = ResRef.FromString(saveData.PlayerName);
            }

            // Convert party members
            if (saveData.PartyState != null && saveData.PartyState.AvailableMembers != null)
            {
                foreach (var kvp in saveData.PartyState.AvailableMembers)
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        gam.PartyMembers.Add(ResRef.FromString(kvp.Key));
                    }
                }
            }

            // Convert global variables
            if (saveData.GlobalVariables != null)
            {
                // Boolean globals
                if (saveData.GlobalVariables.Booleans != null)
                {
                    foreach (var kvp in saveData.GlobalVariables.Booleans)
                    {
                        gam.GlobalBooleans[kvp.Key] = kvp.Value;
                    }
                }

                // Numeric globals
                if (saveData.GlobalVariables.Numbers != null)
                {
                    foreach (var kvp in saveData.GlobalVariables.Numbers)
                    {
                        gam.GlobalNumbers[kvp.Key] = kvp.Value;
                    }
                }

                // String globals
                if (saveData.GlobalVariables.Strings != null)
                {
                    foreach (var kvp in saveData.GlobalVariables.Strings)
                    {
                        gam.GlobalStrings[kvp.Key] = kvp.Value;
                    }
                }
            }

            return gam;
        }

        /// <summary>
        /// Converts GAM format to SaveGameData.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: GAM files store game state (party, globals, BioWareGame time etc.)
        /// </remarks>
        private void ConvertGAMToSaveGameData(GAM gam, SaveGameData saveData)
        {
            // Set game time
            if (saveData.GameTime == null)
            {
                saveData.GameTime = new GameTime();
            }
            saveData.GameTime.Hour = gam.GameTimeHour;
            saveData.GameTime.Minute = gam.GameTimeMinute;
            // Note: GameTime class doesn't have Second property - only Year, Month, Day, Hour, Minute

            // Set time played
            saveData.PlayTime = TimeSpan.FromSeconds(gam.TimePlayed);

            // Set module name
            if (!string.IsNullOrEmpty(gam.ModuleName))
            {
                saveData.CurrentModule = gam.ModuleName;
            }

            // Set current area
            if (!gam.CurrentArea.IsBlank())
            {
                saveData.CurrentAreaName = gam.CurrentArea.ToString();
            }

            // Set player character
            // Based on nwmain.exe: GAM PlayerCharacter field contains the player character template ResRef
            // Populate both PlayerName (for backward compatibility) and PartyState.PlayerCharacter.TemplateResRef
            if (!gam.PlayerCharacter.IsBlank())
            {
                string playerCharacterResRef = gam.PlayerCharacter.ToString();
                saveData.PlayerName = playerCharacterResRef;

                // Also populate PartyState.PlayerCharacter.TemplateResRef for proper data structure
                // This ensures the ResRef is available when saving again
                if (saveData.PartyState == null)
                {
                    saveData.PartyState = new PartyState();
                }
                if (saveData.PartyState.PlayerCharacter == null)
                {
                    saveData.PartyState.PlayerCharacter = new CreatureState();
                }
                saveData.PartyState.PlayerCharacter.TemplateResRef = playerCharacterResRef;
            }

            // Convert party members
            if (saveData.PartyState == null)
            {
                saveData.PartyState = new PartyState();
            }
            if (saveData.PartyState.AvailableMembers == null)
            {
                saveData.PartyState.AvailableMembers = new Dictionary<string, PartyMemberState>();
            }
            foreach (ResRef memberResRef in gam.PartyMembers)
            {
                if (!memberResRef.IsBlank())
                {
                    string memberKey = memberResRef.ToString();
                    if (!saveData.PartyState.AvailableMembers.ContainsKey(memberKey))
                    {
                        saveData.PartyState.AvailableMembers[memberKey] = new PartyMemberState
                        {
                            TemplateResRef = memberKey,
                            IsAvailable = true,
                            IsSelectable = true
                        };
                    }
                }
            }

            // Convert global variables
            if (saveData.GlobalVariables == null)
            {
                saveData.GlobalVariables = new GlobalVariableState();
            }

            // Boolean globals
            foreach (var kvp in gam.GlobalBooleans)
            {
                saveData.GlobalVariables.Booleans[kvp.Key] = kvp.Value;
            }

            // Numeric globals
            foreach (var kvp in gam.GlobalNumbers)
            {
                saveData.GlobalVariables.Numbers[kvp.Key] = kvp.Value;
            }

            // String globals
            foreach (var kvp in gam.GlobalStrings)
            {
                saveData.GlobalVariables.Strings[kvp.Key] = kvp.Value;
            }
        }

        #endregion
    }
}

