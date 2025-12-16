using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using AuroraEngine.Common;
using AuroraEngine.Common.Formats.ERF;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Resources;
using Odyssey.Core.Save;

namespace Odyssey.Content.Save
{
    /// <summary>
    /// Serializes save data to/from KOTOR save file formats.
    /// </summary>
    /// <remarks>
    /// Save file formats:
    /// - savenfo.res: GFF containing metadata
    /// - savegame.sav: ERF archive containing:
    ///   - GLOBALVARS.res (GFF)
    ///   - PARTYTABLE.res (GFF)
    ///   - [module]_s.rim files
    ///
    /// Based on swkotor2.exe save serialization:
    /// - Located via string references: "savenfo" @ 0x007be1f0 (save info file), "SAVES:" @ 0x007be284 (save directory)
    /// - "SAVEGAME" @ 0x007be28c (save game directory), "SAVES:%06d - %s" @ 0x007be298 (save name format)
    /// - "LoadSavegame" @ 0x007bdc90 (load save game function), "SavegameList" @ 0x007bdca0 (save game list)
    /// - "GetSavegameList" @ 0x007bdcb0 (get save game list function), "SaveLoad" @ 0x007cb2ac (save/load system)
    /// - "AutoSave" @ 0x007bd9e8 (auto-save flag), "AutoSaveOnEnter" @ 0x007be248 (auto-save on enter flag)
    /// - "PCAUTOSAVE" @ 0x007be320 (PC auto-save flag), "AUTOSAVE" @ 0x007be34c (auto-save flag)
    /// - "AUTOSAVEPARAMS" @ 0x007be304 (auto-save parameters), "REBOOTAUTOSAVE" @ 0x007cea14 (reboot auto-save)
    /// - "QUICKSAVE" @ 0x007c7368 (quick save identifier), "Old Save Game" @ 0x007cea24 (old save game message)
    /// - "modulesave" @ 0x007bde20 (module save reference), "Mod_IsSaveGame" @ 0x007bea48 (module is save game flag)
    /// - "IncludeInSave" @ 0x007bde10 (include in save flag), "SaveGroup" @ 0x007bde38 (save group field)
    /// - "DeleteSaveGroupOnEnter" @ 0x007bde54 (delete save group on enter flag), "AtSavePoints" @ 0x007bd9cc (at save points flag)
    /// - GUI: "savename_p" @ 0x007cec48 (save name panel), "saveload_p" @ 0x007cede8 (save/load panel)
    /// - "BTN_SAVELOAD" @ 0x007ced68 (save/load button), "BTN_SAVEGAME" @ 0x007d0dbc (save game button)
    /// - "BTN_LASTSAVE" @ 0x007c8db0 (last save button), "CB_AUTOSAVE" @ 0x007d2918 (auto-save checkbox)
    /// - Save directory paths: ".\saves" @ 0x007c6b0c (saves directory)
    /// - Save NFO: FUN_004eb750 @ 0x004eb750 (creates GFF with "NFO " signature, "V2.0" version)
    /// - Save archive: FUN_004eb750 @ 0x004eb750 (creates ERF with "MOD V1.0" signature @ 0x007be0d4)
    /// - GLOBALVARS serialization: FUN_005ac670 @ 0x005ac670 (saves global variables to GFF)
    /// - PARTYTABLE serialization: FUN_0057bd70 @ 0x0057bd70 (creates GFF with "PT  " signature)
    /// </remarks>
    public class SaveSerializer : ISaveSerializer
    {
        private readonly object _gameDataManager;

        /// <summary>
        /// Creates a new save serializer.
        /// </summary>
        /// <param name="gameDataManager">Optional game data manager for party.2da lookups.</param>
        public SaveSerializer(object gameDataManager = null)
        {
            _gameDataManager = gameDataManager;
        }

        // GFF field labels for save NFO
        // Based on swkotor2.exe: FUN_004eb750 @ 0x004eb750
        // Located via string reference: "savenfo" @ 0x007be1f0
        // Original implementation uses these exact field names from GFF structure
        private const string FIELD_SAVE_NAME = "SAVEGAMENAME";
        private const string FIELD_MODULE_NAME = "LASTMODULE";
        private const string FIELD_AREA_NAME = "AREANAME";
        private const string FIELD_TIME_PLAYED = "TIMEPLAYED";
        private const string FIELD_PLAYER_NAME = "PCNAME";
        private const string FIELD_CHEAT_USED = "CHEATUSED";
        private const string FIELD_SAVE_NUMBER = "SAVENUMBER";
        private const string FIELD_GAMEPLAY_HINT = "GAMEPLAYHINT";
        private const string FIELD_STORY_HINT = "STORYHINT";
        private const string FIELD_LIVE_CONTENT = "LIVECONTENT";
        private const string FIELD_TIMESTAMP = "TIMESTAMP";

        // ERF types
        private const string ERF_TYPE_SAV = "SAV ";

        #region ISaveSerializer Implementation

        // Serialize save metadata to NFO GFF format
        // Based on swkotor2.exe: FUN_004eb750 @ 0x004eb750
        // Located via string reference: "savenfo" @ 0x007be1f0
        // Original implementation (from decompiled FUN_004eb750):
        // 1. Creates GFF with "NFO " signature (4 bytes) and "V2.0" version string
        // 2. Writes fields in this exact order:
        //    - AREANAME (string): Current area name from module state
        //    - LASTMODULE (string): Last module ResRef
        //    - TIMEPLAYED (int32): Total seconds played (uint32 from party system)
        //    - CHEATUSED (byte): Cheat used flag (bool converted to byte)
        //    - SAVEGAMENAME (string): Save game name
        //    - TIMESTAMP (int64): FILETIME structure (GetLocalTime + SystemTimeToFileTime)
        //    - PCNAME (string): Player character name from party system
        //    - SAVENUMBER (int32): Save slot number
        //    - GAMEPLAYHINT (byte): Gameplay hint flag
        //    - STORYHINT0-9 (bytes): Story hint flags (10 boolean flags)
        //    - LIVECONTENT (byte): Bitmask for live content (1 << (i-1) for each enabled entry)
        //    - LIVE1-9 (strings): Live content entry strings (up to 9 entries)
        // 3. Progress updates: 5% (0x5), 10% (0xa), 15% (0xf), 20% (0x14), 25% (0x19), 30% (0x1e)
        // 4. File path: Constructs "SAVES:\{saveName}\savenfo" path
        // Note: GFF signature is "NFO " (4 bytes), version string is "V2.0"
        public byte[] SerializeSaveNfo(SaveGameData saveData)
        {
            // Use CSharpKOTOR GFF writer
            // Original creates GFF with "NFO " signature and "V2.0" version
            // Based on swkotor2.exe: FUN_004eb750 @ 0x004eb750 creates GFF with "NFO " signature
            // Located via string reference: "savenfo" @ 0x007be1f0
            // Note: CSharpKOTOR GFFBinaryWriter always writes "V3.2" version, but signature is correct
            var gff = new GFF(GFFContent.NFO);
            GFFStruct root = gff.Root;

            // AREANAME - Current area name
            root.SetString(FIELD_AREA_NAME, saveData.CurrentAreaName ?? "");
            
            // LASTMODULE - Last module ResRef
            root.SetString(FIELD_MODULE_NAME, saveData.CurrentModule ?? "");
            
            // TIMEPLAYED - Total seconds played
            root.SetInt32(FIELD_TIME_PLAYED, (int)saveData.PlayTime.TotalSeconds);
            
            // CHEATUSED - Whether cheats were used (byte)
            root.SetUInt8(FIELD_CHEAT_USED, saveData.CheatUsed ? (byte)1 : (byte)0);
            
            // SAVEGAMENAME - Save name
            root.SetString(FIELD_SAVE_NAME, saveData.Name ?? "");
            
            // TIMESTAMP - FileTime (64-bit integer: dwLowDateTime, dwHighDateTime)
            // Original uses GetLocalTime + SystemTimeToFileTime to create FILETIME
            DateTime saveTime = saveData.SaveTime;
            long fileTime = saveTime.ToFileTime();
            root.SetInt64(FIELD_TIMESTAMP, fileTime);
            
            // PCNAME - Player character name
            string playerName = "";
            if (saveData.PartyState != null && saveData.PartyState.PlayerCharacter != null)
            {
                playerName = saveData.PartyState.PlayerCharacter.Tag ?? "";
            }
            root.SetString(FIELD_PLAYER_NAME, playerName);
            
            // SAVENUMBER - Save slot number
            root.SetInt32(FIELD_SAVE_NUMBER, saveData.SaveNumber);
            
            // GAMEPLAYHINT - Gameplay hint flag (byte)
            root.SetUInt8(FIELD_GAMEPLAY_HINT, saveData.GameplayHint ? (byte)1 : (byte)0);
            
            // STORYHINT0-9 - Story hint flags (bytes)
            for (int i = 0; i < 10; i++)
            {
                string hintField = FIELD_STORY_HINT + i.ToString();
                bool hintValue = saveData.StoryHints != null && i < saveData.StoryHints.Count && saveData.StoryHints[i];
                root.SetUInt8(hintField, hintValue ? (byte)1 : (byte)0);
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
            root.SetUInt8(FIELD_LIVE_CONTENT, liveContent);

            return gff.ToBytes();
        }

        // Deserialize save metadata from NFO GFF format
        // Based on swkotor2.exe: FUN_00707290 @ 0x00707290
        // Located via string reference: "savenfo" @ 0x007be1f0
        // Original implementation: Reads GFF with "NFO " signature, extracts AREANAME, LASTMODULE, TIMEPLAYED,
        // CHEATUSED, SAVEGAMENAME, TIMESTAMP, PCNAME, SAVENUMBER, GAMEPLAYHINT, STORYHINT0-9, LIVECONTENT,
        // REBOOTAUTOSAVE, PCAUTOSAVE, SCREENSHOT
        public SaveGameData DeserializeSaveNfo(byte[] data)
        {
            // Use CSharpKOTOR GFF reader
            GFF gff;
            try
            {
                gff = GFF.FromBytes(data);
            }
            catch (Exception)
            {
                return null;
            }

            if (gff == null || gff.Root == null)
            {
                return null;
            }

            var saveData = new SaveGameData();
            var root = gff.Root;

            // AREANAME
            saveData.CurrentAreaName = root.GetString(FIELD_AREA_NAME);
            
            // LASTMODULE
            saveData.CurrentModule = root.GetString(FIELD_MODULE_NAME);
            
            // TIMEPLAYED - Total seconds played
            int seconds = root.GetInt32(FIELD_TIME_PLAYED);
            saveData.PlayTime = TimeSpan.FromSeconds(seconds);
            
            // CHEATUSED
            saveData.CheatUsed = root.GetUInt8(FIELD_CHEAT_USED) != 0;
            
            // SAVEGAMENAME
            saveData.Name = root.GetString(FIELD_SAVE_NAME);
            if (string.IsNullOrEmpty(saveData.Name))
            {
                // Original sets to "Old Save Game" if empty
                saveData.Name = "Old Save Game";
            }
            
            // TIMESTAMP - FileTime (64-bit integer)
            long fileTime = root.GetInt64(FIELD_TIMESTAMP);
            if (fileTime != 0)
            {
                saveData.SaveTime = DateTime.FromFileTime(fileTime);
            }
            else
            {
                saveData.SaveTime = DateTime.Now;
            }
            
            // PCNAME
            saveData.PlayerName = root.GetString(FIELD_PLAYER_NAME);
            
            // SAVENUMBER
            saveData.SaveNumber = root.GetInt32(FIELD_SAVE_NUMBER);
            
            // GAMEPLAYHINT
            saveData.GameplayHint = root.GetUInt8(FIELD_GAMEPLAY_HINT) != 0;
            
            // STORYHINT0-9
            if (saveData.StoryHints == null)
            {
                saveData.StoryHints = new List<bool>();
            }
            for (int i = 0; i < 10; i++)
            {
                string hintField = FIELD_STORY_HINT + i.ToString();
                bool hintValue = root.GetUInt8(hintField) != 0;
                if (i < saveData.StoryHints.Count)
                {
                    saveData.StoryHints[i] = hintValue;
                }
                else
                {
                    saveData.StoryHints.Add(hintValue);
                }
            }
            
            // LIVECONTENT - Bitmask
            byte liveContent = root.GetUInt8(FIELD_LIVE_CONTENT);
            if (saveData.LiveContent == null)
            {
                saveData.LiveContent = new List<bool>();
            }
            for (int i = 0; i < 32; i++)
            {
                bool enabled = (liveContent & (1 << (i & 0x1F))) != 0;
                if (i < saveData.LiveContent.Count)
                {
                    saveData.LiveContent[i] = enabled;
                }
                else
                {
                    saveData.LiveContent.Add(enabled);
                }
            }

            return saveData;
        }

        // Serialize save game archive to ERF format
        // Based on swkotor2.exe: FUN_004eb750 @ 0x004eb750
        // Located via string reference: "MOD V1.0" @ 0x007be0d4
        // Original implementation: Creates ERF archive with "MOD V1.0" signature, adds GLOBALVARS.res (GFF),
        // PARTYTABLE.res (GFF), and module state files ([module]_s.rim) for each visited area
        public byte[] SerializeSaveArchive(SaveGameData saveData)
        {
            // Use CSharpKOTOR ERF writer
            var erf = new ERF(ERFType.MOD, true); // MOD type is used for save files

            // Add GLOBALVARS.res
            byte[] globalVarsData = SerializeGlobalVariables(saveData.GlobalVariables);
            erf.SetData("GLOBALVARS", ResourceType.GFF, globalVarsData);

            // Add PARTYTABLE.res
            byte[] partyTableData = SerializePartyTable(saveData.PartyState);
            erf.SetData("PARTYTABLE", ResourceType.GFF, partyTableData);

            // Add module state files
            if (saveData.AreaStates != null)
            {
                foreach (KeyValuePair<string, AreaState> kvp in saveData.AreaStates)
                {
                    string areaResRef = kvp.Key;
                    AreaState areaState = kvp.Value;

                    string stateFileName = areaResRef + "_s";
                    byte[] stateData = SerializeAreaState(areaState);
                    erf.SetData(stateFileName, ResourceType.RIM, stateData);
                }
            }

            // Write ERF using CSharpKOTOR writer
            var writer = new ERFBinaryWriter(erf);
            return writer.Write();
        }

        // Deserialize save game archive from ERF format
        // Based on swkotor2.exe: FUN_00708990 @ 0x00708990
        // Located via string reference: "LoadSavegame" @ 0x007bdc90
        // Original implementation: Reads ERF archive with "MOD V1.0" signature, extracts GLOBALVARS.res and PARTYTABLE.res,
        // reads module state files ([module]_s.rim) for each area and stores in AreaStates dictionary
        public void DeserializeSaveArchive(byte[] data, SaveGameData saveData)
        {
            // Use CSharpKOTOR ERF reader
            ERF erf;
            try
            {
                var reader = new ERFBinaryReader(data);
                erf = reader.Load();
            }
            catch (Exception)
            {
                return;
            }

            if (erf == null)
            {
                return;
            }

            // Read GLOBALVARS.res
            byte[] globalVarsData = erf.Get("GLOBALVARS", ResourceType.GFF);
            if (globalVarsData != null)
            {
                saveData.GlobalVariables = DeserializeGlobalVariables(globalVarsData);
            }

            // Read PARTYTABLE.res
            byte[] partyTableData = erf.Get("PARTYTABLE", ResourceType.GFF);
            if (partyTableData != null)
            {
                saveData.PartyState = DeserializePartyTable(partyTableData);
            }

            // Read module state files
            if (saveData.AreaStates == null)
            {
                saveData.AreaStates = new Dictionary<string, AreaState>();
            }

            foreach (ERFResource resource in erf)
            {
                string resName = resource.ResRef.ToString();
                if (resName.EndsWith("_s") && resource.ResType == ResourceType.RIM)
                {
                    string areaResRef = resName.Substring(0, resName.Length - 2);
                    AreaState areaState = DeserializeAreaState(resource.Data);
                    if (areaState != null)
                    {
                        saveData.AreaStates[areaResRef] = areaState;
                    }
                }
            }
        }

        #endregion

        #region Global Variables

        // Serialize global variables to GFF format
        // Based on swkotor2.exe: FUN_005ab310 @ 0x005ab310 (called from FUN_005ac670 @ 0x005ac670)
        // Located via string reference: "GLOBALVARS" @ 0x007c27bc
        // Original implementation: Creates GFF with "GVT " signature and "V2.0" version string
        // Structure: Catalog lists (CatBoolean, CatNumber, CatLocation, CatString) with separate value arrays
        // - CatBoolean (list of structs with "Name" field) + ValBoolean (binary byte array)
        // - CatNumber (list of structs with "Name" field) + ValNumber (binary byte array)
        // - CatLocation (list of structs with "Name" field) + ValLocation (binary float array, 12 floats per location)
        // - CatString (list of structs with "Name" field) + ValString (list of strings, indexed by CatString entry order)
        private byte[] SerializeGlobalVariables(GlobalVariableState state)
        {
            if (state == null)
            {
                state = new GlobalVariableState();
            }

            // Use CSharpKOTOR GFF writer
            // Original creates GFF with "GVT " signature and "V2.0" version
            // Based on swkotor2.exe: FUN_005ab310 @ 0x005ab310 creates GFF with "GVT " signature
            // Located via string reference: "GLOBALVARS" @ 0x007c27bc
            // Note: CSharpKOTOR GFFBinaryWriter always writes "V3.2" version, but signature is correct
            var gff = new GFF(GFFContent.GVT);
            var root = gff.Root;

            // CatBoolean list: Each entry has "Name" field
            var catBooleanList = root.Acquire<GFFList>("CatBoolean", new GFFList());
            var boolValues = new List<byte>();
            foreach (KeyValuePair<string, bool> kvp in state.Booleans)
            {
                GFFStruct entry = catBooleanList.Add();
                entry.SetString("Name", kvp.Key);
                boolValues.Add(kvp.Value ? (byte)1 : (byte)0);
            }
            // ValBoolean: Binary byte array
            if (boolValues.Count > 0)
            {
                root.SetBinary("ValBoolean", boolValues.ToArray());
            }

            // CatNumber list: Each entry has "Name" field
            // Based on swkotor2.exe: FUN_005ab310 @ 0x005ab310
            // Original implementation: Stores numbers as bytes in ValNumber binary array
            // Note: The original engine uses a byte array, but integers are typically stored as 4-byte values
            // However, the Ghidra code shows it stores as single bytes, so we match that format
            var catNumberList = root.Acquire<GFFList>("CatNumber", new GFFList());
            var numValues = new List<byte>();
            foreach (KeyValuePair<string, int> kvp in state.Numbers)
            {
                GFFStruct entry = catNumberList.Add();
                entry.SetString("Name", kvp.Key);
                // Store as single byte (original uses byte array per Ghidra FUN_005ab310)
                // The original engine stores numbers as bytes, truncating to 0-255 range
                byte numValue = (byte)(kvp.Value & 0xFF);
                numValues.Add(numValue);
            }
            // ValNumber: Binary byte array
            if (numValues.Count > 0)
            {
                root.SetBinary("ValNumber", numValues.ToArray());
            }

            // CatLocation list: Each entry has "Name" field
            var catLocationList = root.Acquire<GFFList>("CatLocation", new GFFList());
            var locationValues = new List<float>();
            foreach (KeyValuePair<string, SavedLocation> kvp in state.Locations)
            {
                GFFStruct entry = catLocationList.Add();
                entry.SetString("Name", kvp.Key);
                SavedLocation loc = kvp.Value;
                // Store 12 floats per location: x, y, z, oriX, oriY (unused), oriZ (unused), padding (6 floats)
                locationValues.Add(loc.Position.X);
                locationValues.Add(loc.Position.Y);
                locationValues.Add(loc.Position.Z);
                locationValues.Add(loc.Facing); // oriX
                locationValues.Add(0.0f); // oriY (unused)
                locationValues.Add(0.0f); // oriZ (unused)
                for (int i = 0; i < 6; i++)
                {
                    locationValues.Add(0.0f); // padding
                }
            }
            // ValLocation: Binary float array (12 floats per location)
            if (locationValues.Count > 0)
            {
                byte[] locationBytes = new byte[locationValues.Count * 4];
                System.Buffer.BlockCopy(locationValues.ToArray(), 0, locationBytes, 0, locationBytes.Length);
                root.SetBinary("ValLocation", locationBytes);
            }

            // CatString list: Each entry has "Name" field
            var catStringList = root.Acquire<GFFList>("CatString", new GFFList());
            var stringValues = new List<string>();
            foreach (KeyValuePair<string, string> kvp in state.Strings)
            {
                GFFStruct entry = catStringList.Add();
                entry.SetString("Name", kvp.Key);
                stringValues.Add(kvp.Value ?? "");
            }
            // ValString: List of strings (indexed by CatString entry order)
            var valStringList = root.Acquire<GFFList>("ValString", new GFFList());
            foreach (string strValue in stringValues)
            {
                GFFStruct entry = valStringList.Add();
                entry.SetString("String", strValue);
            }

            return gff.ToBytes();
        }

        // Deserialize global variables from GFF format
        // Based on swkotor2.exe: FUN_005ac540 @ 0x005ac540 (called from FUN_005ac740 @ 0x005ac740)
        // Located via string reference: "GLOBALVARS" @ 0x007c27bc
        // Original implementation: Reads GFF with "GVT " signature, extracts catalog lists (CatBoolean, CatNumber, CatLocation, CatString)
        // and corresponding value arrays (ValBoolean, ValNumber, ValLocation, ValString)
        // Restores variables by matching catalog entry index with value array index
        private GlobalVariableState DeserializeGlobalVariables(byte[] data)
        {
            var state = new GlobalVariableState();

            if (data == null || data.Length < 12)
            {
                return state;
            }

            // Use CSharpKOTOR GFF reader
            try
            {
                GFF gff = GFF.FromBytes(data);
                if (gff == null || gff.Root == null)
                {
                    return state;
                }

                var root = gff.Root;

                // Read booleans: CatBoolean list + ValBoolean binary array
                GFFList catBooleanList = root.GetList("CatBoolean");
                byte[] valBoolean = root.GetBinary("ValBoolean");
                if (catBooleanList != null && valBoolean != null)
                {
                    int index = 0;
                    foreach (GFFStruct entry in catBooleanList)
                    {
                        if (index < valBoolean.Length)
                        {
                            string name = entry.GetString("Name");
                            byte value = valBoolean[index];
                            state.Booleans[name] = value != 0;
                            index++;
                        }
                    }
                }

                // Read numbers: CatNumber list + ValNumber binary array
                GFFList catNumberList = root.GetList("CatNumber");
                byte[] valNumber = root.GetBinary("ValNumber");
                if (catNumberList != null && valNumber != null)
                {
                    int index = 0;
                    foreach (GFFStruct entry in catNumberList)
                    {
                        if (index < valNumber.Length)
                        {
                            string name = entry.GetString("Name");
                            byte value = valNumber[index];
                            state.Numbers[name] = (int)value;
                            index++;
                        }
                    }
                }

                // Read locations: CatLocation list + ValLocation binary float array (12 floats per location)
                GFFList catLocationList = root.GetList("CatLocation");
                byte[] valLocation = root.GetBinary("ValLocation");
                if (catLocationList != null && valLocation != null)
                {
                    float[] locationFloats = new float[valLocation.Length / 4];
                    System.Buffer.BlockCopy(valLocation, 0, locationFloats, 0, valLocation.Length);
                    int floatIndex = 0;
                    foreach (GFFStruct entry in catLocationList)
                    {
                        if (floatIndex + 11 < locationFloats.Length)
                        {
                            string name = entry.GetString("Name");
                            float x = locationFloats[floatIndex];
                            float y = locationFloats[floatIndex + 1];
                            float z = locationFloats[floatIndex + 2];
                            float oriX = locationFloats[floatIndex + 3];
                            // Skip oriY, oriZ, and padding (floats 4-11)
                            floatIndex += 12;
                            var loc = new SavedLocation
                            {
                                AreaResRef = "", // Area ResRef not stored in global variables
                                Position = new Vector3(x, y, z),
                                Facing = oriX
                            };
                            state.Locations[name] = loc;
                        }
                    }
                }

                // Read strings: CatString list + ValString list
                GFFList catStringList = root.GetList("CatString");
                GFFList valStringList = root.GetList("ValString");
                if (catStringList != null && valStringList != null)
                {
                    int index = 0;
                    foreach (GFFStruct entry in catStringList)
                    {
                        if (index < valStringList.Count)
                        {
                            string name = entry.GetString("Name");
                            GFFStruct stringEntry = valStringList[index];
                            string value = stringEntry.GetString("String");
                            state.Strings[name] = value ?? "";
                            index++;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return empty state on error
            }

            return state;
        }

        #endregion

        #region Party Table

        // Serialize party table to GFF format
        // Based on swkotor2.exe: FUN_0057bd70 @ 0x0057bd70
        // Located via string reference: "PARTYTABLE" @ 0x007c1910
        // Original implementation (from decompiled FUN_0057bd70):
        // 1. Creates GFF with "PT  " signature (4 bytes) and "V2.0" version string
        // 2. Writes fields in this exact order:
        //    - PT_PCNAME (string): Player character name
        //    - PT_GOLD (int32): Gold/credits
        //    - PT_ITEM_COMPONENT (int32): Item component count
        //    - PT_ITEM_CHEMICAL (int32): Item chemical count
        //    - PT_SWOOP1, PT_SWOOP2, PT_SWOOP3 (int32): Swoop race times
        //    - PT_XP_POOL (float): Experience point pool
        //    - PT_PLAYEDSECONDS (int32): Total seconds played
        //    - PT_CONTROLLED_NPC (float): Currently controlled NPC ID (-1 if none)
        //    - PT_SOLOMODE (byte): Solo mode flag
        //    - PT_CHEAT_USED (byte): Cheat used flag
        //    - PT_NUM_MEMBERS (byte): Number of party members
        //    - PT_MEMBERS (list): Party member list, each entry has:
        //      - PT_MEMBER_ID (float): Member ID
        //      - PT_IS_LEADER (byte): Whether this member is the leader
        //    - PT_NUM_PUPPETS (byte): Number of puppets
        //    - PT_PUPPETS (list): Puppet list, each entry has:
        //      - PT_PUPPET_ID (float): Puppet ID
        //    - PT_AVAIL_PUPS (list): Available puppets (3 entries), each has:
        //      - PT_PUP_AVAIL (byte): Available flag
        //      - PT_PUP_SELECT (byte): Selected flag
        //    - PT_AVAIL_NPCS (list): Available NPCs (12 entries), each has:
        //      - PT_NPC_AVAIL (byte): Available flag
        //      - PT_NPC_SELECT (byte): Selected flag
        //    - PT_INFLUENCE (list): NPC influence values (12 entries), each has:
        //      - PT_NPC_INFLUENCE (float): Influence value
        //    - PT_AISTATE (float): AI state
        //    - PT_FOLLOWSTATE (float): Follow state
        //    - GlxyMap (struct): Galaxy map data:
        //      - GlxyMapNumPnts (int32): Number of points (16)
        //      - GlxyMapPlntMsk (int32): Planet mask bitmask
        //      - GlxyMapSelPnt (float): Selected point
        //    - PT_PAZAAKCARDS (list): Pazaak cards (23 entries), each has:
        //      - PT_PAZAAKCOUNT (float): Card count
        //    - PT_PAZSIDELIST (list): Pazaak side list (10 entries), each has:
        //      - PT_PAZSIDECARD (float): Side card value
        //    - PT_TUT_WND_SHOWN (bitmask): Tutorial window shown flags (33 bits)
        //    - PT_LAST_GUI_PNL (float): Last GUI panel ID
        //    - FORFEITVIOL (float): Forfeit violation
        //    - FORFEITCONDS (float): Forfeit conditions
        //    - PT_FB_MSG_LIST (list): Feedback message list, each entry has:
        //      - PT_FB_MSG_MSG (string): Message text
        //      - PT_FB_MSG_TYPE (int32): Message type
        //      - PT_FB_MSG_COLOR (byte): Message color
        //    - PT_DLG_MSG_LIST (list): Dialogue message list, each entry has:
        //      - PT_DLG_MSG_SPKR (string): Speaker name
        //      - PT_DLG_MSG_MSG (string): Message text
        //    - PT_COM_MSG_LIST (list): Combat message list, each entry has:
        //      - PT_COM_MSG_MSG (string): Message text
        //      - PT_COM_MSG_TYPE (int32): Message type
        //      - PT_COM_MSG_COOR (byte): Message color
        //    - PT_COST_MULT_LIST (list): Cost multiplier list, each entry has:
        //      - PT_COST_MULT_VALUE (float): Multiplier value
        //    - PT_DISABLEMAP (float): Disable map flag
        //    - PT_DISABLEREGEN (float): Disable regen flag
        // 3. File path: Constructs "PARTYTABLE" path in save directory
        // Note: GFF signature is "PT  " (4 bytes), version string is "V2.0"
        private byte[] SerializePartyTable(PartyState state)
        {
            if (state == null)
            {
                state = new PartyState();
            }

            // Use CSharpKOTOR GFF writer
            // Original creates GFF with "PT  " signature and "V2.0" version
            // Based on swkotor2.exe: FUN_0057bd70 @ 0x0057bd70 creates GFF with "PT  " signature
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Note: CSharpKOTOR GFFBinaryWriter always writes "V3.2" version, but signature is correct
            var gff = new GFF(GFFContent.PT);
            var root = gff.Root;

            // PT_PCNAME - Player character name
            string pcName = "";
            if (state.PlayerCharacter != null)
            {
                pcName = state.PlayerCharacter.Tag ?? "";
            }
            root.SetString("PT_PCNAME", pcName);
            
            // PT_GOLD - Gold/credits
            root.SetInt32("PT_GOLD", state.Gold);
            
            // PT_ITEM_COMPONENT - Item component count
            root.SetInt32("PT_ITEM_COMPONENT", state.ItemComponent);
            
            // PT_ITEM_CHEMICAL - Item chemical count
            root.SetInt32("PT_ITEM_CHEMICAL", state.ItemChemical);
            
            // PT_SWOOP1-3 - Swoop race times
            root.SetInt32("PT_SWOOP1", state.Swoop1);
            root.SetInt32("PT_SWOOP2", state.Swoop2);
            root.SetInt32("PT_SWOOP3", state.Swoop3);
            
            // PT_XP_POOL - Experience point pool (float)
            root.SetSingle("PT_XP_POOL", state.ExperiencePoints);
            
            // PT_PLAYEDSECONDS - Total seconds played
            root.SetInt32("PT_PLAYEDSECONDS", (int)state.PlayTime.TotalSeconds);
            
            // PT_CONTROLLED_NPC - Currently controlled NPC ID (float, -1 if none)
            root.SetSingle("PT_CONTROLLED_NPC", state.ControlledNPC >= 0 ? (float)state.ControlledNPC : -1.0f);
            
            // PT_SOLOMODE - Solo mode flag (byte)
            root.SetUInt8("PT_SOLOMODE", state.SoloMode ? (byte)1 : (byte)0);
            
            // PT_CHEAT_USED - Cheat used flag (byte)
            root.SetUInt8("PT_CHEAT_USED", state.CheatUsed ? (byte)1 : (byte)0);
            
            // PT_NUM_MEMBERS - Number of party members (byte)
            int numMembers = state.SelectedParty != null ? state.SelectedParty.Count : 0;
            root.SetUInt8("PT_NUM_MEMBERS", (byte)numMembers);
            
            // PT_MEMBERS - List of party members
            var membersList = root.Acquire<GFFList>("PT_MEMBERS", new GFFList());
            if (state.SelectedParty != null)
            {
                foreach (string memberResRef in state.SelectedParty)
                {
                    GFFStruct entry = membersList.Add();
                    // PT_MEMBER_ID - Member ID (float)
                    entry.SetSingle("PT_MEMBER_ID", GetMemberId(memberResRef));
                    // PT_IS_LEADER - Whether this member is the leader (byte)
                    bool isLeader = state.LeaderResRef == memberResRef;
                    entry.SetUInt8("PT_IS_LEADER", isLeader ? (byte)1 : (byte)0);
                }
            }
            
            // PT_NUM_PUPPETS - Number of puppets (byte)
            int numPuppets = state.Puppets != null ? state.Puppets.Count : 0;
            root.SetUInt8("PT_NUM_PUPPETS", (byte)numPuppets);
            
            // PT_PUPPETS - List of puppets
            var puppetsList = root.Acquire<GFFList>("PT_PUPPETS", new GFFList());
            if (state.Puppets != null)
            {
                foreach (uint puppetId in state.Puppets)
                {
                    GFFStruct entry = puppetsList.Add();
                    entry.SetSingle("PT_PUPPET_ID", (float)puppetId);
                }
            }
            
            // PT_AVAIL_PUPS - Available puppets list (3 entries)
            var availPupsList = root.Acquire<GFFList>("PT_AVAIL_PUPS", new GFFList());
            for (int i = 0; i < 3; i++)
            {
                GFFStruct entry = availPupsList.Add();
                bool available = state.AvailablePuppets != null && i < state.AvailablePuppets.Count && state.AvailablePuppets[i];
                entry.SetUInt8("PT_PUP_AVAIL", available ? (byte)1 : (byte)0);
                bool selectable = state.SelectablePuppets != null && i < state.SelectablePuppets.Count && state.SelectablePuppets[i];
                entry.SetUInt8("PT_PUP_SELECT", selectable ? (byte)1 : (byte)0);
            }

            // PT_AVAIL_NPCS - Available NPCs list (12 entries)
            GFFList availNpcsList = root.Acquire<GFFList>("PT_AVAIL_NPCS", new GFFList());
            List<PartyMemberState> memberList = state.AvailableMembers != null ? new List<PartyMemberState>(state.AvailableMembers.Values) : new List<PartyMemberState>();
            for (int i = 0; i < 12; i++)
            {
                GFFStruct entry = availNpcsList.Add();
                bool available = i < memberList.Count;
                entry.SetUInt8("PT_NPC_AVAIL", available ? (byte)1 : (byte)0);
                bool selectable = available && memberList[i].IsSelectable;
                entry.SetUInt8("PT_NPC_SELECT", selectable ? (byte)1 : (byte)0);
            }
            
            // PT_INFLUENCE - Influence values list (12 entries)
            var influenceList = root.Acquire<GFFList>("PT_INFLUENCE", new GFFList());
            for (int i = 0; i < 12; i++)
            {
                GFFStruct entry = influenceList.Add();
                float influence = 0.0f;
                if (state.Influence != null && i < state.Influence.Count)
                {
                    influence = (float)state.Influence[i];
                }
                entry.SetSingle("PT_NPC_INFLUENCE", influence);
            }
            
            // PT_AISTATE - AI state (float)
            root.SetSingle("PT_AISTATE", (float)state.AIState);
            
            // PT_FOLLOWSTATE - Follow state (float)
            root.SetSingle("PT_FOLLOWSTATE", (float)state.FollowState);
            
            // GlxyMap - Galaxy map data
            var glxyMapStruct = root.Acquire<GFFStruct>("GlxyMap", new GFFStruct());
            glxyMapStruct.SetInt32("GlxyMapNumPnts", 16); // Always 16 points
            glxyMapStruct.SetInt32("GlxyMapPlntMsk", state.GalaxyMapPlanetMask);
            glxyMapStruct.SetSingle("GlxyMapSelPnt", (float)state.GalaxyMapSelectedPoint);
            
            // PT_PAZAAKCARDS - Pazaak cards list (23 entries)
            var pazaakCardsList = root.Acquire<GFFList>("PT_PAZAAKCARDS", new GFFList());
            for (int i = 0; i < 23; i++)
            {
                GFFStruct entry = pazaakCardsList.Add();
                int count = 0;
                if (state.PazaakCards != null && i < state.PazaakCards.Count)
                {
                    count = state.PazaakCards[i];
                }
                entry.SetSingle("PT_PAZAAKCOUNT", (float)count);
            }
            
            // PT_PAZSIDELIST - Pazaak side list (10 entries)
            var pazaakSideList = root.Acquire<GFFList>("PT_PAZSIDELIST", new GFFList());
            for (int i = 0; i < 10; i++)
            {
                GFFStruct entry = pazaakSideList.Add();
                int card = 0;
                if (state.PazaakSideList != null && i < state.PazaakSideList.Count)
                {
                    card = state.PazaakSideList[i];
                }
                entry.SetSingle("PT_PAZSIDECARD", (float)card);
            }
            
            // PT_TUT_WND_SHOWN - Tutorial windows shown (array of 33 bytes)
            if (state.TutorialWindowsShown != null)
            {
                byte[] tutArray = new byte[33];
                for (int i = 0; i < 33 && i < state.TutorialWindowsShown.Count; i++)
                {
                    tutArray[i] = state.TutorialWindowsShown[i] ? (byte)1 : (byte)0;
                }
                root.SetBinary("PT_TUT_WND_SHOWN", tutArray);
            }
            
            // PT_LAST_GUI_PNL - Last GUI panel (float)
            root.SetSingle("PT_LAST_GUI_PNL", (float)state.LastGUIPanel);
            
            // PT_FB_MSG_LIST - Feedback message list
            var fbMsgList = root.Acquire<GFFList>("PT_FB_MSG_LIST", new GFFList());
            if (state.FeedbackMessages != null)
            {
                foreach (var msg in state.FeedbackMessages)
                {
                    GFFStruct entry = fbMsgList.Add();
                    entry.SetString("PT_FB_MSG_MSG", msg.Message ?? "");
                    entry.SetInt32("PT_FB_MSG_TYPE", msg.Type);
                    entry.SetUInt8("PT_FB_MSG_COLOR", msg.Color);
                }
            }
            
            // PT_DLG_MSG_LIST - Dialogue message list
            var dlgMsgList = root.Acquire<GFFList>("PT_DLG_MSG_LIST", new GFFList());
            if (state.DialogueMessages != null)
            {
                foreach (var msg in state.DialogueMessages)
                {
                    GFFStruct entry = dlgMsgList.Add();
                    entry.SetString("PT_DLG_MSG_SPKR", msg.Speaker ?? "");
                    entry.SetString("PT_DLG_MSG_MSG", msg.Message ?? "");
                }
            }
            
            // PT_COM_MSG_LIST - Combat message list
            var comMsgList = root.Acquire<GFFList>("PT_COM_MSG_LIST", new GFFList());
            if (state.CombatMessages != null)
            {
                foreach (var msg in state.CombatMessages)
                {
                    GFFStruct entry = comMsgList.Add();
                    entry.SetString("PT_COM_MSG_MSG", msg.Message ?? "");
                    entry.SetInt32("PT_COM_MSG_TYPE", msg.Type);
                    entry.SetUInt8("PT_COM_MSG_COOR", msg.Color);
                }
            }
            
            // PT_COST_MULT_LIST - Cost multiplier list
            GFFList costMultList = root.Acquire<GFFList>("PT_COST_MULT_LIST", new GFFList());
            if (state.CostMultipliers != null)
            {
                foreach (var mult in state.CostMultipliers)
                {
                    GFFStruct entry = costMultList.Add();
                    entry.SetSingle("PT_COST_MULT_VALUE", mult);
                }
            }
            
            // PT_DISABLEMAP - Disable map flag (float)
            root.SetSingle("PT_DISABLEMAP", state.DisableMap ? 1.0f : 0.0f);
            
            // PT_DISABLEREGEN - Disable regen flag (float)
            root.SetSingle("PT_DISABLEREGEN", state.DisableRegen ? 1.0f : 0.0f);

            return gff.ToBytes();
        }
        
        // Helper to get member ID from ResRef
        // Member IDs: -1 = Player, 0-8 = NPC slots (K1), 0-11 = NPC slots (K2)
        // Based on nwscript.nss constants: NPC_PLAYER = -1, NPC_BASTILA = 0, etc.
        private float GetMemberId(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                return -1.0f; // Default to player
            }

            // Player character is typically identified by specific ResRefs or -1
            // Common player ResRefs: "player", "pc", etc.
            string resRefLower = resRef.ToLowerInvariant();
            if (resRefLower == "player" || resRefLower == "pc" || resRefLower.StartsWith("pc_"))
            {
                return -1.0f; // NPC_PLAYER
            }

            // Map common K1 NPC ResRefs to member IDs
            // This mapping should ideally come from party.2da or game data
            // For now, use common ResRef patterns
            var npcMapping = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
            {
                // K1 NPCs (0-8)
                { "bastila", 0.0f },      // NPC_BASTILA
                { "canderous", 1.0f },    // NPC_CANDEROUS
                { "carth", 2.0f },        // NPC_CARTH
                { "hk47", 3.0f },         // NPC_HK_47
                { "jolee", 4.0f },        // NPC_JOLEE
                { "juhani", 5.0f },      // NPC_JUHANI
                { "mission", 6.0f },     // NPC_MISSION
                { "t3m4", 7.0f },        // NPC_T3_M4
                { "zaalbar", 8.0f },     // NPC_ZAALBAR
                
                // K2 NPCs (0-11) - some overlap with K1
                { "atton", 0.0f },       // K2 NPC_ATTON
                { "bao-dur", 1.0f },     // K2 NPC_BAO_DUR
                { "disciple", 2.0f },    // K2 NPC_DISCIPLE
                { "handmaiden", 3.0f },  // K2 NPC_HANDMAIDEN
                { "hanharr", 4.0f },     // K2 NPC_HANHARR
                { "g0-t0", 5.0f },       // K2 NPC_G0_T0
                { "kreia", 6.0f },       // K2 NPC_KREIA
                { "mira", 7.0f },        // K2 NPC_MIRA
                { "visas", 8.0f },       // K2 NPC_VISAS
                { "mandalore", 9.0f },   // K2 NPC_MANDALORE
                { "t3m4", 10.0f },       // K2 NPC_T3_M4 (different slot)
                { "sion", 11.0f },       // K2 NPC_SION
            };

            // Try exact match first
            if (npcMapping.TryGetValue(resRefLower, out float memberId))
            {
                return memberId;
            }

            // Try partial match (e.g., "bastila" matches "bastila_001")
            foreach (var kvp in npcMapping)
            {
                if (resRefLower.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            // Try to load from party.2da if GameDataManager is available
            // Based on swkotor2.exe party.2da system
            // Located via string references: "party.2da" @ 0x007c1910, "PARTYTABLE" @ 0x007c1910
            // Original implementation: party.2da maps NPC ResRefs to member IDs (row index = member ID)
            // party.2da columns: Label (ResRef), Name (display name), PortraitId, etc.
            if (_gameDataManager != null)
            {
                // Use dynamic to call GetTable without referencing Odyssey.Kotor
                dynamic gameDataManager = _gameDataManager;
                AuroraEngine.Common.Formats.TwoDA.TwoDA partyTable = gameDataManager.GetTable("party");
                if (partyTable != null)
                {
                    // Search party.2da for matching ResRef
                    // Row index in party.2da corresponds to member ID
                    for (int i = 0; i < partyTable.GetHeight(); i++)
                    {
                        AuroraEngine.Common.Formats.TwoDA.TwoDARow row = partyTable.GetRow(i);
                        string rowLabel = row.Label();
                        
                        // Check exact match
                        if (string.Equals(rowLabel, resRefLower, StringComparison.OrdinalIgnoreCase))
                        {
                            return (float)i;
                        }
                        
                        // Check partial match (e.g., "bastila" matches "bastila_001")
                        if (!string.IsNullOrEmpty(rowLabel) && resRefLower.Contains(rowLabel.ToLowerInvariant()))
                        {
                            return (float)i;
                        }
                    }
                }
            }

            // If no mapping found, return 0.0f as default
            // Fallback to hardcoded mapping if party.2da not available
            return 0.0f;
        }

        // Deserialize party table from GFF format
        // Based on swkotor2.exe: FUN_0057dcd0 @ 0x0057dcd0
        // Located via string reference: "PARTYTABLE" @ 0x007c1910
        // Original implementation: Reads GFF with "PT  " signature, extracts all party-related fields including
        // party members, puppets, available NPCs, influence values, gold, XP pool, solo mode, cheat flags,
        // galaxy map state, Pazaak cards, tutorial windows, message lists, cost multipliers, and various game state flags
        private PartyState DeserializePartyTable(byte[] data)
        {
            var state = new PartyState();

            if (data == null || data.Length < 12)
            {
                return state;
            }

            // Use CSharpKOTOR GFF reader
            try
            {
                GFF gff = GFF.FromBytes(data);
                if (gff == null || gff.Root == null)
                {
                    return state;
                }

                var root = gff.Root;

                // PT_PCNAME - Player character name
                string pcName = root.GetString("PT_PCNAME");
                if (state.PlayerCharacter == null)
                {
                    state.PlayerCharacter = new CreatureState();
                }
                state.PlayerCharacter.Tag = pcName;

                // PT_GOLD - Gold/credits
                state.Gold = root.GetInt32("PT_GOLD");

                // PT_ITEM_COMPONENT - Item component count
                state.ItemComponent = root.GetInt32("PT_ITEM_COMPONENT");

                // PT_ITEM_CHEMICAL - Item chemical count
                state.ItemChemical = root.GetInt32("PT_ITEM_CHEMICAL");

                // PT_SWOOP1-3 - Swoop race times
                state.Swoop1 = root.GetInt32("PT_SWOOP1");
                state.Swoop2 = root.GetInt32("PT_SWOOP2");
                state.Swoop3 = root.GetInt32("PT_SWOOP3");

                // PT_XP_POOL - Experience point pool (float)
                state.ExperiencePoints = (int)root.GetSingle("PT_XP_POOL");

                // PT_PLAYEDSECONDS - Total seconds played (int32, fallback to PT_PLAYEDMINUTES * 60)
                int seconds = root.GetInt32("PT_PLAYEDSECONDS");
                if (seconds == 0)
                {
                    int minutes = root.GetInt32("PT_PLAYEDMINUTES");
                    if (minutes > 0)
                    {
                        seconds = minutes * 60;
                    }
                }
                state.PlayTime = TimeSpan.FromSeconds(seconds);

                // PT_CONTROLLED_NPC - Currently controlled NPC ID (float, -1 if none)
                float controlledNPC = root.GetSingle("PT_CONTROLLED_NPC");
                state.ControlledNPC = controlledNPC >= 0 ? (int)controlledNPC : -1;

                // PT_SOLOMODE - Solo mode flag (byte)
                state.SoloMode = root.GetUInt8("PT_SOLOMODE") != 0;

                // PT_CHEAT_USED - Cheat used flag (byte)
                state.CheatUsed = root.GetUInt8("PT_CHEAT_USED") != 0;

                // PT_NUM_MEMBERS - Number of party members (byte)
                byte numMembers = root.GetUInt8("PT_NUM_MEMBERS");

                // PT_MEMBERS - List of party members
                GFFList membersList = root.GetList("PT_MEMBERS");
                if (membersList != null)
                {
                    int memberCount = Math.Min(numMembers, membersList.Count);
                    for (int i = 0; i < memberCount; i++)
                    {
                        GFFStruct entry = membersList[i];
                        float memberId = entry.GetSingle("PT_MEMBER_ID");
                        bool isLeader = entry.GetUInt8("PT_IS_LEADER") != 0;

                        // Note: Member ID would need to be mapped to ResRef
                        // For now, store as placeholder - actual implementation would need ResRef mapping
                        if (isLeader)
                        {
                            // Would set leader based on member ID
                        }
                    }
                }

                // PT_NUM_PUPPETS - Number of puppets (byte)
                byte numPuppets = root.GetUInt8("PT_NUM_PUPPETS");

                // PT_PUPPETS - List of puppets
                GFFList puppetsList = root.GetList("PT_PUPPETS");
                if (puppetsList != null)
                {
                    int puppetCount = Math.Min(numPuppets, puppetsList.Count);
                    for (int i = 0; i < puppetCount; i++)
                    {
                        GFFStruct entry = puppetsList[i];
                        float puppetId = entry.GetSingle("PT_PUPPET_ID");
                        state.Puppets.Add((uint)puppetId);
                    }
                }

                // PT_AVAIL_PUPS - Available puppets list (3 entries)
                GFFList availPupsList = root.GetList("PT_AVAIL_PUPS");
                if (availPupsList != null)
                {
                    int count = Math.Min(3, availPupsList.Count);
                    for (int i = 0; i < count; i++)
                    {
                        GFFStruct entry = availPupsList[i];
                        bool available = entry.GetUInt8("PT_PUP_AVAIL") != 0;
                        bool selectable = entry.GetUInt8("PT_PUP_SELECT") != 0;

                        if (i < state.AvailablePuppets.Count)
                        {
                            state.AvailablePuppets[i] = available;
                        }
                        else
                        {
                            state.AvailablePuppets.Add(available);
                        }

                        if (i < state.SelectablePuppets.Count)
                        {
                            state.SelectablePuppets[i] = selectable;
                        }
                        else
                        {
                            state.SelectablePuppets.Add(selectable);
                        }
                    }
                }

                // PT_AVAIL_NPCS - Available NPCs list (12 entries)
                GFFList availNpcsList = root.GetList("PT_AVAIL_NPCS");
                if (availNpcsList != null)
                {
                    int count = Math.Min(12, availNpcsList.Count);
                    for (int i = 0; i < count; i++)
                    {
                        GFFStruct entry = availNpcsList[i];
                        bool available = entry.GetUInt8("PT_NPC_AVAIL") != 0;
                        bool selectable = entry.GetUInt8("PT_NPC_SELECT") != 0;

                        // Note: Would need to map index to NPC ResRef
                        // For now, create placeholder entries
                        string npcResRef = string.Format("NPC_{0:D2}", i);
                        if (!state.AvailableMembers.ContainsKey(npcResRef))
                        {
                            state.AvailableMembers[npcResRef] = new PartyMemberState
                            {
                                TemplateResRef = npcResRef
                            };
                        }
                        state.AvailableMembers[npcResRef].IsAvailable = available;
                        state.AvailableMembers[npcResRef].IsSelectable = selectable;
                    }
                }

                // PT_INFLUENCE - Influence values list (12 entries)
                GFFList influenceList = root.GetList("PT_INFLUENCE");
                if (influenceList != null)
                {
                    int count = Math.Min(12, influenceList.Count);
                    for (int i = 0; i < count; i++)
                    {
                        GFFStruct entry = influenceList[i];
                        float influence = entry.GetSingle("PT_NPC_INFLUENCE");

                        if (i < state.Influence.Count)
                        {
                            state.Influence[i] = (int)influence;
                        }
                        else
                        {
                            state.Influence.Add((int)influence);
                        }
                    }
                }

                // PT_AISTATE - AI state (float)
                state.AIState = (int)root.GetSingle("PT_AISTATE");

                // PT_FOLLOWSTATE - Follow state (float)
                state.FollowState = (int)root.GetSingle("PT_FOLLOWSTATE");

                // GlxyMap - Galaxy map data
                if (root.Exists("GlxyMap"))
                {
                    GFFStruct glxyMapStruct = root.GetStruct("GlxyMap");
                    if (glxyMapStruct != null)
                    {
                        int numPnts = glxyMapStruct.GetInt32("GlxyMapNumPnts");
                        state.GalaxyMapPlanetMask = glxyMapStruct.GetInt32("GlxyMapPlntMsk");
                        state.GalaxyMapSelectedPoint = (int)glxyMapStruct.GetSingle("GlxyMapSelPnt");
                    }
                }

                // PT_PAZAAKCARDS - Pazaak cards list (23 entries)
                GFFList pazaakCardsList = root.GetList("PT_PAZAAKCARDS");
                if (pazaakCardsList != null)
                {
                    int count = Math.Min(23, pazaakCardsList.Count);
                    for (int i = 0; i < count; i++)
                    {
                        GFFStruct entry = pazaakCardsList[i];
                        float countValue = entry.GetSingle("PT_PAZAAKCOUNT");

                        if (i < state.PazaakCards.Count)
                        {
                            state.PazaakCards[i] = (int)countValue;
                        }
                        else
                        {
                            state.PazaakCards.Add((int)countValue);
                        }
                    }
                }

                // PT_PAZSIDELIST - Pazaak side list (10 entries)
                GFFList pazaakSideList = root.GetList("PT_PAZSIDELIST");
                if (pazaakSideList != null)
                {
                    int count = Math.Min(10, pazaakSideList.Count);
                    for (int i = 0; i < count; i++)
                    {
                        GFFStruct entry = pazaakSideList[i];
                        float card = entry.GetSingle("PT_PAZSIDECARD");

                        if (i < state.PazaakSideList.Count)
                        {
                            state.PazaakSideList[i] = (int)card;
                        }
                        else
                        {
                            state.PazaakSideList.Add((int)card);
                        }
                    }
                }

                // PT_TUT_WND_SHOWN - Tutorial windows shown (array of 33 bytes)
                byte[] tutArray = root.GetBinary("PT_TUT_WND_SHOWN");
                if (tutArray != null)
                {
                    int count = Math.Min(33, tutArray.Length);
                    for (int i = 0; i < count; i++)
                    {
                        bool shown = tutArray[i] != 0;
                        if (i < state.TutorialWindowsShown.Count)
                        {
                            state.TutorialWindowsShown[i] = shown;
                        }
                        else
                        {
                            state.TutorialWindowsShown.Add(shown);
                        }
                    }
                }

                // PT_LAST_GUI_PNL - Last GUI panel (float)
                state.LastGUIPanel = (int)root.GetSingle("PT_LAST_GUI_PNL");

                // PT_FB_MSG_LIST - Feedback message list
                GFFList fbMsgList = root.GetList("PT_FB_MSG_LIST");
                if (fbMsgList != null)
                {
                    foreach (GFFStruct entry in fbMsgList)
                    {
                        var msg = new FeedbackMessage
                        {
                            Message = entry.GetString("PT_FB_MSG_MSG"),
                            Type = entry.GetInt32("PT_FB_MSG_TYPE"),
                            Color = entry.GetUInt8("PT_FB_MSG_COLOR")
                        };
                        state.FeedbackMessages.Add(msg);
                    }
                }

                // PT_DLG_MSG_LIST - Dialogue message list
                GFFList dlgMsgList = root.GetList("PT_DLG_MSG_LIST");
                if (dlgMsgList != null)
                {
                    foreach (GFFStruct entry in dlgMsgList)
                    {
                        var msg = new DialogueMessage
                        {
                            Speaker = entry.GetString("PT_DLG_MSG_SPKR"),
                            Message = entry.GetString("PT_DLG_MSG_MSG")
                        };
                        state.DialogueMessages.Add(msg);
                    }
                }

                // PT_COM_MSG_LIST - Combat message list
                GFFList comMsgList = root.GetList("PT_COM_MSG_LIST");
                if (comMsgList != null)
                {
                    foreach (GFFStruct entry in comMsgList)
                    {
                        var msg = new CombatMessage
                        {
                            Message = entry.GetString("PT_COM_MSG_MSG"),
                            Type = entry.GetInt32("PT_COM_MSG_TYPE"),
                            Color = entry.GetUInt8("PT_COM_MSG_COOR")
                        };
                        state.CombatMessages.Add(msg);
                    }
                }

                // PT_COST_MULT_LIST - Cost multiplier list
                GFFList costMultList = root.GetList("PT_COST_MULT_LIST");
                if (costMultList != null)
                {
                    foreach (GFFStruct entry in costMultList)
                    {
                        float mult = entry.GetSingle("PT_COST_MULT_VALUE");
                        state.CostMultipliers.Add(mult);
                    }
                }

                // PT_DISABLEMAP - Disable map flag (float)
                state.DisableMap = root.GetSingle("PT_DISABLEMAP") != 0.0f;

                // PT_DISABLEREGEN - Disable regen flag (float)
                state.DisableRegen = root.GetSingle("PT_DISABLEREGEN") != 0.0f;
            }
            catch (Exception)
            {
                // Return empty state on error
            }

            return state;
        }

        #endregion

        #region Area State

        private byte[] SerializeAreaState(AreaState state)
        {
            if (state == null)
            {
                return new byte[0];
            }

            using (var ms = new MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // GFF header
                writer.Write(Encoding.ASCII.GetBytes("GFF "));
                writer.Write(Encoding.ASCII.GetBytes("V3.2"));
                writer.Write((uint)0); // Type

                WriteGffString(writer, "AREARES", state.AreaResRef ?? "");
                writer.Write(state.Visited ? (byte)1 : (byte)0);

                // Write entity states
                SerializeEntityStates(writer, "CREATURES", state.CreatureStates);
                SerializeEntityStates(writer, "DOORS", state.DoorStates);
                SerializeEntityStates(writer, "PLACEABLES", state.PlaceableStates);
                SerializeEntityStates(writer, "TRIGGERS", state.TriggerStates);

                // Write destroyed entity IDs
                writer.Write((uint)state.DestroyedEntityIds.Count);
                foreach (uint id in state.DestroyedEntityIds)
                {
                    writer.Write(id);
                }

                return ms.ToArray();
            }
        }

        private AreaState DeserializeAreaState(byte[] data)
        {
            var state = new AreaState();

            if (data == null || data.Length < 12)
            {
                return state;
            }

            // Parse GFF using CSharpKOTOR
            try
            {
                GFF gff = GFF.FromBytes(data);
                GFFStruct root = gff.Root;

                // Area ResRef
                if (root.Exists("AreaResRef"))
                {
                    state.AreaResRef = root.GetString("AreaResRef") ?? "";
                }

                // Visited flag
                if (root.Exists("Visited"))
                {
                    state.Visited = root.GetUInt8("Visited") != 0;
                }

                // Deserialize entity state lists
                DeserializeEntityStateList(root, "CreatureList", state.CreatureStates);
                DeserializeEntityStateList(root, "DoorList", state.DoorStates);
                DeserializeEntityStateList(root, "PlaceableList", state.PlaceableStates);
                DeserializeEntityStateList(root, "TriggerList", state.TriggerStates);
                DeserializeEntityStateList(root, "StoreList", state.StoreStates);
                DeserializeEntityStateList(root, "SoundList", state.SoundStates);
                DeserializeEntityStateList(root, "WaypointList", state.WaypointStates);
                DeserializeEntityStateList(root, "EncounterList", state.EncounterStates);
                DeserializeEntityStateList(root, "CameraList", state.CameraStates);

                // Destroyed entity IDs
                if (root.Exists("DestroyedList"))
                {
                    GFFList destroyedList = root.GetList("DestroyedList");
                    if (destroyedList != null)
                    {
                        foreach (GFFStruct item in destroyedList)
                        {
                            if (item.Exists("ObjectId"))
                            {
                                state.DestroyedEntityIds.Add((uint)item.GetUInt32("ObjectId"));
                            }
                        }
                    }
                }

                // Spawned entities
                if (root.Exists("SpawnedList"))
                {
                    GFFList spawnedList = root.GetList("SpawnedList");
                    if (spawnedList != null)
                    {
                        foreach (GFFStruct item in spawnedList)
                        {
                            var spawnedState = new SpawnedEntityState();
                            DeserializeEntityState(item, spawnedState);
                            if (item.Exists("BlueprintResRef"))
                            {
                                spawnedState.BlueprintResRef = item.GetString("BlueprintResRef") ?? "";
                            }
                            if (item.Exists("SpawnedBy"))
                            {
                                spawnedState.SpawnedBy = item.GetString("SpawnedBy") ?? "";
                            }
                            state.SpawnedEntities.Add(spawnedState);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[SaveSerializer] Failed to deserialize area state: " + ex.Message);
            }

            return state;
        }

        private void DeserializeEntityStateList(GFFStruct root, string listName, List<EntityState> targetList)
        {
            if (!root.Exists(listName))
            {
                return;
            }

            GFFList list = root.GetList(listName);
            if (list == null)
            {
                return;
            }

            foreach (GFFStruct item in list)
            {
                var entityState = new EntityState();
                DeserializeEntityState(item, entityState);
                targetList.Add(entityState);
            }
        }

        private void DeserializeEntityState(GFFStruct structData, EntityState state)
        {
            // Tag
            if (structData.Exists("Tag"))
            {
                state.Tag = structData.GetString("Tag") ?? "";
            }

            // ObjectId
            if (structData.Exists("ObjectId"))
            {
                state.ObjectId = (uint)structData.GetUInt32("ObjectId");
            }

            // ObjectType
            if (structData.Exists("ObjectType"))
            {
                state.ObjectType = (Odyssey.Core.Enums.ObjectType)(int)structData.GetUInt32("ObjectType");
            }

            // TemplateResRef
            if (structData.Exists("TemplateResRef"))
            {
                ResRef resRef = structData.GetResRef("TemplateResRef");
                if (resRef != null)
                {
                    state.TemplateResRef = resRef.ToString();
                }
            }

            // Position
            if (structData.Exists("Position"))
            {
                Vector3 pos = structData.GetVector3("Position");
                state.Position = new System.Numerics.Vector3(pos.X, pos.Y, pos.Z);
            }

            // Facing
            if (structData.Exists("Facing"))
            {
                state.Facing = structData.GetSingle("Facing");
            }

            // HP
            if (structData.Exists("CurrentHP"))
            {
                state.CurrentHP = structData.GetInt32("CurrentHP");
            }
            if (structData.Exists("MaxHP"))
            {
                state.MaxHP = structData.GetInt32("MaxHP");
            }

            // Flags
            if (structData.Exists("IsDestroyed"))
            {
                state.IsDestroyed = structData.GetUInt8("IsDestroyed") != 0;
            }
            if (structData.Exists("IsPlot"))
            {
                state.IsPlot = structData.GetUInt8("IsPlot") != 0;
            }
            if (structData.Exists("IsOpen"))
            {
                state.IsOpen = structData.GetUInt8("IsOpen") != 0;
            }
            if (structData.Exists("IsLocked"))
            {
                state.IsLocked = structData.GetUInt8("IsLocked") != 0;
            }
            if (structData.Exists("AnimationState"))
            {
                state.AnimationState = structData.GetInt32("AnimationState");
            }

            // Local variables
            if (structData.Exists("LocalVars"))
            {
                DeserializeLocalVariables(structData.GetStruct("LocalVars"), state.LocalVariables);
            }

            // Active effects
            if (structData.Exists("Effects"))
            {
                GFFList effectsList = structData.GetList("Effects");
                if (effectsList != null)
                {
                    foreach (GFFStruct effectStruct in effectsList)
                    {
                        var effect = new SavedEffect();
                        if (effectStruct.Exists("EffectType"))
                        {
                            effect.EffectType = effectStruct.GetInt32("EffectType");
                        }
                        if (effectStruct.Exists("SubType"))
                        {
                            effect.SubType = effectStruct.GetInt32("SubType");
                        }
                        if (effectStruct.Exists("DurationType"))
                        {
                            effect.DurationType = effectStruct.GetInt32("DurationType");
                        }
                        if (effectStruct.Exists("RemainingDuration"))
                        {
                            effect.RemainingDuration = effectStruct.GetSingle("RemainingDuration");
                        }
                        if (effectStruct.Exists("CreatorId"))
                        {
                            effect.CreatorId = (uint)effectStruct.GetUInt32("CreatorId");
                        }
                        if (effectStruct.Exists("SpellId"))
                        {
                            effect.SpellId = effectStruct.GetInt32("SpellId");
                        }
                        state.ActiveEffects.Add(effect);
                    }
                }
            }
        }

        private void DeserializeLocalVariables(GFFStruct varsStruct, LocalVariableSet target)
        {
            if (varsStruct == null)
            {
                return;
            }

            // Int variables
            if (varsStruct.Exists("Ints"))
            {
                GFFList intList = varsStruct.GetList("Ints");
                if (intList != null)
                {
                    foreach (GFFStruct item in intList)
                    {
                        if (item.Exists("Name") && item.Exists("Value"))
                        {
                            target.Ints[item.GetString("Name") ?? ""] = item.GetInt32("Value");
                        }
                    }
                }
            }

            // Float variables
            if (varsStruct.Exists("Floats"))
            {
                GFFList floatList = varsStruct.GetList("Floats");
                if (floatList != null)
                {
                    foreach (GFFStruct item in floatList)
                    {
                        if (item.Exists("Name") && item.Exists("Value"))
                        {
                            target.Floats[item.GetString("Name") ?? ""] = item.GetSingle("Value");
                        }
                    }
                }
            }

            // String variables
            if (varsStruct.Exists("Strings"))
            {
                GFFList stringList = varsStruct.GetList("Strings");
                if (stringList != null)
                {
                    foreach (GFFStruct item in stringList)
                    {
                        if (item.Exists("Name") && item.Exists("Value"))
                        {
                            target.Strings[item.GetString("Name") ?? ""] = item.GetString("Value") ?? "";
                        }
                    }
                }
            }

            // Object variables
            if (varsStruct.Exists("Objects"))
            {
                GFFList objectList = varsStruct.GetList("Objects");
                if (objectList != null)
                {
                    foreach (GFFStruct item in objectList)
                    {
                        if (item.Exists("Name") && item.Exists("Value"))
                        {
                            target.Objects[item.GetString("Name") ?? ""] = (uint)item.GetUInt32("Value");
                        }
                    }
                }
            }
        }

        private void SerializeEntityStates(System.IO.BinaryWriter writer, string label, List<EntityState> states)
        {
            writer.Write((uint)states.Count);
            foreach (EntityState entityState in states)
            {
                SerializeEntityState(writer, entityState);
            }
        }

        private void SerializeEntityState(System.IO.BinaryWriter writer, EntityState state)
        {
            WriteGffString(writer, "TAG", state.Tag ?? "");
            writer.Write(state.ObjectId);
            writer.Write((int)state.ObjectType);

            // Position
            writer.Write(state.Position.X);
            writer.Write(state.Position.Y);
            writer.Write(state.Position.Z);

            // Facing
            writer.Write(state.Facing);

            // HP
            writer.Write(state.CurrentHP);
            writer.Write(state.MaxHP);

            // Flags
            writer.Write(state.IsDestroyed ? (byte)1 : (byte)0);
            writer.Write(state.IsPlot ? (byte)1 : (byte)0);
            writer.Write(state.IsOpen ? (byte)1 : (byte)0);
            writer.Write(state.IsLocked ? (byte)1 : (byte)0);
            writer.Write(state.AnimationState);
        }

        #endregion

        #region GFF Helpers

        private void WriteGffString(System.IO.BinaryWriter writer, string label, string value)
        {
            // Write label length and label
            byte[] labelBytes = Encoding.ASCII.GetBytes(label);
            writer.Write((byte)labelBytes.Length);
            writer.Write(labelBytes);

            // Write value length and value
            byte[] valueBytes = Encoding.UTF8.GetBytes(value ?? "");
            writer.Write((ushort)valueBytes.Length);
            writer.Write(valueBytes);
        }

        private void WriteGffInt(System.IO.BinaryWriter writer, string label, int value)
        {
            byte[] labelBytes = Encoding.ASCII.GetBytes(label);
            writer.Write((byte)labelBytes.Length);
            writer.Write(labelBytes);
            writer.Write(value);
        }

        private string ReadGffString(System.IO.BinaryReader reader, string expectedLabel)
        {
            try
            {
                byte labelLength = reader.ReadByte();
                byte[] labelBytes = reader.ReadBytes(labelLength);
                string label = Encoding.ASCII.GetString(labelBytes);

                ushort valueLength = reader.ReadUInt16();
                byte[] valueBytes = reader.ReadBytes(valueLength);
                return Encoding.UTF8.GetString(valueBytes);
            }
            catch
            {
                return "";
            }
        }

        private int ReadGffInt(System.IO.BinaryReader reader, string expectedLabel)
        {
            try
            {
                byte labelLength = reader.ReadByte();
                byte[] labelBytes = reader.ReadBytes(labelLength);
                return reader.ReadInt32();
            }
            catch
            {
                return 0;
            }
        }

        #endregion
    }
}
