using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Andastra.Parsing.Common;
using Andastra.Parsing.Extract.SaveData;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.UTC;
using Andastra.Runtime.Core.Save;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace Andastra.Runtime.Content.Save
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
        private string _savesDirectory;

        /// <summary>
        /// Creates a new save serializer.
        /// </summary>
        /// <param name="gameDataManager">Optional game data manager for party.2da lookups.</param>
        public SaveSerializer(object gameDataManager = null)
        {
            _gameDataManager = gameDataManager;
            _savesDirectory = null;
        }

        /// <summary>
        /// Sets the saves directory for path resolution.
        /// When set, GetSavePathFromData can construct save paths from save names.
        /// </summary>
        /// <param name="savesDirectory">Base directory for save games.</param>
        public void SetSavesDirectory(string savesDirectory)
        {
            _savesDirectory = savesDirectory;
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
        // Based on swkotor2.exe: SerializeSaveNfo @ 0x004eb750
        // Located via string reference: "savenfo" @ 0x007be1f0
        // Original implementation (from decompiled SerializeSaveNfo):
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
            if (saveData == null) throw new ArgumentNullException(nameof(saveData));

            var nfo = new NFOData
            {
                AreaName = saveData.CurrentAreaName ?? string.Empty,
                LastModule = saveData.CurrentModule ?? string.Empty,
                SavegameName = saveData.Name ?? string.Empty,
                TimePlayedSeconds = (int)saveData.PlayTime.TotalSeconds,
                CheatUsed = saveData.CheatUsed,
                GameplayHint = (byte)(saveData.GameplayHint ? 1 : 0),
                PcName = saveData.PlayerName ?? string.Empty,
            };

            // Timestamp stored as FILETIME (uint64) in many saves.
            long ft = saveData.SaveTime.ToFileTime();
            if (ft > 0)
            {
                nfo.TimestampFileTime = (ulong)ft;
            }

            // Story hints (0..9)
            if (saveData.StoryHints != null)
            {
                for (int i = 0; i < 10 && i < saveData.StoryHints.Count; i++)
                {
                    nfo.StoryHints[i] = saveData.StoryHints[i];
                }
            }

            // Live content bitmask
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
            nfo.LiveContentBitmask = liveContent;

            return NFOAuto.BytesNfo(nfo);
        }

        // Deserialize save metadata from NFO GFF format
        // Based on swkotor2.exe: FUN_00707290 @ 0x00707290
        // Located via string reference: "savenfo" @ 0x007be1f0
        // Original implementation: Reads GFF with "NFO " signature, extracts AREANAME, LASTMODULE, TIMEPLAYED,
        // CHEATUSED, SAVEGAMENAME, TIMESTAMP, PCNAME, SAVENUMBER, GAMEPLAYHINT, STORYHINT0-9, LIVECONTENT,
        // REBOOTAUTOSAVE, PCAUTOSAVE, SCREENSHOT
        public SaveGameData DeserializeSaveNfo(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return null;
            }

            NFOData nfo;
            try
            {
                nfo = NFOAuto.ReadNfo(data);
            }
            catch (Exception)
            {
                return null;
            }

            var saveData = new SaveGameData();
            saveData.CurrentAreaName = nfo.AreaName ?? string.Empty;
            saveData.CurrentModule = nfo.LastModule ?? string.Empty;
            saveData.PlayTime = TimeSpan.FromSeconds(nfo.TimePlayedSeconds);
            saveData.CheatUsed = nfo.CheatUsed;
            saveData.Name = string.IsNullOrEmpty(nfo.SavegameName) ? "Old Save Game" : nfo.SavegameName;
            saveData.PlayerName = nfo.PcName ?? string.Empty;
            saveData.GameplayHint = nfo.GameplayHint != 0;

            if (nfo.TimestampFileTime.HasValue && nfo.TimestampFileTime.Value != 0)
            {
                try
                {
                    saveData.SaveTime = DateTime.FromFileTime((long)nfo.TimestampFileTime.Value);
                }
                catch (ArgumentOutOfRangeException)
                {
                    saveData.SaveTime = DateTime.Now;
                }
            }
            else
            {
                saveData.SaveTime = DateTime.Now;
            }

            if (saveData.StoryHints == null) saveData.StoryHints = new List<bool>();
            saveData.StoryHints.Clear();
            for (int i = 0; i < 10; i++)
            {
                saveData.StoryHints.Add(nfo.StoryHints[i]);
            }

            if (saveData.LiveContent == null) saveData.LiveContent = new List<bool>();
            saveData.LiveContent.Clear();
            byte liveContent = nfo.LiveContentBitmask;
            for (int i = 0; i < 32; i++)
            {
                bool enabled = (liveContent & (1 << (i & 0x1F))) != 0;
                saveData.LiveContent.Add(enabled);
            }

            return saveData;
        }

        // Serialize save game archive to ERF format
        // Based on swkotor2.exe: FUN_004eb750 @ 0x004eb750
        // Located via string reference: "MOD V1.0" @ 0x007be0d4
        // Original implementation: Creates ERF archive with "MOD V1.0" signature, adds GLOBALVARS.res (GFF),
        // PARTYTABLE.res (GFF), cached modules (nested ERF/RIM), INVENTORY.res, REPUTE.fac, AVAILNPC*.utc
        // Uses SaveNestedCapsule to handle nested modules and cached entities
        public byte[] SerializeSaveArchive(SaveGameData saveData)
        {
            // Use SaveNestedCapsule to handle nested modules and cached entities
            // Create a temporary directory path for SaveNestedCapsule (it will write to savegame.sav)
            string tempPath = Path.GetTempPath();
            var nestedCapsule = new SaveNestedCapsule(tempPath);

            // Load existing save if it exists (to preserve cached modules and other resources)
            // This is important for maintaining nested module state
            // Note: savesDirectory should be provided via SetSavesDirectory() for path resolution
            string savePath = GetSavePathFromData(saveData);
            if (!string.IsNullOrEmpty(savePath) && Directory.Exists(savePath))
            {
                nestedCapsule = new SaveNestedCapsule(savePath);
                nestedCapsule.Load();
            }

            // Add GLOBALVARS.res
            byte[] globalVarsData = SerializeGlobalVariables(saveData.GlobalVariables);
            nestedCapsule.SetResource(new ResourceIdentifier("GLOBALVARS", ResourceType.GFF), globalVarsData);

            // Add PARTYTABLE.res
            byte[] partyTableData = SerializePartyTable(saveData.PartyState);
            nestedCapsule.SetResource(new ResourceIdentifier("PARTYTABLE", ResourceType.GFF), partyTableData);

            // Add INVENTORY.res (from party state - player inventory items)
            byte[] inventoryData = SerializeInventory(saveData.PartyState);
            if (inventoryData != null)
            {
                nestedCapsule.SetInventory(inventoryData);
            }

            // Add REPUTE.fac (faction reputation)
            byte[] reputeData = SerializeRepute(saveData);
            if (reputeData != null)
            {
                nestedCapsule.SetRepute(reputeData);
            }

            // Add AVAILNPC*.utc files (cached companion characters)
            SerializeCachedCharacters(nestedCapsule, saveData.PartyState);

            // Add cached modules (nested ERF/RIM files for previously visited areas)
            SerializeCachedModules(nestedCapsule, saveData.AreaStates);

            // Serialize nested capsule to ERF bytes
            // SaveNestedCapsule.Save() writes to disk, but we need bytes
            // So we'll manually build the ERF from nested capsule resources
            var erf = new ERF(ERFType.MOD, true); // MOD type is used for save files

            foreach (var kvp in nestedCapsule.IterSerializedResources())
            {
                erf.SetData(kvp.Key.ResName, kvp.Key.ResType, kvp.Value);
            }

            // Write ERF using Andastra.Parsing writer
            var writer = new ERFBinaryWriter(erf);
            return writer.Write();
        }

        /// <summary>
        /// Gets the save path from SaveGameData by constructing it from the save name.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_00708990 @ 0x00708990
        /// Original implementation constructs path as "SAVES:\{saveName}\"
        /// Save name format: "%06d - %s" (6-digit number - name) from string @ 0x007be298
        ///
        /// Path construction:
        /// 1. Uses savesDirectory if provided (via SetSavesDirectory or parameter)
        /// 2. Sanitizes save name for filesystem compatibility
        /// 3. Combines savesDirectory + sanitized save name
        ///
        /// Returns null if saves directory is not available or save name is invalid.
        /// </remarks>
        private string GetSavePathFromData(SaveGameData saveData, string savesDirectory = null)
        {
            if (saveData == null)
            {
                return null;
            }

            // Use provided savesDirectory parameter, fall back to instance field, or return null
            string baseDirectory = savesDirectory ?? _savesDirectory;
            if (string.IsNullOrEmpty(baseDirectory))
            {
                // Cannot construct path without saves directory
                return null;
            }

            // Get save name from SaveGameData
            string saveName = saveData.Name;
            if (string.IsNullOrEmpty(saveName))
            {
                // Save name is required to construct path
                return null;
            }

            // Sanitize save name for filesystem compatibility
            // Based on SaveDataProvider.SanitizeSaveName implementation
            string sanitizedName = SanitizeSaveName(saveName);

            // Construct path: savesDirectory/sanitizedName
            // Based on swkotor2.exe: "SAVES:\{saveName}\" format
            return Path.Combine(baseDirectory, sanitizedName);
        }

        /// <summary>
        /// Sanitizes a save name for filesystem compatibility.
        /// Removes or replaces invalid filename characters.
        /// </summary>
        /// <remarks>
        /// Based on SaveDataProvider.SanitizeSaveName implementation.
        /// Invalid characters are replaced with underscore ('_').
        /// </remarks>
        private string SanitizeSaveName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            // Remove or replace invalid filename characters
            char[] invalid = Path.GetInvalidFileNameChars();
            string sanitized = name;
            foreach (char c in invalid)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            return sanitized;
        }

        // Deserialize save game archive from ERF format
        // Based on swkotor2.exe: FUN_00708990 @ 0x00708990
        // Located via string reference: "LoadSavegame" @ 0x007bdc90
        // Original implementation: Reads ERF archive with "MOD V1.0" signature, extracts GLOBALVARS.res and PARTYTABLE.res,
        // reads module state files ([module]_s.rim) for each area and stores in AreaStates dictionary
        public void DeserializeSaveArchive(byte[] data, SaveGameData saveData)
        {
            // Use Andastra.Parsing ERF reader
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
        // Based on swkotor2.exe: SaveGlobalVariables @ 0x005ac670 (calls FUN_005ab310 @ 0x005ab310 internally)
        // Located via string reference: "GLOBALVARS" @ 0x007c27bc
        // Original implementation: Creates GFF with "GVT " signature and "V2.0" version string
        // Structure: Catalog lists (CatBoolean, CatNumber, CatLocation, CatString) with separate value arrays
        // - CatBoolean (list of structs with "Name" field) + ValBoolean (binary byte array)
        // - CatNumber (list of structs with "Name" field) + ValNumber (binary byte array)
        // - CatLocation (list of structs with "Name" field) + ValLocation (binary float array, 12 floats per location)
        // - CatString (list of structs with "Name" field) + ValString (list of strings, indexed by CatString entry order)
        private byte[] SerializeGlobalVariables(GlobalVariableState state)
        {
            // Create a temporary GlobalVars instance and populate it from state
            var globalVars = new Andastra.Parsing.Extract.SaveData.GlobalVars(Path.GetTempPath());

            // Populate from state
            foreach (var kvp in state.Booleans)
            {
                globalVars.SetBool(kvp.Key, kvp.Value);
            }
            foreach (var kvp in state.Numbers)
            {
                globalVars.SetNumber(kvp.Key, kvp.Value);
            }
            foreach (var kvp in state.Strings)
            {
                globalVars.SetString(kvp.Key, kvp.Value);
            }
            foreach (var kvp in state.Locations)
            {
                // Convert SavedLocation to Vector4 (x, y, z, facing)
                var vec4 = new System.Numerics.Vector4(kvp.Value.Position.X, kvp.Value.Position.Y, kvp.Value.Position.Z, kvp.Value.Facing);
                globalVars.SetLocation(kvp.Key, vec4);
            }

            // Serialize to GFF format
            GFF gff = new GFF(GFFContent.GVT);
            GFFStruct root = gff.Root;

            // Booleans: pack bits LSB first
            int boolCount = globalVars.GlobalBools.Count;
            int boolBytes = (boolCount + 7) / 8;
            byte[] valBool = new byte[boolBytes];
            GFFList catBool = new GFFList();
            for (int i = 0; i < boolCount; i++)
            {
                catBool.Add().SetString("Name", globalVars.GlobalBools[i].Item1);
                if (globalVars.GlobalBools[i].Item2)
                {
                    int byteIndex = i / 8;
                    int bitIndex = i % 8;
                    valBool[byteIndex] |= (byte)(1 << bitIndex);
                }
            }
            if (boolCount > 0)
            {
                root.SetList("CatBoolean", catBool);
                root.SetBinary("ValBoolean", valBool);
            }

            // Locations: 12 floats per entry
            if (globalVars.GlobalLocations.Count > 0)
            {
                GFFList catLoc = new GFFList();
                using (var ms = new MemoryStream())
                using (var bw = new System.IO.BinaryWriter(ms))
                {
                    foreach (var entry in globalVars.GlobalLocations)
                    {
                        catLoc.Add().SetString("Name", entry.Item1);
                        System.Numerics.Vector4 v = entry.Item2;
                        bw.Write(v.X);
                        bw.Write(v.Y);
                        bw.Write(v.Z);
                        bw.Write(v.W); // ori_x
                        bw.Write(0.0f); // ori_y
                        bw.Write(0.0f); // ori_z
                        for (int i = 0; i < 6; i++) bw.Write(0.0f); // padding
                    }
                    root.SetList("CatLocation", catLoc);
                    root.SetBinary("ValLocation", ms.ToArray());
                }
            }

            // Numbers: one byte each
            if (globalVars.GlobalNumbers.Count > 0)
            {
                GFFList catNum = new GFFList();
                using (var ms = new MemoryStream())
                {
                    foreach (var entry in globalVars.GlobalNumbers)
                    {
                        catNum.Add().SetString("Name", entry.Item1);
                        ms.WriteByte((byte)entry.Item2);
                    }
                    root.SetList("CatNumber", catNum);
                    root.SetBinary("ValNumber", ms.ToArray());
                }
            }

            // Strings: parallel lists
            if (globalVars.GlobalStrings.Count > 0)
            {
                GFFList catStr = new GFFList();
                GFFList valStr = new GFFList();
                foreach (var entry in globalVars.GlobalStrings)
                {
                    catStr.Add().SetString("Name", entry.Item1);
                    valStr.Add().SetString("String", entry.Item2);
                }
                root.SetList("CatString", catStr);
                root.SetList("ValString", valStr);
            }

            return new GFFBinaryWriter(gff).Write();
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

            // Use Andastra.Parsing GFF reader
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
        // Based on swkotor2.exe: SavePartyTable @ 0x0057bd70
        // Located via string reference: "PARTYTABLE" @ 0x007c1910
        // Original implementation (from decompiled SavePartyTable):
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

            // Use Andastra.Parsing GFF writer
            // Original creates GFF with "PT  " signature and "V2.0" version
            // Based on swkotor2.exe: FUN_0057bd70 @ 0x0057bd70 creates GFF with "PT  " signature
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Note: Andastra.Parsing GFFBinaryWriter always writes "V3.2" version, but signature is correct
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
        // Based on swkotor2.exe: partytable.2da system
        // Located via string reference: "PARTYTABLE" @ 0x007c1910
        // Original implementation: partytable.2da maps NPC ResRefs to member IDs (row index = member ID)
        // partytable.2da structure: Row label is ResRef, row index is member ID (0-11 for K2, 0-8 for K1)
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

            // Try to load from partytable.2da if GameDataManager is available
            // Based on swkotor2.exe: partytable.2da system
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Original implementation: partytable.2da maps NPC ResRefs to member IDs (row index = member ID)
            // partytable.2da structure: Row label is ResRef, row index is member ID (0-11 for K2, 0-8 for K1)
            if (_gameDataManager != null)
            {
                // Use dynamic to call GetTable without referencing Odyssey.Kotor
                dynamic gameDataManager = _gameDataManager;
                Andastra.Parsing.Formats.TwoDA.TwoDA partyTable = gameDataManager.GetTable("partytable");
                if (partyTable != null)
                {
                    // Search partytable.2da for matching ResRef
                    // Row index in partytable.2da corresponds to member ID
                    for (int i = 0; i < partyTable.GetHeight(); i++)
                    {
                        Andastra.Parsing.Formats.TwoDA.TwoDARow row = partyTable.GetRow(i);
                        string rowLabel = row.Label();

                        if (string.IsNullOrEmpty(rowLabel))
                        {
                            continue;
                        }

                        string rowLabelLower = rowLabel.ToLowerInvariant();

                        // Check exact match first (most common case)
                        if (string.Equals(rowLabelLower, resRefLower, StringComparison.OrdinalIgnoreCase))
                        {
                            return (float)i;
                        }

                        // Check if ResRef starts with row label (e.g., "bastila" matches "bastila_001")
                        if (resRefLower.StartsWith(rowLabelLower, StringComparison.OrdinalIgnoreCase))
                        {
                            // Verify it's a valid match (not just a substring in the middle)
                            // Check if next character is underscore, number, or end of string
                            if (resRefLower.Length == rowLabelLower.Length ||
                                resRefLower[rowLabelLower.Length] == '_' ||
                                char.IsDigit(resRefLower[rowLabelLower.Length]))
                            {
                                return (float)i;
                            }
                        }

                        // Check if row label is contained in ResRef (e.g., "atton" in "atton_001")
                        // This handles cases where the ResRef has additional suffixes
                        if (resRefLower.Contains(rowLabelLower))
                        {
                            // Verify it's at the start of the ResRef (not in the middle)
                            int index = resRefLower.IndexOf(rowLabelLower, StringComparison.OrdinalIgnoreCase);
                            if (index == 0)
                            {
                                return (float)i;
                            }
                        }
                    }
                }
            }

            // Fallback: Hardcoded mapping for common NPCs when partytable.2da not available
            // Based on nwscript.nss constants and common ResRef patterns
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
                if (resRefLower.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    // Verify it's a valid match (not just a substring)
                    if (resRefLower.Length == kvp.Key.Length ||
                        resRefLower[kvp.Key.Length] == '_' ||
                        char.IsDigit(resRefLower[kvp.Key.Length]))
                    {
                        return kvp.Value;
                    }
                }
            }

            // If no mapping found, return 0.0f as default
            // This matches original engine behavior when ResRef cannot be resolved
            return 0.0f;
        }

        // Helper to get ResRef from member ID (reverse of GetMemberId)
        // Member IDs: -1 = Player, 0-8 = NPC slots (K1), 0-11 = NPC slots (K2)
        // Based on swkotor2.exe: partytable.2da system
        // Located via string reference: "PARTYTABLE" @ 0x007c1910
        // Original implementation: partytable.2da maps NPC ResRefs to member IDs (row index = member ID)
        // partytable.2da structure: Row label is ResRef, row index is member ID (0-11 for K2, 0-8 for K1)
        // Reverse mapping: member ID -> ResRef by reading row label at index = member ID
        // Based on swkotor2.exe: FUN_0057dcd0 @ 0x0057dcd0 (LoadPartyTable function)
        // Original implementation reads PT_MEMBER_ID (float) and converts to ResRef using partytable.2da lookup
        private string GetResRefFromMemberId(float memberId)
        {
            // Player character (member ID = -1)
            // Based on nwscript.nss constants: NPC_PLAYER = -1
            // Player ResRefs are typically "player", "pc", or start with "pc_"
            if (memberId < 0.0f || Math.Abs(memberId - (-1.0f)) < 0.001f)
            {
                return "player"; // Default player ResRef
            }

            // Convert float to int (member IDs are stored as floats but represent integer indices)
            int memberIndex = (int)memberId;
            if (memberIndex < 0)
            {
                return "player"; // Default to player for negative values
            }

            // Try to load from partytable.2da if GameDataManager is available
            // Based on swkotor2.exe: partytable.2da system
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Original implementation: partytable.2da row index = member ID, row label = ResRef
            // FUN_0057dcd0 @ 0x0057dcd0: Reads PT_MEMBER_ID and uses row index to get ResRef from partytable.2da
            if (_gameDataManager != null)
            {
                // Use dynamic to call GetTable without referencing Odyssey.Kotor
                dynamic gameDataManager = _gameDataManager;
                Andastra.Parsing.Formats.TwoDA.TwoDA partyTable = gameDataManager.GetTable("partytable");
                if (partyTable != null)
                {
                    // Check if member index is within valid range
                    if (memberIndex < partyTable.GetHeight())
                    {
                        Andastra.Parsing.Formats.TwoDA.TwoDARow row = partyTable.GetRow(memberIndex);
                        string rowLabel = row.Label();

                        if (!string.IsNullOrEmpty(rowLabel))
                        {
                            return rowLabel; // Return the ResRef from partytable.2da row label
                        }
                    }
                }
            }

            // Fallback: Hardcoded reverse mapping for common NPCs when partytable.2da not available
            // Based on nwscript.nss constants and common ResRef patterns
            // This is the reverse of the mapping in GetMemberId
            var memberIdToResRef = new Dictionary<int, string>
            {
                // K1 NPCs (0-8)
                { 0, "bastila" },      // NPC_BASTILA
                { 1, "canderous" },    // NPC_CANDEROUS
                { 2, "carth" },        // NPC_CARTH
                { 3, "hk47" },         // NPC_HK_47
                { 4, "jolee" },        // NPC_JOLEE
                { 5, "juhani" },       // NPC_JUHANI
                { 6, "mission" },      // NPC_MISSION
                { 7, "t3m4" },         // NPC_T3_M4
                { 8, "zaalbar" },      // NPC_ZAALBAR

                // K2 NPCs (0-11) - some overlap with K1, but K2 takes precedence
                // Note: K2 has different NPCs, but some slots may overlap with K1
                // When both games have NPCs in same slot, K2 mapping is used (game-specific behavior)
                { 0, "atton" },        // K2 NPC_ATTON (overrides bastila for K2)
                { 1, "bao-dur" },      // K2 NPC_BAO_DUR
                { 2, "disciple" },     // K2 NPC_DISCIPLE
                { 3, "handmaiden" },   // K2 NPC_HANDMAIDEN
                { 4, "hanharr" },      // K2 NPC_HANHARR
                { 5, "g0-t0" },        // K2 NPC_G0_T0
                { 6, "kreia" },        // K2 NPC_KREIA
                { 7, "mira" },         // K2 NPC_MIRA
                { 8, "visas" },        // K2 NPC_VISAS (overrides zaalbar for K2)
                { 9, "mandalore" },    // K2 NPC_MANDALORE
                { 11, "sion" },        // K2 NPC_SION
            };

            // Try to find ResRef in hardcoded mapping
            if (memberIdToResRef.TryGetValue(memberIndex, out string resRef))
            {
                return resRef;
            }

            // If no mapping found, return generic NPC ResRef based on index
            // This matches original engine behavior when ResRef cannot be resolved from partytable.2da
            // Format: "npc_XX" where XX is zero-padded member index
            return string.Format("npc_{0:D2}", memberIndex);
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

            // Use Andastra.Parsing GFF reader
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
                // Based on swkotor2.exe: FUN_0057dcd0 @ 0x0057dcd0
                // Original implementation: Reads PT_MEMBER_ID (float) and PT_IS_LEADER (byte) for each member
                // Maps member IDs to ResRefs using partytable.2da lookup (row index = member ID, row label = ResRef)
                // Adds ResRefs to SelectedParty list and sets LeaderResRef if PT_IS_LEADER is true
                GFFList membersList = root.GetList("PT_MEMBERS");
                if (membersList != null)
                {
                    // Initialize SelectedParty if not already initialized
                    if (state.SelectedParty == null)
                    {
                        state.SelectedParty = new List<string>();
                    }

                    int memberCount = Math.Min(numMembers, membersList.Count);
                    for (int i = 0; i < memberCount; i++)
                    {
                        GFFStruct entry = membersList[i];
                        float memberId = entry.GetSingle("PT_MEMBER_ID");
                        bool isLeader = entry.GetUInt8("PT_IS_LEADER") != 0;

                        // Map member ID to ResRef using reverse lookup
                        // Based on swkotor2.exe: FUN_0057dcd0 @ 0x0057dcd0
                        // Original implementation uses partytable.2da to convert member ID (row index) to ResRef (row label)
                        string memberResRef = GetResRefFromMemberId(memberId);

                        // Add member ResRef to SelectedParty list
                        // SelectedParty contains ResRefs of currently active party members
                        if (!string.IsNullOrEmpty(memberResRef) && !state.SelectedParty.Contains(memberResRef))
                        {
                            state.SelectedParty.Add(memberResRef);
                        }

                        // Set LeaderResRef if this member is the leader
                        // PT_IS_LEADER (byte): 1 = leader, 0 = not leader
                        // Only one member should have PT_IS_LEADER = 1 (original engine enforces this)
                        if (isLeader)
                        {
                            state.LeaderResRef = memberResRef;
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
                // Based on swkotor2.exe: FUN_0057dcd0 @ 0x0057dcd0
                // Original implementation: Reads PT_NPC_AVAIL (byte) and PT_NPC_SELECT (byte) for each NPC slot (0-11)
                // List index corresponds to member ID (0-11), maps to ResRef using partytable.2da lookup
                // PT_AVAIL_NPCS[0] = NPC at member ID 0, PT_AVAIL_NPCS[1] = NPC at member ID 1, etc.
                GFFList availNpcsList = root.GetList("PT_AVAIL_NPCS");
                if (availNpcsList != null)
                {
                    // Initialize AvailableMembers if not already initialized
                    if (state.AvailableMembers == null)
                    {
                        state.AvailableMembers = new Dictionary<string, PartyMemberState>();
                    }

                    int count = Math.Min(12, availNpcsList.Count);
                    for (int i = 0; i < count; i++)
                    {
                        GFFStruct entry = availNpcsList[i];
                        bool available = entry.GetUInt8("PT_NPC_AVAIL") != 0;
                        bool selectable = entry.GetUInt8("PT_NPC_SELECT") != 0;

                        // Map index (member ID) to NPC ResRef using reverse lookup
                        // Based on swkotor2.exe: FUN_0057dcd0 @ 0x0057dcd0
                        // Original implementation uses partytable.2da to convert member ID (row index) to ResRef (row label)
                        // List index i corresponds to member ID i (0-11)
                        string npcResRef = GetResRefFromMemberId((float)i);

                        // Skip if ResRef is "player" (member ID -1 is player, but index 0-11 should be NPCs)
                        // However, if GetResRefFromMemberId returns "player" for index 0, it means partytable.2da lookup failed
                        // We still create the entry but use the resolved ResRef
                        if (!state.AvailableMembers.ContainsKey(npcResRef))
                        {
                            state.AvailableMembers[npcResRef] = new PartyMemberState
                            {
                                TemplateResRef = npcResRef
                            };
                        }

                        // Set availability and selectability flags
                        // PT_NPC_AVAIL (byte): 1 = available, 0 = not available
                        // PT_NPC_SELECT (byte): 1 = selectable, 0 = not selectable
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

            // Parse GFF using Andastra.Parsing
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
                state.ObjectType = (Andastra.Runtime.Core.Enums.ObjectType)(int)structData.GetUInt32("ObjectType");
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

        #region Inventory, Repute, Cached Characters, Cached Modules

        // Serialize inventory (INVENTORY.res) - player inventory items
        // Based on swkotor.exe: Inventory is stored as a GFF file in savegame.sav
        // Located via string reference: "INVENTORY" @ (needs verification)
        private byte[] SerializeInventory(PartyState partyState)
        {
            if (partyState == null || partyState.PlayerCharacter == null)
            {
                return null;
            }

            // Create GFF with INV content type
            GFF gff = new GFF(GFFContent.INV);
            GFFStruct root = gff.Root;

            // ItemList - List of inventory items
            GFFList itemList = root.Acquire<GFFList>("ItemList", new GFFList());

            // Serialize each inventory item
            if (partyState.PlayerCharacter.Inventory != null)
            {
                ushort gridX = 0;
                ushort gridY = 0;
                const ushort gridWidth = 10; // Standard inventory grid width

                foreach (ItemState item in partyState.PlayerCharacter.Inventory)
                {
                    if (item == null || string.IsNullOrEmpty(item.TemplateResRef))
                    {
                        continue;
                    }

                    // Create item struct
                    GFFStruct itemStruct = itemList.Add();

                    // Repos_PosX - X position in inventory grid (WORD)
                    itemStruct.SetUInt16("Repos_PosX", gridX);

                    // Repos_PosY - Y position in inventory grid (WORD)
                    itemStruct.SetUInt16("Repos_PosY", gridY);

                    // InventoryRes - Item template ResRef (ResRef)
                    itemStruct.SetResRef("InventoryRes", ResRef.FromString(item.TemplateResRef));

                    // StackSize - Item stack count (DWORD)
                    itemStruct.SetUInt32("StackSize", (uint)item.StackSize);

                    // Charges - Current charges/uses (DWORD)
                    itemStruct.SetUInt32("Charges", (uint)item.Charges);

                    // Identified - Whether item is identified (BYTE)
                    itemStruct.SetUInt8("Identified", item.Identified ? (byte)1 : (byte)0);

                    // Upgrades - Item upgrade modifications (List)
                    if (item.Upgrades != null && item.Upgrades.Count > 0)
                    {
                        GFFList upgradesList = itemStruct.Acquire<GFFList>("Upgrades", new GFFList());
                        foreach (ItemUpgrade upgrade in item.Upgrades)
                        {
                            if (upgrade == null || string.IsNullOrEmpty(upgrade.UpgradeResRef))
                            {
                                continue;
                            }

                            GFFStruct upgradeStruct = upgradesList.Add();
                            upgradeStruct.SetInt32("UpgradeSlot", upgrade.UpgradeSlot);
                            upgradeStruct.SetResRef("UpgradeResRef", ResRef.FromString(upgrade.UpgradeResRef));
                        }
                    }

                    // Update grid position for next item (simple sequential layout)
                    gridX++;
                    if (gridX >= gridWidth)
                    {
                        gridX = 0;
                        gridY++;
                    }
                }
            }

            return gff.ToBytes();
        }

        // Serialize repute (REPUTE.fac) - faction reputation
        // Based on swkotor.exe: Repute is stored as a FAC file in savegame.sav
        // Located via string reference: "REPUTE" @ (needs verification)
        // FAC file format: GFF with "FAC " signature, contains FactionList and RepList
        // FactionList: List of Faction structs (FactionName: string, FactionGlobal: uint)
        // RepList: List of Reputation structs (FactionID1: uint, FactionID2: uint, FactionRep: uint)
        // Reference: vendor/xoreos/src/engines/nwn2/faction.cpp (loadFac method)
        // Reference: vendor/PyKotor/wiki/Bioware-Aurora-Faction.md (Faction Format documentation)
        private byte[] SerializeRepute(SaveGameData saveData)
        {
            if (saveData == null)
            {
                return null;
            }

            // Create GFF with FAC content type
            GFF gff = new GFF(GFFContent.FAC);
            GFFStruct root = gff.Root;

            // Create FactionList - list of Faction structs
            // Each Faction struct contains:
            // - FactionName (string): Name of the faction
            // - FactionGlobal (uint/word): Global effect flag (1 if all members of this faction immediately change reputation)
            // Based on vendor/xoreos/src/engines/nwn2/faction.cpp:179-198
            GFFList factionList = new GFFList();

            // Create RepList - list of Reputation structs
            // Each Reputation struct contains:
            // - FactionID1 (uint): Index into FactionList (source faction)
            // - FactionID2 (uint): Index into FactionList (target faction)
            // - FactionRep (uint): How FactionID1 perceives FactionID2 (0-100, where 50 is neutral)
            // Based on vendor/xoreos/src/engines/nwn2/faction.cpp:210-226
            GFFList repList = new GFFList();

            // Populate FactionList and RepList from SaveGameData.FactionReputation
            // Based on swkotor2.exe: Faction reputation saved in REPUTE.fac (GFF with "FAC " signature)
            // Located via string references: "REPUTE" @ (needs verification), "FactionList" @ 0x007be604
            // Reference: vendor/xoreos/src/engines/nwn2/faction.cpp:179-226 (loadFac method)
            if (saveData.FactionReputation != null && saveData.FactionReputation.Reputations != null)
            {
                // Collect all unique faction IDs from the reputation matrix
                HashSet<int> allFactionIds = new HashSet<int>();
                foreach (var sourceKvp in saveData.FactionReputation.Reputations)
                {
                    allFactionIds.Add(sourceKvp.Key);
                    if (sourceKvp.Value != null)
                    {
                        foreach (var targetKvp in sourceKvp.Value)
                        {
                            allFactionIds.Add(targetKvp.Key);
                        }
                    }
                }

                // Sort faction IDs for consistent ordering (index in FactionList = FactionID)
                List<int> sortedFactionIds = new List<int>(allFactionIds);
                sortedFactionIds.Sort();

                // Create FactionList entries
                // Each Faction struct contains:
                // - FactionName (string): Name of the faction
                // - FactionGlobal (uint): Global effect flag (1 if all members of this faction immediately change reputation)
                foreach (int factionId in sortedFactionIds)
                {
                    GFFStruct factionStruct = factionList.Add();

                    // Get faction name from FactionReputation.FactionNames if available
                    string factionName = string.Empty;
                    if (saveData.FactionReputation.FactionNames != null &&
                        saveData.FactionReputation.FactionNames.TryGetValue(factionId, out factionName))
                    {
                        factionStruct.SetString("FactionName", factionName);
                    }
                    else
                    {
                        // Use default name if not available (faction ID as string, or empty)
                        // Empty name is valid - engine will use faction ID for lookup
                        factionStruct.SetString("FactionName", string.Empty);
                    }

                    // Get FactionGlobal flag from FactionReputation.FactionGlobal if available
                    uint factionGlobal = 0;
                    if (saveData.FactionReputation.FactionGlobal != null &&
                        saveData.FactionReputation.FactionGlobal.TryGetValue(factionId, out bool globalFlag))
                    {
                        factionGlobal = globalFlag ? (uint)1 : (uint)0;
                    }
                    factionStruct.SetUInt32("FactionGlobal", factionGlobal);
                }

                // Create RepList entries
                // Each Reputation struct contains:
                // - FactionID1 (uint): Index into FactionList (source faction)
                // - FactionID2 (uint): Index into FactionList (target faction)
                // - FactionRep (uint): How FactionID1 perceives FactionID2 (0-100, where 50 is neutral)
                foreach (var sourceKvp in saveData.FactionReputation.Reputations)
                {
                    int sourceFactionId = sourceKvp.Key;
                    Dictionary<int, int> targetReps = sourceKvp.Value;

                    if (targetReps == null)
                    {
                        continue;
                    }

                    // Find index of source faction in sorted list
                    int sourceIndex = sortedFactionIds.IndexOf(sourceFactionId);
                    if (sourceIndex < 0)
                    {
                        continue; // Source faction not in list (should not happen)
                    }

                    foreach (var targetKvp in targetReps)
                    {
                        int targetFactionId = targetKvp.Key;
                        int reputation = targetKvp.Value;

                        // Find index of target faction in sorted list
                        int targetIndex = sortedFactionIds.IndexOf(targetFactionId);
                        if (targetIndex < 0)
                        {
                            continue; // Target faction not in list (should not happen)
                        }

                        // Create Reputation struct
                        GFFStruct repStruct = repList.Add();
                        repStruct.SetUInt32("FactionID1", (uint)sourceIndex);
                        repStruct.SetUInt32("FactionID2", (uint)targetIndex);
                        repStruct.SetUInt32("FactionRep", (uint)reputation);
                    }
                }
            }
            // If FactionReputation is null or empty, create empty lists which is a valid FAC file structure
            // (empty = no custom factions, engine will fall back to repute.2da for default faction relationships)

            // Set lists in root struct
            root.SetList("FactionList", factionList);
            root.SetList("RepList", repList);

            // Write GFF to byte array
            return new GFFBinaryWriter(gff).Write();
        }

        // Serialize cached characters (AVAILNPC*.utc) - companion character templates
        // Based on swkotor.exe: Cached characters are stored as UTC files in savegame.sav
        // Located via string reference: "AVAILNPC" @ (needs verification)
        // Each companion character template is serialized as a UTC file with ResRef "AVAILNPC" + index (0-based)
        private void SerializeCachedCharacters(SaveNestedCapsule nestedCapsule, PartyState partyState)
        {
            if (partyState == null || partyState.AvailableMembers == null)
            {
                return;
            }

            // Serialize each available companion character as a UTC file
            // ResRef format: "AVAILNPC" + index (e.g., "AVAILNPC0", "AVAILNPC1", etc.)
            int index = 0;
            foreach (KeyValuePair<string, PartyMemberState> kvp in partyState.AvailableMembers)
            {
                string templateResRef = kvp.Key;
                PartyMemberState memberState = kvp.Value;

                // Skip if no creature state is available
                if (memberState?.State == null)
                {
                    continue;
                }

                // Convert CreatureState to UTC format
                // Get stack size information for items (needed for save game serialization)
                UTC utc = CreateUtcFromCreatureState(memberState.State, templateResRef, out Dictionary<string, int> itemStackSizes);

                // Serialize UTC to bytes with StackSize support for save games
                // Based on swkotor2.exe: FUN_005675e0 @ 0x005675e0 writes StackSize to ItemList struct when serializing creatures
                // swkotor2.exe: SerializeCreature_K2 @ 0x005226d0 calls FUN_005675e0 for each inventory item
                byte[] utcData = SerializeUtcForSaveGame(utc, BioWareGame.K2, itemStackSizes);

                // Create ResRef for cached character (AVAILNPC + index)
                string cachedResRef = "AVAILNPC" + index.ToString();

                // Add UTC file to nested capsule with ResourceType.UTC
                ResourceIdentifier ident = new ResourceIdentifier(cachedResRef, ResourceType.UTC);
                nestedCapsule.SetResource(ident, utcData);

                index++;
            }
        }

        /// <summary>
        /// Creates a UTC object from a CreatureState for cached character serialization.
        /// Converts all creature state fields to UTC format, including stats, inventory, equipment, classes, feats, and skills.
        /// </summary>
        /// <param name="creatureState">The creature state to convert.</param>
        /// <param name="templateResRef">The template ResRef for the creature.</param>
        /// <param name="itemStackSizes">Output parameter: Dictionary mapping item TemplateResRef to stack size.</param>
        /// <returns>A UTC object containing the creature data.</returns>
        private UTC CreateUtcFromCreatureState(CreatureState creatureState, string templateResRef, out Dictionary<string, int> itemStackSizes)
        {
            if (creatureState == null)
            {
                throw new ArgumentNullException(nameof(creatureState));
            }

            var utc = new UTC();

            // Set basic identification fields
            utc.ResRef = ResRef.FromString(templateResRef ?? "");
            utc.Tag = creatureState.Tag ?? "";

            // Set attributes (ability scores)
            if (creatureState.Attributes != null)
            {
                utc.Strength = creatureState.Attributes.Strength;
                utc.Dexterity = creatureState.Attributes.Dexterity;
                utc.Constitution = creatureState.Attributes.Constitution;
                utc.Intelligence = creatureState.Attributes.Intelligence;
                utc.Wisdom = creatureState.Attributes.Wisdom;
                utc.Charisma = creatureState.Attributes.Charisma;
            }

            // Set hit points and force points
            // CurrentHP and MaxHP are from EntityState (base class)
            utc.CurrentHp = creatureState.CurrentHP;
            utc.MaxHp = creatureState.MaxHP;
            // HP is base hit points (typically same as MaxHP for creatures)
            utc.Hp = creatureState.MaxHP;
            utc.Fp = creatureState.CurrentFP;
            utc.MaxFp = creatureState.MaxFP;

            // Set level and XP (note: UTC doesn't have XP field directly, but level is used)
            // Level is encoded in ClassLevels

            // Set alignment
            utc.Alignment = creatureState.Alignment;

            // Set skills from Skills dictionary
            // Skill order: ComputerUse, Demolitions, Stealth, Awareness, Persuade, Repair, Security, TreatInjury
            if (creatureState.Skills != null)
            {
                // Try to get skill values by common names (case-insensitive matching)
                if (creatureState.Skills.TryGetValue("ComputerUse", out int computerUse))
                {
                    utc.ComputerUse = computerUse;
                }
                if (creatureState.Skills.TryGetValue("Demolitions", out int demolitions))
                {
                    utc.Demolitions = demolitions;
                }
                if (creatureState.Skills.TryGetValue("Stealth", out int stealth))
                {
                    utc.Stealth = stealth;
                }
                if (creatureState.Skills.TryGetValue("Awareness", out int awareness))
                {
                    utc.Awareness = awareness;
                }
                if (creatureState.Skills.TryGetValue("Persuade", out int persuade))
                {
                    utc.Persuade = persuade;
                }
                if (creatureState.Skills.TryGetValue("Repair", out int repair))
                {
                    utc.Repair = repair;
                }
                if (creatureState.Skills.TryGetValue("Security", out int security))
                {
                    utc.Security = security;
                }
                if (creatureState.Skills.TryGetValue("TreatInjury", out int treatInjury))
                {
                    utc.TreatInjury = treatInjury;
                }
            }

            // Set classes from ClassLevels
            if (creatureState.ClassLevels != null)
            {
                foreach (ClassLevel classLevel in creatureState.ClassLevels)
                {
                    var utcClass = new UTCClass(classLevel.ClassId, classLevel.Level);

                    // Add powers from KnownPowers that belong to this class level
                    // Note: KnownPowers is a List<string>, we need to convert to integer IDs
                    // Based on swkotor2.exe: spells.2da system
                    // Located via string references: "spells.2da" @ 0x007c2e60
                    // Original implementation: spells.2da row index = spell/power ID, row label = spell label
                    // The engine uses spell IDs (row indices) to store powers in UTC structures
                    // Based on vendor/PyKotor/wiki/2DA-spells.md: spells.2da structure where row index directly corresponds to spell ID
                    if (creatureState.KnownPowers != null)
                    {
                        foreach (string powerStr in creatureState.KnownPowers)
                        {
                            bool powerAdded = false;

                            // Try to parse as integer (most common case - power IDs are typically numeric strings)
                            if (int.TryParse(powerStr, out int powerId))
                            {
                                utcClass.Powers.Add(powerId);
                                powerAdded = true;
                            }
                            else
                            {
                                // If powerStr is not numeric, try to look it up by label in spells.2da
                                // This allows loading saves that contain power labels instead of IDs
                                if (_gameDataManager != null)
                                {
                                    try
                                    {
                                        // Use dynamic to call GetTable without referencing Odyssey.Kotor
                                        dynamic gameDataManager = _gameDataManager;
                                        Andastra.Parsing.Formats.TwoDA.TwoDA spellsTable = gameDataManager.GetTable("spells");
                                        if (spellsTable != null)
                                        {
                                            // Get row index by label - in spells.2da, row index IS the spell/power ID
                                            // swkotor2.exe: spells.2da structure where row index directly corresponds to spell ID
                                            // Based on vendor/PyKotor/wiki/2DA-spells.md: Row index = Spell ID (integer)
                                            int powerRowIndex = spellsTable.GetRowIndex(powerStr);
                                            utcClass.Powers.Add(powerRowIndex);
                                            powerAdded = true;
                                        }
                                    }
                                    catch (System.Collections.Generic.KeyNotFoundException)
                                    {
                                        // Power label not found in spells.2da, skip it (matches original behavior when label doesn't exist)
                                    }
                                    catch
                                    {
                                        // Any other error (table not available, etc.), skip this power
                                        // This matches the original behavior of skipping invalid powers
                                    }
                                }
                            }

                            // If power was not added (either invalid ID or lookup failed), it's silently skipped
                            // This matches the original engine behavior of ignoring invalid power references
                        }
                    }

                    utc.Classes.Add(utcClass);
                }
            }

            // Set feats from KnownFeats
            // KnownFeats is List<string>, but UTC expects List<int>
            // Try to parse as integers, or look up by label in feat.2da if not numeric
            // Based on swkotor2.exe: feat.2da system
            // Located via string reference: "CSWClass::LoadFeatTable: Can't load feat.2da" @ 0x007c4720
            // Original implementation: feat.2da row index = feat ID, row label = feat label
            // The engine uses feat IDs (row indices) to store feats in UTC structures
            if (creatureState.KnownFeats != null)
            {
                foreach (string featStr in creatureState.KnownFeats)
                {
                    bool featAdded = false;

                    // Try to parse as integer (most common case - feat IDs are typically numeric strings)
                    if (int.TryParse(featStr, out int featId))
                    {
                        utc.Feats.Add(featId);
                        featAdded = true;
                    }
                    else
                    {
                        // If featStr is not numeric, try to look it up by label in feat.2da
                        // This allows loading saves that contain feat labels instead of IDs
                        if (_gameDataManager != null)
                        {
                            try
                            {
                                // Use dynamic to call GetTable without referencing Odyssey.Kotor
                                dynamic gameDataManager = _gameDataManager;
                                Andastra.Parsing.Formats.TwoDA.TwoDA featTable = gameDataManager.GetTable("feat");
                                if (featTable != null)
                                {
                                    // Get row index by label - in feat.2da, row index IS the feat ID
                                    // swkotor2.exe: feat.2da structure where row index directly corresponds to feat ID
                                    int featRowIndex = featTable.GetRowIndex(featStr);
                                    utc.Feats.Add(featRowIndex);
                                    featAdded = true;
                                }
                            }
                            catch (System.Collections.Generic.KeyNotFoundException)
                            {
                                // Feat label not found in feat.2da, skip it (matches original behavior when label doesn't exist)
                            }
                            catch
                            {
                                // Any other error (table not available, etc.), skip this feat
                                // This matches the original behavior of skipping invalid feats
                            }
                        }
                    }

                    // If feat was not added (either invalid ID or lookup failed), it's silently skipped
                    // This matches the original engine behavior of ignoring invalid feat references
                }
            }

            // Set equipment from EquipmentState
            if (creatureState.Equipment != null)
            {
                // Map EquipmentState properties to EquipmentSlot enum
                if (creatureState.Equipment.Head != null && !string.IsNullOrEmpty(creatureState.Equipment.Head.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.HEAD] = new InventoryItem(ResRef.FromString(creatureState.Equipment.Head.TemplateResRef), true);
                }
                if (creatureState.Equipment.Armor != null && !string.IsNullOrEmpty(creatureState.Equipment.Armor.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.ARMOR] = new InventoryItem(ResRef.FromString(creatureState.Equipment.Armor.TemplateResRef), true);
                }
                if (creatureState.Equipment.Gloves != null && !string.IsNullOrEmpty(creatureState.Equipment.Gloves.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.GAUNTLET] = new InventoryItem(ResRef.FromString(creatureState.Equipment.Gloves.TemplateResRef), true);
                }
                if (creatureState.Equipment.RightHand != null && !string.IsNullOrEmpty(creatureState.Equipment.RightHand.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.RIGHT_HAND] = new InventoryItem(ResRef.FromString(creatureState.Equipment.RightHand.TemplateResRef), true);
                }
                if (creatureState.Equipment.LeftHand != null && !string.IsNullOrEmpty(creatureState.Equipment.LeftHand.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.LEFT_HAND] = new InventoryItem(ResRef.FromString(creatureState.Equipment.LeftHand.TemplateResRef), true);
                }
                if (creatureState.Equipment.Belt != null && !string.IsNullOrEmpty(creatureState.Equipment.Belt.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.BELT] = new InventoryItem(ResRef.FromString(creatureState.Equipment.Belt.TemplateResRef), true);
                }
                if (creatureState.Equipment.Implant != null && !string.IsNullOrEmpty(creatureState.Equipment.Implant.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.IMPLANT] = new InventoryItem(ResRef.FromString(creatureState.Equipment.Implant.TemplateResRef), true);
                }
                if (creatureState.Equipment.RightArm != null && !string.IsNullOrEmpty(creatureState.Equipment.RightArm.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.RIGHT_ARM] = new InventoryItem(ResRef.FromString(creatureState.Equipment.RightArm.TemplateResRef), true);
                }
                if (creatureState.Equipment.LeftArm != null && !string.IsNullOrEmpty(creatureState.Equipment.LeftArm.TemplateResRef))
                {
                    utc.Equipment[EquipmentSlot.LEFT_ARM] = new InventoryItem(ResRef.FromString(creatureState.Equipment.LeftArm.TemplateResRef), true);
                }
            }

            // Set inventory from Inventory list
            // Based on swkotor2.exe: ItemList in UTC format stores one entry per unique item template
            // StackSize is written when serializing creatures in save games (FUN_005675e0 @ 0x005675e0 line 15)
            // swkotor2.exe: SerializeCreature_K2 @ 0x005226d0 iterates through inventory and calls FUN_005675e0 for each item
            // FUN_005675e0 writes StackSize, Charges, Upgrades, and other item properties to ItemList struct
            // For save game UTC files, StackSize is preserved in the ItemList structure
            if (creatureState.Inventory != null)
            {
                // Group identical items by TemplateResRef to handle stacks properly
                // Based on swkotor2.exe: Items with the same template are grouped, StackSize is written per entry
                // Original implementation: One ItemList entry per unique item template with StackSize field
                Dictionary<string, ItemState> groupedItems = new Dictionary<string, ItemState>(StringComparer.OrdinalIgnoreCase);

                foreach (ItemState itemState in creatureState.Inventory)
                {
                    if (itemState != null && !string.IsNullOrEmpty(itemState.TemplateResRef))
                    {
                        // Group items by TemplateResRef - if same template exists, combine stack sizes
                        // Based on swkotor2.exe: Items with same template are combined with total StackSize
                        if (groupedItems.TryGetValue(itemState.TemplateResRef, out ItemState existingItem))
                        {
                            // Combine stacks: add stack sizes together
                            // Based on swkotor2.exe: StackSize is cumulative for identical items
                            existingItem.StackSize += itemState.StackSize;
                            // Preserve other properties from the first item (charges, identified, upgrades)
                            // If items have different properties, we use the first item's properties
                        }
                        else
                        {
                            // First occurrence of this item template - add to grouped items
                            groupedItems[itemState.TemplateResRef] = itemState;
                        }
                    }
                }

                // Add one InventoryItem per unique item template
                // StackSize will be written directly to GFF structure when serializing for save games
                // Based on swkotor2.exe: ItemList contains one entry per unique item with StackSize field
                foreach (ItemState groupedItem in groupedItems.Values)
                {
                    // Create InventoryItem for each unique item template
                    // Note: InventoryItem doesn't store StackSize, but we'll write it directly to GFF when serializing
                    utc.Inventory.Add(new InventoryItem(ResRef.FromString(groupedItem.TemplateResRef), true));
                }

                // Store stack sizes for later use when serializing UTC for save games
                // This allows us to write StackSize directly to the GFF structure even though InventoryItem doesn't support it
                // Based on swkotor2.exe: StackSize is written to ItemList struct when serializing creatures in save games
                itemStackSizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (ItemState groupedItem in groupedItems.Values)
                {
                    itemStackSizes[groupedItem.TemplateResRef] = groupedItem.StackSize;
                }
            }
            else
            {
                itemStackSizes = new Dictionary<string, int>();
            }

            // Set default flags for companion characters
            utc.IsPc = false; // Companions are NPCs
            utc.Plot = false; // Companions are typically not plot-critical (can be overridden if needed)

            return utc;
        }

        /// <summary>
        /// Serializes a UTC object to bytes for save game format, including StackSize in ItemList.
        /// Based on swkotor2.exe: FUN_005675e0 @ 0x005675e0 writes StackSize to ItemList struct when serializing creatures in save games.
        /// swkotor2.exe: SerializeCreature_K2 @ 0x005226d0 calls FUN_005675e0 for each inventory item.
        /// </summary>
        /// <param name="utc">The UTC object to serialize.</param>
        /// <param name="game">The game version.</param>
        /// <param name="itemStackSizes">Dictionary mapping item TemplateResRef to stack size.</param>
        /// <returns>Serialized UTC data as byte array.</returns>
        private byte[] SerializeUtcForSaveGame(UTC utc, BioWareGame game, Dictionary<string, int> itemStackSizes)
        {
            if (utc == null)
            {
                throw new ArgumentNullException(nameof(utc));
            }

            // Create GFF structure using standard DismantleUtc
            GFF gff = UTCHelpers.DismantleUtc(utc, game);
            GFFStruct root = gff.Root;

            // Modify ItemList to include StackSize for each item
            // Based on swkotor2.exe: FUN_005675e0 @ 0x005675e0 line 15 writes StackSize as WORD (ushort)
            // swkotor2.exe: StackSize is written to ItemList struct when serializing creatures in save games
            if (itemStackSizes != null && itemStackSizes.Count > 0 && utc.Inventory != null && utc.Inventory.Count > 0)
            {
                GFFList itemList = root.Acquire<GFFList>("ItemList", new GFFList());
                if (itemList != null && itemList.Count > 0)
                {
                    // Update each item struct in ItemList to include StackSize
                    // Based on swkotor2.exe: ItemList contains one entry per unique item with StackSize field
                    for (int i = 0; i < itemList.Count && i < utc.Inventory.Count; i++)
                    {
                        GFFStruct itemStruct = itemList[i];
                        if (itemStruct != null)
                        {
                            InventoryItem item = utc.Inventory[i];
                            if (item != null && itemStackSizes.TryGetValue(item.ResRef.ToString(), out int stackSize))
                            {
                                // Write StackSize to ItemList struct
                                // Based on swkotor2.exe: FUN_005675e0 @ 0x005675e0 line 15 writes StackSize as WORD (ushort)
                                // However, save game format uses DWORD (uint) for StackSize (see SerializeInventory line 2066)
                                // We'll use DWORD to match the save game format
                                itemStruct.SetUInt32("StackSize", (uint)Math.Max(1, stackSize));
                            }
                            else
                            {
                                // Default to stack size of 1 if not found in dictionary
                                itemStruct.SetUInt32("StackSize", 1);
                            }
                        }
                    }
                }
            }

            // Serialize GFF to bytes
            return GFFAuto.BytesGff(gff, ResourceType.UTC);
        }

        // Serialize cached modules (nested ERF/RIM files) - previously visited areas
        // Based on swkotor.exe: Cached modules are stored as ERF/RIM files (ResourceType.SAV) in savegame.sav
        // Located via string reference: Module state files are stored as [module]_s.rim in savegame.sav
        private void SerializeCachedModules(SaveNestedCapsule nestedCapsule, Dictionary<string, AreaState> areaStates)
        {
            if (areaStates == null)
            {
                return;
            }

            // Each area state should have a cached module (ERF/RIM) containing the area's state
            foreach (KeyValuePair<string, AreaState> kvp in areaStates)
            {
                string areaResRef = kvp.Key;
                AreaState areaState = kvp.Value;

                // Cached modules are stored as ResourceType.SAV (2057) with ResRef = areaResRef
                // The data inside is RIM format (standard format for area state in swkotor2.exe)
                // Based on swkotor2.exe: Area state is stored as [module]_s.rim in savegame.sav
                // SerializeAreaStateAsModule creates a RIM archive containing the area state GFF
                byte[] moduleData = SerializeAreaStateAsModule(areaState);
                if (moduleData != null)
                {
                    // Try to parse as RIM first, then ERF
                    try
                    {
                        var rim = RIMAuto.ReadRim(moduleData);
                        nestedCapsule.SetCachedRimModule(areaResRef, rim);
                    }
                    catch
                    {
                        // Not RIM format, try ERF
                        try
                        {
                            var erf = ERFAuto.ReadErf(moduleData);
                            nestedCapsule.SetCachedModule(areaResRef, erf);
                        }
                        catch
                        {
                            // Neither RIM nor ERF - store as raw data
                            var ident = new ResourceIdentifier(areaResRef, ResourceType.SAV);
                            nestedCapsule.SetResource(ident, moduleData);
                        }
                    }
                }
            }
        }

        // Serialize area state as a module (ERF/RIM format)
        // Based on swkotor.exe: Area state is stored as [module]_s.rim in savegame.sav
        // Located via string reference: Module state files are stored as [module]_s.rim in savegame.sav
        // Original implementation: Area state is serialized as a RIM file containing a GFF resource with the area state data
        // The RIM file contains one resource: the area ResRef as a GFF file (ResourceType.GFF)
        // This GFF contains all entity states, destroyed entities, spawned entities, and local variables
        private byte[] SerializeAreaStateAsModule(AreaState areaState)
        {
            if (areaState == null)
            {
                return null;
            }

            // Create RIM archive
            var rim = new RIM();

            // Serialize area state to GFF format
            // The area state GFF contains all the entity states and area-specific data
            byte[] areaStateGffData = SerializeAreaStateToGff(areaState);

            if (areaStateGffData != null && areaStateGffData.Length > 0)
            {
                // Add area state GFF to RIM archive
                // Resource name is the area ResRef, type is GFF
                // Based on swkotor2.exe: Area state stored as GFF resource in RIM with ResRef = area ResRef
                string areaResRef = areaState.AreaResRef ?? "area";
                rim.SetData(areaResRef, ResourceType.GFF, areaStateGffData);
            }

            // Serialize RIM to bytes
            // Based on Andastra.Parsing RIMBinaryWriter implementation
            var writer = new RIMBinaryWriter(rim);
            return writer.Write();
        }

        /// <summary>
        /// Serializes area state to GFF format.
        /// Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 saves entity states to GFF format
        /// Creates a GFF with area state data including entity states, destroyed entities, spawned entities, and local variables.
        /// </summary>
        private byte[] SerializeAreaStateToGff(AreaState areaState)
        {
            if (areaState == null)
            {
                return null;
            }

            // Create GFF for area state
            // Note: This is not an ARE file, but a custom GFF structure for area state
            // ARE files contain static area properties, while this GFF contains dynamic state
            var gff = new GFF(GFFContent.ARE); // Use ARE content type for area-related data
            var root = gff.Root;

            // Area ResRef
            root.SetString("AreaResRef", areaState.AreaResRef ?? "");

            // Visited flag
            root.SetUInt8("Visited", areaState.Visited ? (byte)1 : (byte)0);

            // Serialize entity state lists
            SerializeEntityStateListToGff(root, "CreatureList", areaState.CreatureStates);
            SerializeEntityStateListToGff(root, "DoorList", areaState.DoorStates);
            SerializeEntityStateListToGff(root, "PlaceableList", areaState.PlaceableStates);
            SerializeEntityStateListToGff(root, "TriggerList", areaState.TriggerStates);
            SerializeEntityStateListToGff(root, "StoreList", areaState.StoreStates);
            SerializeEntityStateListToGff(root, "SoundList", areaState.SoundStates);
            SerializeEntityStateListToGff(root, "WaypointList", areaState.WaypointStates);
            SerializeEntityStateListToGff(root, "EncounterList", areaState.EncounterStates);
            SerializeEntityStateListToGff(root, "CameraList", areaState.CameraStates);

            // Destroyed entity IDs
            if (areaState.DestroyedEntityIds != null && areaState.DestroyedEntityIds.Count > 0)
            {
                var destroyedList = root.Acquire<GFFList>("DestroyedList", new GFFList());
                foreach (uint entityId in areaState.DestroyedEntityIds)
                {
                    GFFStruct item = destroyedList.Add();
                    item.SetUInt32("ObjectId", entityId);
                }
            }

            // Spawned entities
            if (areaState.SpawnedEntities != null && areaState.SpawnedEntities.Count > 0)
            {
                var spawnedList = root.Acquire<GFFList>("SpawnedList", new GFFList());
                foreach (SpawnedEntityState spawnedEntity in areaState.SpawnedEntities)
                {
                    GFFStruct item = spawnedList.Add();
                    SerializeEntityStateToGff(item, spawnedEntity);
                    item.SetString("BlueprintResRef", spawnedEntity.BlueprintResRef ?? "");
                    item.SetString("SpawnedBy", spawnedEntity.SpawnedBy ?? "");
                }
            }

            // Local variables
            if (areaState.LocalVariables != null && !areaState.LocalVariables.IsEmpty)
            {
                var localVarsStruct = root.Acquire<GFFStruct>("LocalVars", new GFFStruct());
                SerializeLocalVariablesToGff(localVarsStruct, areaState.LocalVariables);
            }

            // Serialize GFF to bytes
            return new GFFBinaryWriter(gff).Write();
        }

        /// <summary>
        /// Serializes a list of entity states to a GFF list.
        /// </summary>
        private void SerializeEntityStateListToGff(GFFStruct root, string listName, List<EntityState> entityStates)
        {
            if (entityStates == null || entityStates.Count == 0)
            {
                return;
            }

            var list = root.Acquire<GFFList>(listName, new GFFList());
            foreach (EntityState entityState in entityStates)
            {
                GFFStruct item = list.Add();
                SerializeEntityStateToGff(item, entityState);
            }
        }

        /// <summary>
        /// Serializes an entity state to a GFF struct.
        /// Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 saves entity states to GFF format
        /// </summary>
        private void SerializeEntityStateToGff(GFFStruct structData, EntityState entityState)
        {
            if (entityState == null)
            {
                return;
            }

            // Tag
            structData.SetString("Tag", entityState.Tag ?? "");

            // ObjectId
            structData.SetUInt32("ObjectId", entityState.ObjectId);

            // ObjectType
            structData.SetUInt32("ObjectType", (uint)(int)entityState.ObjectType);

            // TemplateResRef
            if (!string.IsNullOrEmpty(entityState.TemplateResRef))
            {
                structData.SetResRef("TemplateResRef", ResRef.FromString(entityState.TemplateResRef));
            }

            // Position
            structData.SetVector3("Position", new System.Numerics.Vector3(
                entityState.Position.X,
                entityState.Position.Y,
                entityState.Position.Z));

            // Facing
            structData.SetSingle("Facing", entityState.Facing);

            // HP
            structData.SetInt32("CurrentHP", entityState.CurrentHP);
            structData.SetInt32("MaxHP", entityState.MaxHP);

            // Flags
            structData.SetUInt8("IsDestroyed", entityState.IsDestroyed ? (byte)1 : (byte)0);
            structData.SetUInt8("IsPlot", entityState.IsPlot ? (byte)1 : (byte)0);
            structData.SetUInt8("IsOpen", entityState.IsOpen ? (byte)1 : (byte)0);
            structData.SetUInt8("IsLocked", entityState.IsLocked ? (byte)1 : (byte)0);
            structData.SetInt32("AnimationState", entityState.AnimationState);

            // Local variables
            if (entityState.LocalVariables != null && !entityState.LocalVariables.IsEmpty)
            {
                var localVarsStruct = structData.Acquire<GFFStruct>("LocalVars", new GFFStruct());
                SerializeLocalVariablesToGff(localVarsStruct, entityState.LocalVariables);
            }

            // Active effects
            if (entityState.ActiveEffects != null && entityState.ActiveEffects.Count > 0)
            {
                var effectsList = structData.Acquire<GFFList>("Effects", new GFFList());
                foreach (SavedEffect effect in entityState.ActiveEffects)
                {
                    GFFStruct effectStruct = effectsList.Add();
                    effectStruct.SetInt32("EffectType", effect.EffectType);
                    effectStruct.SetInt32("SubType", effect.SubType);
                    effectStruct.SetInt32("DurationType", effect.DurationType);
                    effectStruct.SetSingle("RemainingDuration", effect.RemainingDuration);
                    effectStruct.SetUInt32("CreatorId", effect.CreatorId);
                    effectStruct.SetInt32("SpellId", effect.SpellId);

                    // Effect parameters (if any)
                    if (effect.IntParams != null && effect.IntParams.Count > 0)
                    {
                        var intParamsList = effectStruct.Acquire<GFFList>("IntParams", new GFFList());
                        foreach (int param in effect.IntParams)
                        {
                            GFFStruct paramStruct = intParamsList.Add();
                            paramStruct.SetInt32("Value", param);
                        }
                    }

                    if (effect.FloatParams != null && effect.FloatParams.Count > 0)
                    {
                        var floatParamsList = effectStruct.Acquire<GFFList>("FloatParams", new GFFList());
                        foreach (float param in effect.FloatParams)
                        {
                            GFFStruct paramStruct = floatParamsList.Add();
                            paramStruct.SetSingle("Value", param);
                        }
                    }

                    if (effect.StringParams != null && effect.StringParams.Count > 0)
                    {
                        var stringParamsList = effectStruct.Acquire<GFFList>("StringParams", new GFFList());
                        foreach (string param in effect.StringParams)
                        {
                            GFFStruct paramStruct = stringParamsList.Add();
                            paramStruct.SetString("Value", param ?? "");
                        }
                    }

                    if (effect.ObjectParams != null && effect.ObjectParams.Count > 0)
                    {
                        var objectParamsList = effectStruct.Acquire<GFFList>("ObjectParams", new GFFList());
                        foreach (uint param in effect.ObjectParams)
                        {
                            GFFStruct paramStruct = objectParamsList.Add();
                            paramStruct.SetUInt32("Value", param);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Serializes local variables to a GFF struct.
        /// </summary>
        private void SerializeLocalVariablesToGff(GFFStruct varsStruct, LocalVariableSet localVars)
        {
            if (localVars == null)
            {
                return;
            }

            // Int variables
            if (localVars.Ints != null && localVars.Ints.Count > 0)
            {
                var intList = varsStruct.Acquire<GFFList>("Ints", new GFFList());
                foreach (var kvp in localVars.Ints)
                {
                    GFFStruct item = intList.Add();
                    item.SetString("Name", kvp.Key ?? "");
                    item.SetInt32("Value", kvp.Value);
                }
            }

            // Float variables
            if (localVars.Floats != null && localVars.Floats.Count > 0)
            {
                var floatList = varsStruct.Acquire<GFFList>("Floats", new GFFList());
                foreach (var kvp in localVars.Floats)
                {
                    GFFStruct item = floatList.Add();
                    item.SetString("Name", kvp.Key ?? "");
                    item.SetSingle("Value", kvp.Value);
                }
            }

            // String variables
            if (localVars.Strings != null && localVars.Strings.Count > 0)
            {
                var stringList = varsStruct.Acquire<GFFList>("Strings", new GFFList());
                foreach (var kvp in localVars.Strings)
                {
                    GFFStruct item = stringList.Add();
                    item.SetString("Name", kvp.Key ?? "");
                    item.SetString("Value", kvp.Value ?? "");
                }
            }

            // Object variables
            if (localVars.Objects != null && localVars.Objects.Count > 0)
            {
                var objectList = varsStruct.Acquire<GFFList>("Objects", new GFFList());
                foreach (var kvp in localVars.Objects)
                {
                    GFFStruct item = objectList.Add();
                    item.SetString("Name", kvp.Key ?? "");
                    item.SetUInt32("Value", kvp.Value);
                }
            }

            // Location variables
            if (localVars.Locations != null && localVars.Locations.Count > 0)
            {
                var locationList = varsStruct.Acquire<GFFList>("Locations", new GFFList());
                foreach (var kvp in localVars.Locations)
                {
                    GFFStruct item = locationList.Add();
                    item.SetString("Name", kvp.Key ?? "");
                    if (kvp.Value != null)
                    {
                        var locationStruct = item.Acquire<GFFStruct>("Location", new GFFStruct());
                        locationStruct.SetString("AreaResRef", kvp.Value.AreaResRef ?? "");
                        locationStruct.SetVector3("Position", new System.Numerics.Vector3(
                            kvp.Value.Position.X,
                            kvp.Value.Position.Y,
                            kvp.Value.Position.Z));
                        locationStruct.SetSingle("Facing", kvp.Value.Facing);
                    }
                }
            }
        }

        #endregion
    }
}
