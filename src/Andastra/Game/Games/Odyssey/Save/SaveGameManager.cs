
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Save;
using Andastra.Game.Games.Common.Save;

namespace Andastra.Game.Games.Odyssey.Save
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) save game manager implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Save Game Manager:
    /// - Inherits from BaseSaveGameManager for common save directory naming (shared with Aurora)
    /// - Odyssey-specific: ERF archive save format, NFO GFF metadata structure
    /// - Based on swkotor.exe and swkotor2.exe save game systems
    ///
    /// Engine-Specific Details (Odyssey):
    /// - Save file format: ERF archive with "MOD V1.0" signature
    /// - Save function: 0x004eb750 @ 0x004eb750 (swkotor2.exe) creates save game ERF archive
    /// - Load function: 0x00708990 @ 0x00708990 (swkotor2.exe) loads ERF archive
    ///   - Located via string references: "savenfo" @ 0x007be1f0, "SAVEGAME" @ 0x007be28c, "SAVES:" @ 0x007be284
    ///   - "LoadSavegame" @ 0x007bdc90, "SavegameList" @ 0x007bdca0, "GetSavegameList" @ 0x007bdcb0
    ///   - "SAVEGAMENAME" @ 0x007be1a8, "Mod_IsSaveGame" @ 0x007bea48, "BTN_SAVEGAME" @ 0x007d0dbc
    ///   - Constructs save path: "SAVES:\{saveName}\SAVEGAME" using format string "%06d - %s" (save number and name)
    ///   - Creates GAMEINPROGRESS: directory if missing (checks existence via 0x004069c0, creates via 0x00409670 if not found)
    ///   - Loads savegame.sav ERF archive from constructed path (via 0x00629d60, 0x0062a2b0)
    ///   - Extracts savenfo.res (NFO GFF) to TEMP:pifo, reads NFO GFF with "NFO " signature
    ///   - Progress updates: 5% (0x5), 10% (0xa), 15% (0xf), 20% (0x14), 25% (0x19), 30% (0x1e), 35% (0x23), 40% (0x28), 45% (0x2d), 50% (0x32)
    ///   - Loads PARTYTABLE via 0x0057dcd0 @ 0x0057dcd0 (party table deserialization, called at 30% progress)
    ///   - Loads GLOBALVARS via 0x005ac740 @ 0x005ac740 (global variables deserialization, called at 35% progress)
    ///   - Reads AUTOSAVEPARAMS from NFO GFF if present (via 0x00412b30, 0x00708660)
    ///   - Sets module state flags and initializes game session (via 0x004dc470, 0x004dc9e0, 0x004dc9c0)
    ///   - Module state: Sets flags at offset 0x48 in game session object (bit 0x200 = module loaded flag)
    ///   - Final progress: 50% (0x32) when savegame load completes
    /// - 0x004e28c0 @ 0x004e28c0 loads creature list from module state
    ///   - Original implementation: Iterates through "Creature List" GFF list, loads each creature via 0x005226d0
    ///   - Only loads creatures that are not PC (IsPC == 0) and not destroyed (IsDestroyed == 0)
    ///   - Creates creature entities from saved ObjectId and GFF data
    ///   - Saves creature state to GFF structure (position, stats, inventory, scripts, etc.)
    /// - Save file structure: ERF archive containing:
    ///   - savenfo.res (GFF with "NFO " signature): Save metadata (AREANAME, TIMEPLAYED, SAVEGAMENAME, etc.)
    ///   - GLOBALVARS.res (GFF with "GLOB" signature): Global variable state
    ///   - PARTYTABLE.res (GFF with "PT  " signature): Party state
    ///   - [module]_s.rim: Per-module state ERF archive (area states, entity positions, etc.)
    /// - Save location: "SAVES:\{saveName}\savegame.sav" (ERF archive)
    /// - Based on KOTOR save game format documentation in vendor/PyKotor/wiki/
    ///
    /// Common Functionality (from BaseSaveGameManager):
    /// - Save directory naming: "%06d - %s" format (shared with Aurora engine)
    /// - Save number auto-generation and parsing
    /// - Directory name formatting and parsing
    /// </remarks>
    public class OdysseySaveGameManager : BaseSaveGameManager
    {
        private readonly IGameResourceProvider _resourceProvider;

        public OdysseySaveGameManager(IGameResourceProvider resourceProvider, string savesDirectory)
            : base(savesDirectory)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
        }

        /// <summary>
        /// Saves the current game state to a save file.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific implementation:
        /// - Uses ERF archive format with "MOD V1.0" signature
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004eb750 @ 0x004eb750
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
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Save numbers are auto-incremented
                // Original implementation: Scans existing saves to find highest number, then increments
                if (saveData.SaveNumber <= 0)
                {
                    saveData.SaveNumber = GetNextSaveNumber();
                }

                // Create save directory using common format (shared with Aurora)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00708990 @ 0x00708990
                // Original implementation: Constructs path using format "%06d - %s" (save number and name)
                // Path format: "SAVES:\{saveNumber:06d} - {saveName}\SAVEGAME"
                // Located via string reference: "%06d - %s" @ 0x007be298
                // Format: 6-digit zero-padded save number, followed by " - ", followed by save name
                string formattedSaveName = FormatSaveDirectoryName(saveData.SaveNumber, saveName);
                string saveDir = Path.Combine(_savesDirectory, formattedSaveName);
                if (!Directory.Exists(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                }

                // Create ERF archive for save game
                var erf = new ERF(ERFType.MOD, isSave: true);

                // 1. Save metadata (savenfo.res)
                GFF nfoGff = CreateSaveInfoGFF(saveData);
                byte[] nfoData = SerializeGFF(nfoGff);
                erf.SetData("savenfo", ResourceType.GFF, nfoData);

                // 2. Save global variables (GLOBALVARS.res)
                GFF globGff = CreateGlobalVarsGFF(saveData.GlobalVariables);
                byte[] globData = SerializeGFF(globGff);
                erf.SetData("GLOBALVARS", ResourceType.GFF, globData);

                // 3. Save party state (PARTYTABLE.res)
                GFF partyGff = CreatePartyTableGFF(saveData.PartyState);
                byte[] partyData = SerializeGFF(partyGff);
                erf.SetData("PARTYTABLE", ResourceType.GFF, partyData);

                // 4. Save per-module state ([module]_s.rim)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state saved as ERF archive containing area state GFF files
                // Original implementation: Each visited area has its state saved (entity positions, door/placeable states, etc.)
                // CreateModuleStateERF saves all area states to GFF format and packages them in ERF archive
                string moduleRimName = saveData.CurrentModule + "_s";
                ERF moduleRim = CreateModuleStateERF(saveData);
                byte[] moduleRimData = SerializeERF(moduleRim);
                erf.SetData(moduleRimName, ResourceType.ERF, moduleRimData);

                // Write ERF archive to disk
                string saveFilePath = Path.Combine(saveDir, "savegame.sav");
                byte[] erfData = SerializeERF(erf);
                await File.WriteAllBytesAsync(saveFilePath, erfData, ct);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveGameManager] Error saving game: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a save game from a save file.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific implementation:
        /// - Loads ERF archive format with "MOD V1.0" signature
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00708990 @ 0x00708990
        /// - Original implementation: Accepts save name and constructs path using format "%06d - %s"
        /// - Supports both formatted names ("000001 - SaveName") and plain names (for backward compatibility)
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
                string saveDir = Path.Combine(_savesDirectory, saveName);
                string saveFilePath = Path.Combine(saveDir, "savegame.sav");

                // If formatted name doesn't exist, try to find it by parsing existing directories
                // This handles backward compatibility with saves created before this fix
                if (!File.Exists(saveFilePath))
                {
                    // Try to find save by matching the name part (after " - ")
                    // Uses common parsing logic from BaseSaveGameManager
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
                                saveFilePath = Path.Combine(saveDir, "savegame.sav");
                                if (File.Exists(saveFilePath))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!File.Exists(saveFilePath))
                {
                    return null;
                }

                // Read ERF archive
                byte[] erfData = await File.ReadAllBytesAsync(saveFilePath, ct);
                ERF erf = DeserializeERF(erfData);

                var saveData = new Runtime.Core.Save.SaveGameData();

                // 1. Load metadata (savenfo.res)
                byte[] nfoData = erf.Get("savenfo", ResourceType.GFF);
                if (nfoData != null)
                {
                    GFF nfoGff = DeserializeGFF(nfoData);
                    LoadSaveInfoGFF(nfoGff, saveData);
                }

                // 2. Load global variables (GLOBALVARS.res)
                byte[] globData = erf.Get("GLOBALVARS", ResourceType.GFF);
                if (globData != null)
                {
                    GFF globGff = DeserializeGFF(globData);
                    saveData.GlobalVariables = LoadGlobalVarsGFF(globGff);
                }

                // 3. Load party state (PARTYTABLE.res)
                byte[] partyData = erf.Get("PARTYTABLE", ResourceType.GFF);
                if (partyData != null)
                {
                    GFF partyGff = DeserializeGFF(partyData);
                    saveData.PartyState = LoadPartyTableGFF(partyGff);
                }

                // 4. Load per-module state ([module]_s.rim)
                string moduleRimName = saveData.CurrentModule + "_s";
                byte[] moduleRimData = erf.Get(moduleRimName, ResourceType.ERF);
                if (moduleRimData != null)
                {
                    ERF moduleRim = DeserializeERF(moduleRimData);
                    LoadModuleStateERF(moduleRim, saveData);
                }

                return saveData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveGameManager] Error loading game: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Lists all available save games.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific implementation:
        /// - Parses ERF archive format to extract save metadata
        /// - Uses common directory name parsing from BaseSaveGameManager
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
                string saveFilePath = Path.Combine(saveDir, "savegame.sav");

                if (!File.Exists(saveFilePath))
                {
                    continue;
                }

                SaveGameInfo info = null;
                try
                {
                    // Load basic info from save file
                    byte[] erfData = File.ReadAllBytes(saveFilePath);
                    ERF erf = DeserializeERF(erfData);

                    byte[] nfoData = erf.Get("savenfo", ResourceType.GFF);
                    if (nfoData != null)
                    {
                        GFF nfoGff = DeserializeGFF(nfoData);

                        // Parse save number and name from directory name using common logic
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Format "%06d - %s" (6-digit number - name)
                        // Uses common parsing methods from BaseSaveGameManager (shared with Aurora)
                        int saveNumber = ParseSaveNumberFromDirectory(saveName);
                        string displayName = ParseSaveNameFromDirectory(saveName);
                        if (string.IsNullOrEmpty(displayName))
                        {
                            // Fallback: use directory name if parsing fails (backward compatibility)
                            displayName = saveName;
                        }

                        info = new SaveGameInfo
                        {
                            Name = displayName,
                            SaveTime = File.GetLastWriteTime(saveFilePath),
                            SavePath = saveFilePath,
                            SlotIndex = saveNumber
                        };

                        // Extract metadata from NFO GFF
                        var root = nfoGff.Root;
                        if (root != null)
                        {
                            // Use SAVEGAMENAME from GFF as the actual save name (user-entered name)
                            string saveGameName = GetStringField(root, "SAVEGAMENAME", "");
                            if (!string.IsNullOrEmpty(saveGameName))
                            {
                                info.Name = saveGameName;
                            }

                            info.PlayerName = GetStringField(root, "PCNAME", "");
                            info.ModuleName = GetStringField(root, "AREANAME", ""); // AREANAME is actually the module name
                            info.PlayTime = TimeSpan.FromSeconds(GetFloatField(root, "TIMEPLAYED", 0f));

                            // Get save number from GFF if available (more reliable than parsing directory name)
                            int gffSaveNumber = GetIntField(root, "SAVENUMBER", 0);
                            if (gffSaveNumber > 0)
                            {
                                info.SlotIndex = gffSaveNumber;
                            }
                        }
                    }
                }
                catch
                {
                    // Skip corrupted saves
                    continue;
                }

                if (info != null)
                {
                    yield return info;
                }
            }
        }

        #region GFF Creation Helpers

        private GFF CreateSaveInfoGFF(SaveGameData saveData)
        {
            var gff = new GFF();
            var root = gff.Root;

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004eb750 @ 0x004eb750
            // Located via string reference: "savenfo" @ 0x007be1f0
            // Original implementation (from decompiled 0x004eb750):
            // Creates GFF with "NFO " signature and "V2.0" version
            // Writes fields in this exact order (matching original):
            // 1. AREANAME (string): Current area name from module state
            // 2. LASTMODULE (string): Last module ResRef
            // 3. TIMEPLAYED (int32): Total seconds played (uint32 from party system)
            // 4. CHEATUSED (byte): Cheat used flag (bool converted to byte)
            // 5. SAVEGAMENAME (string): Save game name
            // 6. TIMESTAMP (int64): FILETIME structure (GetLocalTime + SystemTimeToFileTime)
            // 7. PCNAME (string): Player character name from party system
            // 8. SAVENUMBER (int32): Save slot number
            // 9. GAMEPLAYHINT (byte): Gameplay hint flag
            // 10. STORYHINT0-9 (bytes): Story hint flags (10 boolean flags)
            // 11. LIVECONTENT (byte): Bitmask for live content (1 << (i-1) for each enabled entry)
            // 12. LIVE1-9 (strings): Live content entry strings (up to 9 entries)

            // Field order must match 0x004eb750 exactly
            SetStringField(root, "AREANAME", saveData.CurrentAreaName ?? "");
            SetStringField(root, "LASTMODULE", saveData.CurrentModule ?? "");
            SetIntField(root, "TIMEPLAYED", (int)saveData.PlayTime.TotalSeconds);
            SetIntField(root, "CHEATUSED", saveData.CheatUsed ? 1 : 0);
            SetStringField(root, "SAVEGAMENAME", saveData.Name ?? "");

            // TIMESTAMP - FileTime (64-bit integer: dwLowDateTime, dwHighDateTime)
            // Original uses GetLocalTime + SystemTimeToFileTime to create FILETIME
            DateTime saveTime = saveData.SaveTime != default(DateTime) ? saveData.SaveTime : DateTime.Now;
            long fileTime = saveTime.ToFileTime();
            SetInt64Field(root, "TIMESTAMP", fileTime);

            // PCNAME - Player character name
            string playerName = "";
            if (saveData.PartyState != null && saveData.PartyState.PlayerCharacter != null)
            {
                playerName = saveData.PartyState.PlayerCharacter.Tag ?? "";
            }
            SetStringField(root, "PCNAME", playerName);

            SetIntField(root, "SAVENUMBER", saveData.SaveNumber);
            SetIntField(root, "GAMEPLAYHINT", saveData.GameplayHint ? 1 : 0);

            // STORYHINT0-9 - Story hint flags (bytes)
            for (int i = 0; i < 10; i++)
            {
                string hintField = "STORYHINT" + i.ToString();
                bool hintValue = saveData.StoryHints != null && i < saveData.StoryHints.Count && saveData.StoryHints[i];
                SetIntField(root, hintField, hintValue ? 1 : 0);
            }

            // LIVECONTENT - Bitmask for live content flags (byte)
            // Original uses bitmask: 1 << (i-1) for each enabled live content
            byte liveContent = 0;
            if (saveData.LiveContent != null)
            {
                for (int i = 0; i < saveData.LiveContent.Count && i < 32; i++)
                {
                    if (saveData.LiveContent[i])
                    {
                        liveContent |= (byte)(1 << (i & 0x1F));
                    }
                }
            }
            SetIntField(root, "LIVECONTENT", liveContent);

            // LIVE1-9 - Live content entry strings (up to 9 entries)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004eb750 @ 0x004eb750
            // Original implementation stores LIVE1-9 as string fields in NFO GFF
            if (saveData.LiveContentStrings != null)
            {
                for (int i = 1; i <= 9; i++)
                {
                    string liveField = "LIVE" + i.ToString();
                    string liveValue = (i - 1 < saveData.LiveContentStrings.Count) ? (saveData.LiveContentStrings[i - 1] ?? "") : "";
                    SetStringField(root, liveField, liveValue);
                }
            }
            else
            {
                // Initialize empty LIVE1-9 fields if LiveContentStrings is null
                for (int i = 1; i <= 9; i++)
                {
                    SetStringField(root, "LIVE" + i.ToString(), "");
                }
            }

            return gff;
        }

        private GFF CreateGlobalVarsGFF(GlobalVariableState globalVars)
        {
            var gff = new GFF();
            var root = gff.Root;

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005ac670 @ 0x005ac670
            // GLOB GFF structure: VariableList array with VariableName, VariableType, VariableValue
            var varList = new GFFList();
            root.SetList("VariableList", varList);

            if (globalVars != null)
            {
                // Save boolean globals
                if (globalVars.Booleans != null)
                {
                    foreach (KeyValuePair<string, bool> kvp in globalVars.Booleans)
                    {
                        var varStruct = varList.Add();
                        SetStringField(varStruct, "VariableName", kvp.Key);
                        SetIntField(varStruct, "VariableType", 0); // BOOLEAN
                        SetIntField(varStruct, "VariableValue", kvp.Value ? 1 : 0);
                    }
                }

                // Save numeric globals
                if (globalVars.Numbers != null)
                {
                    foreach (KeyValuePair<string, int> kvp in globalVars.Numbers)
                    {
                        var varStruct = varList.Add();
                        SetStringField(varStruct, "VariableName", kvp.Key);
                        SetIntField(varStruct, "VariableType", 1); // INT
                        SetIntField(varStruct, "VariableValue", kvp.Value);
                    }
                }

                // Save string globals
                if (globalVars.Strings != null)
                {
                    foreach (KeyValuePair<string, string> kvp in globalVars.Strings)
                    {
                        var varStruct = varList.Add();
                        SetStringField(varStruct, "VariableName", kvp.Key);
                        SetIntField(varStruct, "VariableType", 3); // STRING
                        SetStringField(varStruct, "VariableValue", kvp.Value);
                    }
                }
            }

            return gff;
        }

        private GFF CreatePartyTableGFF(PartyState partyState)
        {
            var gff = new GFF();
            var root = gff.Root;

            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057bd70 @ 0x0057bd70
            // PT GFF structure: PartyList array with party member data
            var partyList = new GFFList();
            root.SetList("PartyList", partyList);

            if (partyState != null && partyState.AvailableMembers != null)
            {
                foreach (KeyValuePair<string, PartyMemberState> kvp in partyState.AvailableMembers)
                {
                    var memberStruct = partyList.Add();
                    SetStringField(memberStruct, "TemplateResRef", kvp.Key ?? "");
                    // Save party member state data
                    PartyMemberState member = kvp.Value;
                    if (member != null)
                    {
                        SetIntField(memberStruct, "IsInParty", partyState.SelectedParty != null && partyState.SelectedParty.Contains(kvp.Key) ? 1 : 0);
                    }
                }
            }

            return gff;
        }

        private ERF CreateModuleStateERF(SaveGameData saveData)
        {
            // Module state files are RIM format, but ERF class uses ERF type
            // The actual format will be written as RIM when serialized
            var erf = new ERF(ERFType.ERF);

            // Save area states
            if (saveData.AreaStates != null)
            {
                foreach (KeyValuePair<string, AreaState> kvp in saveData.AreaStates)
                {
                    string areaResRef = kvp.Key;
                    AreaState areaState = kvp.Value;

                    // Create ARE GFF for area state
                    GFF areaGff = CreateAreaStateGFF(areaState);
                    byte[] areaData = SerializeGFF(areaGff);
                    erf.SetData(areaResRef, ResourceType.ARE, areaData);
                }
            }

            return erf;
        }

        private GFF CreateAreaStateGFF(AreaState areaState)
        {
            var gff = new GFF();
            var root = gff.Root;

            // Save area-specific state (entity positions, door states, etc.)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 saves entity states to GFF format
            // Located via string references: Entity state serialization in save system
            // Original implementation: Saves entity positions, door/placeable states, HP, local variables, etc.
            if (areaState != null)
            {
                SetStringField(root, "AreaResRef", areaState.AreaResRef ?? "");

                // Save creature states
                if (areaState.CreatureStates != null && areaState.CreatureStates.Count > 0)
                {
                    var creatureList = new GFFList();
                    root.SetList("CreatureList", creatureList);
                    foreach (EntityState entityState in areaState.CreatureStates)
                    {
                        var entityStruct = creatureList.Add();
                        SaveEntityStateToGFF(entityStruct, entityState);
                    }
                }

                // Save door states
                if (areaState.DoorStates != null && areaState.DoorStates.Count > 0)
                {
                    var doorList = new GFFList();
                    root.SetList("DoorList", doorList);
                    foreach (EntityState entityState in areaState.DoorStates)
                    {
                        var entityStruct = doorList.Add();
                        SaveEntityStateToGFF(entityStruct, entityState);
                    }
                }

                // Save placeable states
                if (areaState.PlaceableStates != null && areaState.PlaceableStates.Count > 0)
                {
                    var placeableList = new GFFList();
                    root.SetList("PlaceableList", placeableList);
                    foreach (EntityState entityState in areaState.PlaceableStates)
                    {
                        var entityStruct = placeableList.Add();
                        SaveEntityStateToGFF(entityStruct, entityState);
                    }
                }

                // Save destroyed entity IDs
                if (areaState.DestroyedEntityIds != null && areaState.DestroyedEntityIds.Count > 0)
                {
                    var destroyedList = new GFFList();
                    root.SetList("DestroyedList", destroyedList);
                    foreach (uint objectId in areaState.DestroyedEntityIds)
                    {
                        var destroyedStruct = destroyedList.Add();
                        SetIntField(destroyedStruct, "ObjectId", (int)objectId);
                    }
                }

                // Save spawned entities (dynamically created, not in original GIT)
                if (areaState.SpawnedEntities != null && areaState.SpawnedEntities.Count > 0)
                {
                    var spawnedList = new GFFList();
                    root.SetList("SpawnedList", spawnedList);
                    foreach (SpawnedEntityState spawnedState in areaState.SpawnedEntities)
                    {
                        var entityStruct = spawnedList.Add();
                        SaveEntityStateToGFF(entityStruct, spawnedState);
                        SetStringField(entityStruct, "BlueprintResRef", spawnedState.BlueprintResRef ?? "");
                        SetStringField(entityStruct, "SpawnedBy", spawnedState.SpawnedBy ?? "");
                    }
                }
            }

            return gff;
        }

        /// <summary>
        /// Saves an EntityState to a GFF struct.
        /// </summary>
        private void SaveEntityStateToGFF(GFFStruct entityStruct, EntityState entityState)
        {
            if (entityStruct == null || entityState == null)
            {
                return;
            }

            // Basic entity data
            SetIntField(entityStruct, "ObjectId", (int)entityState.ObjectId);
            SetStringField(entityStruct, "Tag", entityState.Tag ?? "");
            SetStringField(entityStruct, "TemplateResRef", entityState.TemplateResRef ?? "");
            SetIntField(entityStruct, "ObjectType", (int)entityState.ObjectType);

            // Position and orientation
            SetFloatField(entityStruct, "X", entityState.Position.X);
            SetFloatField(entityStruct, "Y", entityState.Position.Y);
            SetFloatField(entityStruct, "Z", entityState.Position.Z);
            SetFloatField(entityStruct, "Facing", entityState.Facing);

            // Stats (for creatures)
            SetIntField(entityStruct, "CurrentHP", entityState.CurrentHP);
            SetIntField(entityStruct, "MaxHP", entityState.MaxHP);

            // Door/placeable states
            SetIntField(entityStruct, "IsOpen", entityState.IsOpen ? 1 : 0);
            SetIntField(entityStruct, "IsLocked", entityState.IsLocked ? 1 : 0);
            SetIntField(entityStruct, "IsDestroyed", entityState.IsDestroyed ? 1 : 0);
            SetIntField(entityStruct, "IsPlot", entityState.IsPlot ? 1 : 0);
            SetIntField(entityStruct, "AnimationState", entityState.AnimationState);

            // Local variables (if present)
            if (entityState.LocalVariables != null && !entityState.LocalVariables.IsEmpty)
            {
                var localVarStruct = new GFFStruct();
                entityStruct.SetStruct("LocalVariables", localVarStruct);

                // Save integer variables
                if (entityState.LocalVariables.Ints != null && entityState.LocalVariables.Ints.Count > 0)
                {
                    var intList = new GFFList();
                    localVarStruct.SetList("IntList", intList);
                    foreach (var kvp in entityState.LocalVariables.Ints)
                    {
                        var varStruct = intList.Add();
                        SetStringField(varStruct, "Name", kvp.Key);
                        SetIntField(varStruct, "Value", kvp.Value);
                    }
                }

                // Save float variables
                if (entityState.LocalVariables.Floats != null && entityState.LocalVariables.Floats.Count > 0)
                {
                    var floatList = new GFFList();
                    localVarStruct.SetList("FloatList", floatList);
                    foreach (var kvp in entityState.LocalVariables.Floats)
                    {
                        var varStruct = floatList.Add();
                        SetStringField(varStruct, "Name", kvp.Key);
                        SetFloatField(varStruct, "Value", kvp.Value);
                    }
                }

                // Save string variables
                if (entityState.LocalVariables.Strings != null && entityState.LocalVariables.Strings.Count > 0)
                {
                    var stringList = new GFFList();
                    localVarStruct.SetList("StringList", stringList);
                    foreach (var kvp in entityState.LocalVariables.Strings)
                    {
                        var varStruct = stringList.Add();
                        SetStringField(varStruct, "Name", kvp.Key);
                        SetStringField(varStruct, "Value", kvp.Value);
                    }
                }

                // Save object reference variables
                if (entityState.LocalVariables.Objects != null && entityState.LocalVariables.Objects.Count > 0)
                {
                    var objectList = new GFFList();
                    localVarStruct.SetList("ObjectList", objectList);
                    foreach (var kvp in entityState.LocalVariables.Objects)
                    {
                        var varStruct = objectList.Add();
                        SetStringField(varStruct, "Name", kvp.Key);
                        SetIntField(varStruct, "Value", (int)kvp.Value);
                    }
                }
            }
        }

        #endregion

        #region GFF Loading Helpers

        private void LoadSaveInfoGFF(GFF gff, SaveGameData saveData)
        {
            var root = gff.Root;
            if (root == null)
            {
                return;
            }

            saveData.Name = GetStringField(root, "SAVEGAMENAME", "");
            saveData.CurrentAreaName = GetStringField(root, "AREANAME", "");
            saveData.PlayTime = TimeSpan.FromSeconds(GetFloatField(root, "TIMEPLAYED", 0f));
            saveData.CurrentModule = GetStringField(root, "MODULENAME", "");
            saveData.SaveNumber = GetIntField(root, "SAVENUMBER", 0);
            saveData.CheatUsed = GetIntField(root, "CHEATUSED", 0) != 0;
            saveData.GameplayHint = GetIntField(root, "GAMEPLAYHINT", 0) != 0;

            // Load STORYHINT0-9
            if (saveData.StoryHints == null)
            {
                saveData.StoryHints = new List<bool>();
            }
            for (int i = 0; i < 10; i++)
            {
                string hintField = "STORYHINT" + i.ToString();
                bool hintValue = GetIntField(root, hintField, 0) != 0;
                if (i < saveData.StoryHints.Count)
                {
                    saveData.StoryHints[i] = hintValue;
                }
                else
                {
                    saveData.StoryHints.Add(hintValue);
                }
            }

            // Load LIVECONTENT bitmask
            byte liveContentBitmask = (byte)GetIntField(root, "LIVECONTENT", 0);
            if (saveData.LiveContent == null)
            {
                saveData.LiveContent = new List<bool>();
            }
            // Parse bitmask to boolean list
            for (int i = 0; i < 32; i++)
            {
                bool isSet = (liveContentBitmask & (1 << (i & 0x1F))) != 0;
                if (i < saveData.LiveContent.Count)
                {
                    saveData.LiveContent[i] = isSet;
                }
                else
                {
                    saveData.LiveContent.Add(isSet);
                }
            }

            // Load LIVE1-9 strings
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00707290 @ 0x00707290 loads LIVE1-9 from NFO GFF
            if (saveData.LiveContentStrings == null)
            {
                saveData.LiveContentStrings = new List<string>();
            }
            for (int i = 1; i <= 9; i++)
            {
                string liveField = "LIVE" + i.ToString();
                string liveValue = GetStringField(root, liveField, "");
                if (i - 1 < saveData.LiveContentStrings.Count)
                {
                    saveData.LiveContentStrings[i - 1] = liveValue;
                }
                else
                {
                    saveData.LiveContentStrings.Add(liveValue);
                }
            }

            // Load TIMESTAMP (FileTime)
            long timestamp = GetInt64Field(root, "TIMESTAMP", 0);
            if (timestamp != 0)
            {
                try
                {
                    saveData.SaveTime = DateTime.FromFileTime(timestamp);
                }
                catch (ArgumentOutOfRangeException)
                {
                    saveData.SaveTime = DateTime.Now;
                }
            }

            // Load PCNAME
            saveData.PlayerName = GetStringField(root, "PCNAME", "");
        }

        private GlobalVariableState LoadGlobalVarsGFF(GFF gff)
        {
            var state = new GlobalVariableState();
            var root = gff.Root;
            if (root == null)
            {
                return state;
            }

            var varList = root.GetList("VariableList");
            if (varList != null)
            {
                foreach (GFFStruct varStruct in varList)
                {
                    string varName = GetStringField(varStruct, "VariableName", "");
                    int varType = GetIntField(varStruct, "VariableType", 0);

                    if (string.IsNullOrEmpty(varName))
                    {
                        continue;
                    }

                    switch (varType)
                    {
                        case 0: // BOOLEAN
                            int boolVal = GetIntField(varStruct, "VariableValue", 0);
                            state.Booleans[varName] = boolVal != 0;
                            break;
                        case 1: // INT
                            int intVal = GetIntField(varStruct, "VariableValue", 0);
                            state.Numbers[varName] = intVal;
                            break;
                        case 3: // STRING
                            string strVal = GetStringField(varStruct, "VariableValue", "");
                            state.Strings[varName] = strVal;
                            break;
                    }
                }
            }

            return state;
        }

        private PartyState LoadPartyTableGFF(GFF gff)
        {
            var state = new PartyState();
            var root = gff.Root;
            if (root == null)
            {
                return state;
            }

            // Initialize collections if null
            if (state.AvailableMembers == null)
            {
                state.AvailableMembers = new Dictionary<string, PartyMemberState>();
            }
            if (state.SelectedParty == null)
            {
                state.SelectedParty = new List<string>();
            }

            var partyList = root.GetList("PartyList");
            if (partyList != null)
            {
                foreach (GFFStruct memberStruct in partyList)
                {
                    string templateResRef = GetStringField(memberStruct, "TemplateResRef", "");
                    if (string.IsNullOrEmpty(templateResRef))
                    {
                        continue;
                    }

                    var memberState = new PartyMemberState
                    {
                        TemplateResRef = templateResRef
                    };
                    bool isInParty = GetIntField(memberStruct, "IsInParty", 0) != 0;

                    state.AvailableMembers[templateResRef] = memberState;

                    if (isInParty)
                    {
                        state.SelectedParty.Add(templateResRef);
                    }
                }
            }

            return state;
        }

        private void LoadModuleStateERF(ERF erf, SaveGameData saveData)
        {
            // Load area states from module RIM
            // LoadSavegame @ (K1: 0x00708990, TSL: 0x008529e0): Module state loading from [module]_s.rim ERF archive
            // Located via string references: "GAMEINPROGRESS:" @ 0x00993e84, "_s.rim" @ 0x009a5ab8, "savenfo" @ 0x009940b8
            // Original implementation: Main save game loading function (0x008529e0 in TSL) loads ERF archive, extracts [module]_s.rim resource
            // Calls FUN_007221c0 to iterate through ERF resources with "GAMEINPROGRESS:" prefix, processes each nested ERF/RIM resource
            // Module state ERF ([module]_s.rim) contains ARE resources (area state GFF files) for each visited area in the module
            // Each ARE resource contains GFF data with entity positions, door/placeable states, destroyed entities, spawned entities, etc.
            // Function flow: LoadSavegame (0x008529e0) -> Loads main ERF -> Extracts [module]_s.rim -> Iterates ARE resources -> Loads area state GFF

            if (erf == null || saveData == null)
            {
                return;
            }

            // Initialize AreaStates dictionary if null
            if (saveData.AreaStates == null)
            {
                saveData.AreaStates = new Dictionary<string, AreaState>();
            }

            // Iterate through all resources in the ERF
            // Each ARE resource represents an area state
            foreach (ERFResource resource in erf)
            {
                // Only process ARE resources (area state files)
                if (resource.ResType != ResourceType.ARE)
                {
                    continue;
                }

                try
                {
                    // Get resource data
                    byte[] areaData = resource.Data;
                    if (areaData == null || areaData.Length == 0)
                    {
                        continue;
                    }

                    // Deserialize GFF
                    GFF areaGff = DeserializeGFF(areaData);
                    if (areaGff == null || areaGff.Root == null)
                    {
                        continue;
                    }

                    // Create AreaState from GFF
                    AreaState areaState = LoadAreaStateGFF(areaGff, resource.ResRef);
                    if (areaState != null && !string.IsNullOrEmpty(areaState.AreaResRef))
                    {
                        // Use AreaResRef from GFF if available, otherwise use resource ResRef
                        string areaKey = !string.IsNullOrEmpty(areaState.AreaResRef)
                            ? areaState.AreaResRef
                            : resource.ResRef.ToString();
                        saveData.AreaStates[areaKey] = areaState;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SaveGameManager] Error loading area state from {resource.ResRef}: {ex.Message}");
                    // Continue loading other areas even if one fails
                }
            }
        }

        /// <summary>
        /// Loads an AreaState from a GFF structure.
        /// </summary>
        private AreaState LoadAreaStateGFF(GFF gff, string defaultResRef)
        {
            var areaState = new AreaState();
            var root = gff.Root;
            if (root == null)
            {
                return null;
            }

            // Get area ResRef
            areaState.AreaResRef = GetStringField(root, "AreaResRef", defaultResRef);
            areaState.Visited = true; // If it's in the save, it was visited

            // Load creature states
            GFFList creatureList = root.GetList("CreatureList");
            if (creatureList != null)
            {
                foreach (GFFStruct creatureStruct in creatureList)
                {
                    var entityState = LoadEntityStateFromGFF(creatureStruct);
                    entityState.ObjectType = Runtime.Core.Enums.ObjectType.Creature;
                    areaState.CreatureStates.Add(entityState);
                }
            }

            // Load door states (if present in GFF)
            GFFList doorList = root.GetList("DoorList");
            if (doorList != null)
            {
                foreach (GFFStruct doorStruct in doorList)
                {
                    var entityState = LoadEntityStateFromGFF(doorStruct);
                    entityState.ObjectType = Runtime.Core.Enums.ObjectType.Door;
                    areaState.DoorStates.Add(entityState);
                }
            }

            // Load placeable states (if present in GFF)
            GFFList placeableList = root.GetList("PlaceableList");
            if (placeableList != null)
            {
                foreach (GFFStruct placeableStruct in placeableList)
                {
                    var entityState = LoadEntityStateFromGFF(placeableStruct);
                    entityState.ObjectType = Runtime.Core.Enums.ObjectType.Placeable;
                    areaState.PlaceableStates.Add(entityState);
                }
            }

            // Load destroyed entity IDs
            GFFList destroyedList = root.GetList("DestroyedList");
            if (destroyedList != null)
            {
                foreach (GFFStruct destroyedStruct in destroyedList)
                {
                    uint objectId = (uint)GetIntField(destroyedStruct, "ObjectId", 0);
                    if (objectId != 0)
                    {
                        areaState.DestroyedEntityIds.Add(objectId);
                    }
                }
            }

            // Load spawned entities (dynamically created, not in original GIT)
            GFFList spawnedList = root.GetList("SpawnedList");
            if (spawnedList != null)
            {
                foreach (GFFStruct spawnedStruct in spawnedList)
                {
                    var spawnedState = new SpawnedEntityState();
                    LoadEntityStateFromGFF(spawnedStruct, spawnedState);
                    spawnedState.BlueprintResRef = GetStringField(spawnedStruct, "BlueprintResRef", "");
                    spawnedState.SpawnedBy = GetStringField(spawnedStruct, "SpawnedBy", "");
                    areaState.SpawnedEntities.Add(spawnedState);
                }
            }

            return areaState;
        }

        /// <summary>
        /// Loads an EntityState from a GFF struct.
        /// </summary>
        private EntityState LoadEntityStateFromGFF(GFFStruct entityStruct, EntityState entityState = null)
        {
            if (entityStruct == null)
            {
                return null;
            }

            if (entityState == null)
            {
                entityState = new EntityState();
            }

            // Basic entity data
            entityState.ObjectId = (uint)GetIntField(entityStruct, "ObjectId", 0);
            entityState.Tag = GetStringField(entityStruct, "Tag", "");
            entityState.TemplateResRef = GetStringField(entityStruct, "TemplateResRef", "");
            entityState.ObjectType = (Runtime.Core.Enums.ObjectType)GetIntField(entityStruct, "ObjectType", 0);

            // Position and orientation
            entityState.Position = new System.Numerics.Vector3(
                GetFloatField(entityStruct, "X", 0f),
                GetFloatField(entityStruct, "Y", 0f),
                GetFloatField(entityStruct, "Z", 0f)
            );
            entityState.Facing = GetFloatField(entityStruct, "Facing", 0f);

            // Stats (for creatures)
            entityState.CurrentHP = GetIntField(entityStruct, "CurrentHP", 1);
            entityState.MaxHP = GetIntField(entityStruct, "MaxHP", 1);

            // Door/placeable states
            entityState.IsOpen = GetIntField(entityStruct, "IsOpen", 0) != 0;
            entityState.IsLocked = GetIntField(entityStruct, "IsLocked", 0) != 0;
            entityState.IsDestroyed = GetIntField(entityStruct, "IsDestroyed", 0) != 0;
            entityState.IsPlot = GetIntField(entityStruct, "IsPlot", 0) != 0;
            entityState.AnimationState = GetIntField(entityStruct, "AnimationState", 0);

            // Local variables (if present)
            GFFStruct localVarStruct = entityStruct.GetStruct("LocalVariables");
            if (localVarStruct != null)
            {
                entityState.LocalVariables = new LocalVariableSet();

                // Load integer variables
                GFFList intList = localVarStruct.GetList("IntList");
                if (intList != null)
                {
                    foreach (GFFStruct varStruct in intList)
                    {
                        string name = GetStringField(varStruct, "Name", "");
                        int value = GetIntField(varStruct, "Value", 0);
                        if (!string.IsNullOrEmpty(name))
                        {
                            entityState.LocalVariables.Ints[name] = value;
                        }
                    }
                }

                // Load float variables
                GFFList floatList = localVarStruct.GetList("FloatList");
                if (floatList != null)
                {
                    foreach (GFFStruct varStruct in floatList)
                    {
                        string name = GetStringField(varStruct, "Name", "");
                        float value = GetFloatField(varStruct, "Value", 0f);
                        if (!string.IsNullOrEmpty(name))
                        {
                            entityState.LocalVariables.Floats[name] = value;
                        }
                    }
                }

                // Load string variables
                GFFList stringList = localVarStruct.GetList("StringList");
                if (stringList != null)
                {
                    foreach (GFFStruct varStruct in stringList)
                    {
                        string name = GetStringField(varStruct, "Name", "");
                        string value = GetStringField(varStruct, "Value", "");
                        if (!string.IsNullOrEmpty(name))
                        {
                            entityState.LocalVariables.Strings[name] = value;
                        }
                    }
                }

                // Load object reference variables
                GFFList objectList = localVarStruct.GetList("ObjectList");
                if (objectList != null)
                {
                    foreach (GFFStruct varStruct in objectList)
                    {
                        string name = GetStringField(varStruct, "Name", "");
                        uint value = (uint)GetIntField(varStruct, "Value", 0);
                        if (!string.IsNullOrEmpty(name))
                        {
                            entityState.LocalVariables.Objects[name] = value;
                        }
                    }
                }
            }

            return entityState;
        }

        #endregion

        #region GFF Serialization Helpers

        private byte[] SerializeGFF(GFF gff)
        {
            // Use GFF's built-in ToBytes method
            return gff.ToBytes();
        }

        private GFF DeserializeGFF(byte[] data)
        {
            // Use GFF's built-in FromBytes method
            return GFF.FromBytes(data);
        }

        private byte[] SerializeERF(ERF erf)
        {
            var writer = new ERFBinaryWriter(erf);
            return writer.Write();
        }

        private ERF DeserializeERF(byte[] data)
        {
            var reader = new ERFBinaryReader(data);
            return reader.Load();
        }

        #endregion

        #region Odyssey-Specific Save Helpers (ERF/GFF Format)

        #region GFF Field Helpers

        private void SetStringField(GFFStruct gffStruct, string fieldName, string value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            // Use BioWare.NET GFF API
            gffStruct.SetString(fieldName, value ?? "");
        }

        private void SetIntField(GFFStruct gffStruct, string fieldName, int value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            gffStruct.SetInt32(fieldName, value);
        }

        private void SetInt64Field(GFFStruct gffStruct, string fieldName, long value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            gffStruct.SetInt64(fieldName, value);
        }

        private void SetFloatField(GFFStruct gffStruct, string fieldName, float value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            gffStruct.SetSingle(fieldName, value);
        }

        private string GetStringField(GFFStruct gffStruct, string fieldName, string defaultValue)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return defaultValue;
            }

            try
            {
                return gffStruct.GetString(fieldName) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        private int GetIntField(GFFStruct gffStruct, string fieldName, int defaultValue)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return defaultValue;
            }

            try
            {
                return gffStruct.GetInt32(fieldName);
            }
            catch
            {
                return defaultValue;
            }
        }

        private float GetFloatField(GFFStruct gffStruct, string fieldName, float defaultValue)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return defaultValue;
            }

            try
            {
                return gffStruct.GetSingle(fieldName);
            }
            catch
            {
                return defaultValue;
            }
        }

        private long GetInt64Field(GFFStruct gffStruct, string fieldName, long defaultValue)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return defaultValue;
            }

            try
            {
                return gffStruct.GetInt64(fieldName);
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion

        #endregion
    }
}

