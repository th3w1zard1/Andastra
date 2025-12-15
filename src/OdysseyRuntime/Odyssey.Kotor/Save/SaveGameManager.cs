using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CSharpKOTOR.Formats.ERF;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Save;

namespace Odyssey.Kotor.Save
{
    /// <summary>
    /// Manages save game operations (save and load).
    /// </summary>
    /// <remarks>
    /// Save Game Manager:
    /// - Based on swkotor2.exe save game system
    /// - Located via string references: "savenfo" @ 0x007be1f0, "SAVEGAME" @ 0x007be28c, "SAVES:" @ 0x007be284
    /// - "LoadSavegame" @ 0x007bdc90, "SavegameList" @ 0x007bdca0, "GetSavegameList" @ 0x007bdcb0
    /// - "SAVEGAMENAME" @ 0x007be1a8, "Mod_IsSaveGame" @ 0x007bea48, "BTN_SAVEGAME" @ 0x007d0dbc
    /// - Save function: FUN_004eb750 @ 0x004eb750 creates save game ERF archive
    /// - Load function: FUN_00708990 @ 0x00708990 (main load function, loads ERF archive)
    /// - FUN_004e28c0 @ 0x004e28c0 loads creature list from module state
    ///   - Original implementation: Iterates through "Creature List" GFF list, loads each creature via FUN_005226d0
    ///   - Only loads creatures that are not PC (IsPC == 0) and not destroyed (IsDestroyed == 0)
    ///   - Creates creature entities from saved ObjectId and GFF data
    ///   - Saves creature state to GFF structure (position, stats, inventory, scripts, etc.)
    /// - Save file format: ERF archive with "MOD V1.0" signature containing:
    ///   - savenfo.res (GFF with "NFO " signature): Save metadata (AREANAME, TIMEPLAYED, SAVEGAMENAME, etc.)
    ///   - GLOBALVARS.res (GFF with "GLOB" signature): Global variable state
    ///   - PARTYTABLE.res (GFF with "PT  " signature): Party state
    ///   - [module]_s.rim: Per-module state ERF archive (area states, entity positions, etc.)
    /// - Save location: "SAVES:\{saveName}\savegame.sav" (ERF archive)
    /// - Original implementation: Save files are ERF archives containing GFF resources
    /// - Based on KOTOR save game format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public class SaveGameManager
    {
        private readonly IGameResourceProvider _resourceProvider;
        private readonly string _savesDirectory;

        public SaveGameManager(IGameResourceProvider resourceProvider, string savesDirectory)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException("resourceProvider");
            _savesDirectory = savesDirectory ?? throw new ArgumentNullException("savesDirectory");
            
            // Ensure saves directory exists
            if (!Directory.Exists(_savesDirectory))
            {
                Directory.CreateDirectory(_savesDirectory);
            }
        }

        /// <summary>
        /// Saves the current game state to a save file.
        /// </summary>
        public async Task<bool> SaveGameAsync(SaveGameData saveData, string saveName, CancellationToken ct = default)
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
                // Create save directory
                string saveDir = Path.Combine(_savesDirectory, saveName);
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
                // This would contain area states, entity positions, etc.
                // For now, we'll create a placeholder
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
        public async Task<SaveGameData> LoadGameAsync(string saveName, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                throw new ArgumentException("Save name cannot be null or empty", "saveName");
            }

            try
            {
                string saveDir = Path.Combine(_savesDirectory, saveName);
                string saveFilePath = Path.Combine(saveDir, "savegame.sav");

                if (!File.Exists(saveFilePath))
                {
                    return null;
                }

                // Read ERF archive
                byte[] erfData = await File.ReadAllBytesAsync(saveFilePath, ct);
                ERF erf = DeserializeERF(erfData);

                var saveData = new SaveGameData();

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
        public IEnumerable<SaveGameInfo> ListSaves()
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
                        
                        info = new SaveGameInfo
                        {
                            Name = saveName,
                            SaveTime = File.GetLastWriteTime(saveFilePath),
                            SavePath = saveFilePath
                        };

                        // Extract metadata from NFO GFF
                        var root = nfoGff.Root;
                        if (root != null)
                        {
                            info.PlayerName = GetStringField(root, "SAVEGAMENAME", "");
                            info.ModuleName = GetStringField(root, "AREANAME", ""); // AREANAME is actually the module name
                            info.PlayTime = TimeSpan.FromSeconds(GetFloatField(root, "TIMEPLAYED", 0f));
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

            // Based on swkotor2.exe: FUN_004eb750 @ 0x004eb750
            // NFO GFF fields: SAVEGAMENAME, AREANAME, TIMEPLAYED, etc.
            SetStringField(root, "SAVEGAMENAME", saveData.Name ?? "");
            SetStringField(root, "AREANAME", saveData.CurrentAreaName ?? "");
            SetFloatField(root, "TIMEPLAYED", (float)saveData.PlayTime.TotalSeconds);
            SetStringField(root, "MODULENAME", saveData.CurrentModule ?? "");
            SetIntField(root, "SAVENUMBER", saveData.SaveNumber);
            SetIntField(root, "CHEATUSED", saveData.CheatUsed ? 1 : 0);
            SetIntField(root, "GAMEPLAYHINT", saveData.GameplayHint ? 1 : 0);

            return gff;
        }

        private GFF CreateGlobalVarsGFF(GlobalVariableState globalVars)
        {
            var gff = new GFF();
            var root = gff.Root;

            // Based on swkotor2.exe: FUN_005ac670 @ 0x005ac670
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

            // Based on swkotor2.exe: FUN_0057bd70 @ 0x0057bd70
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
            if (areaState != null)
            {
                SetStringField(root, "AreaResRef", areaState.AreaResRef ?? "");
                
                // Save entity states
                // Note: AreaState has separate lists for different entity types
                // For now, this is a placeholder - full implementation would serialize all entity data
                // Save creature states
                if (areaState.CreatureStates != null && areaState.CreatureStates.Count > 0)
                {
                    var creatureList = new GFFList();
                    root.SetList("CreatureList", creatureList);
                    foreach (EntityState entityState in areaState.CreatureStates)
                    {
                        var entityStruct = creatureList.Add();
                        SetIntField(entityStruct, "ObjectId", (int)entityState.ObjectId);
                        SetFloatField(entityStruct, "X", entityState.Position.X);
                        SetFloatField(entityStruct, "Y", entityState.Position.Y);
                        SetFloatField(entityStruct, "Z", entityState.Position.Z);
                        SetFloatField(entityStruct, "Facing", entityState.Facing);
                    }
                }
            }

            return gff;
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
            // This would iterate through ARE resources in the RIM
            // For now, this is a placeholder
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

        #region GFF Field Helpers

        private void SetStringField(GFFStruct gffStruct, string fieldName, string value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            // Use CSharpKOTOR GFF API
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

        #endregion
    }
}

