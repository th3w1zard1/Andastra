using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Save;
using Andastra.Game.Games.Odyssey.Components;
using Andastra.Game.Games.Odyssey.Data;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Odyssey.Systems;
using Andastra.Game.Scripting.Interfaces;
using Andastra.Game.Scripting.Types;
using JetBrains.Annotations;
using Loading = Andastra.Game.Games.Odyssey.Loading;
using ObjectType = Andastra.Runtime.Core.Enums.ObjectType;
using RuntimeIModule = Andastra.Runtime.Core.Interfaces.IModule;

namespace Andastra.Game.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine (KotOR/KotOR2) save game serializer implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Save Serializer Implementation:
    /// - Based on swkotor.exe and swkotor2.exe save systems
    /// - Uses GFF format with "SAV " and "NFO " signatures
    /// - Handles entity serialization, global variables, party data
    ///
    /// Based on reverse engineering of:
    /// - swkotor2.exe: SerializeSaveNfo @ 0x004eb750 for metadata creation
    /// - swkotor2.exe: Global variable save/load functions
    /// - Entity serialization: 0x004e28c0 save, 0x005fb0f0 load
    /// - Party management and companion state saving
    ///
    /// Save file structure:
    /// - Save directory with numbered subdirectories
    /// - NFO file: Metadata (name, time, area, screenshot)
    /// - SAV file: Main save data (entities, globals, party)
    /// - RES directory: Screenshots and additional resources
    /// - GFF format for structured data storage
    /// </remarks>
    [PublicAPI]
    public class OdysseySaveSerializer : BaseSaveSerializer
    {
        [CanBeNull] private readonly GameDataManager _gameDataManager;
        [CanBeNull] private readonly string _savesDirectory;
        [CanBeNull] private Scripting.Interfaces.IScriptGlobals _scriptGlobals;

        /// <summary>
        /// Initializes a new instance of the OdysseySaveSerializer class.
        /// </summary>
        /// <param name="gameDataManager">Optional game data manager for partytable.2da lookups. If null, falls back to hardcoded mapping.</param>
        /// <param name="savesDirectory">Optional saves directory path. If null, CreateSaveDirectory will require saves directory to be provided via other means.</param>
        public OdysseySaveSerializer([CanBeNull] GameDataManager gameDataManager = null, [CanBeNull] string savesDirectory = null)
        {
            _gameDataManager = gameDataManager;
            _savesDirectory = savesDirectory;
        }

        /// <summary>
        /// Sets the script globals instance for saving/loading local variables.
        /// </summary>
        /// <param name="scriptGlobals">The script globals instance to use for variable operations.</param>
        public void SetScriptGlobals([CanBeNull] Scripting.Interfaces.IScriptGlobals scriptGlobals)
        {
            _scriptGlobals = scriptGlobals;
        }
        /// <summary>
        /// Gets the save file format version for Odyssey engine.
        /// </summary>
        /// <remarks>
        /// KotOR uses version 1, KotOR2 uses version 2.
        /// Used for compatibility checking between game versions.
        /// </remarks>
        protected override int SaveVersion => 2; // KotOR 2 version

        /// <summary>
        /// Gets the engine identifier.
        /// </summary>
        /// <remarks>
        /// Identifies this as an Odyssey engine save.
        /// Used for cross-engine compatibility detection.
        /// </remarks>
        protected override string EngineIdentifier => "Odyssey";

        /// <summary>
        /// Serializes save game metadata to NFO format.
        /// </summary>
        /// <remarks>
        /// Based on SerializeSaveNfo @ 0x004eb750 in swkotor2.exe.
        /// Creates GFF with "NFO " signature containing save information.
        /// Includes SAVEGAMENAME, TIMEPLAYED, AREANAME, and metadata.
        ///
        /// NFO structure:
        /// - Signature: "NFO "
        /// - Version: "V2.0" for KotOR 2
        /// - SAVEGAMENAME: Display name
        /// - TIMEPLAYED: Play time in seconds
        /// - AREANAME: Current area resource
        /// - LASTMODIFIED: Timestamp
        ///
        /// Original implementation (swkotor2.exe: 0x004eb750):
        /// 1. Creates GFF with "NFO " signature (4 bytes) and "V2.0" version string
        /// 2. Writes fields in this exact order:
        ///    - AREANAME (string): Current area name from module state
        ///    - LASTMODULE (string): Last module ResRef
        ///    - TIMEPLAYED (int32): Total seconds played (uint32 from party system)
        ///    - CHEATUSED (byte): Cheat used flag (bool converted to byte)
        ///    - SAVEGAMENAME (string): Save game name
        ///    - TIMESTAMP (int64): FILETIME structure (GetLocalTime + SystemTimeToFileTime)
        ///    - PCNAME (string): Player character name from party system
        ///    - SAVENUMBER (int32): Save slot number
        ///    - GAMEPLAYHINT (byte): Gameplay hint flag
        ///    - STORYHINT0-9 (bytes): Story hint flags (10 boolean flags)
        ///    - LIVECONTENT (byte): Bitmask for live content (1 << (i-1) for each enabled entry)
        ///    - LIVE1-9 (strings): Live content entry strings (up to 9 entries)
        ///    - PORTRAIT0-2 (ResRef): Player portrait resource references
        /// 3. File path: Constructs "SAVES:\{saveName}\savenfo" path
        /// Note: GFF signature is "NFO " (4 bytes), version string is "V2.0"
        /// </remarks>
        public override byte[] SerializeSaveNfo(Common.SaveGameData saveData)
        {
            if (saveData == null)
            {
                return new byte[0];
            }

            // Note: Andastra.Runtime.Games.Common.SaveGameData and Runtime.Core.Save.SaveGameData
            // are unrelated types, so we use reflection to check and access Core-specific properties
            object coreSaveData = null;
            Type coreSaveDataType = null;
            var saveDataType = saveData.GetType();
            var coreSaveDataTypeName = "Runtime.Core.Save.SaveGameData";
            if (saveDataType.FullName == coreSaveDataTypeName)
            {
                coreSaveData = saveData;
                coreSaveDataType = saveDataType;
            }
            else
            {
                // Try to get the type via reflection in case it's a derived type
                coreSaveDataType = Type.GetType(coreSaveDataTypeName);
                if (coreSaveDataType != null && coreSaveDataType.IsAssignableFrom(saveDataType))
                {
                    coreSaveData = saveData;
                }
            }

            // Create NFOData structure
            var nfo = new NFOData();

            // AREANAME: Current area name from CurrentAreaInstance or CurrentArea
            if (saveData.CurrentAreaInstance != null)
            {
                nfo.AreaName = saveData.CurrentAreaInstance.DisplayName ?? saveData.CurrentAreaInstance.ResRef ?? string.Empty;
            }
            else if (!string.IsNullOrEmpty(saveData.CurrentArea))
            {
                nfo.AreaName = saveData.CurrentArea;
            }
            else
            {
                nfo.AreaName = string.Empty;
            }

            // LASTMODULE: Last module ResRef
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SerializeSaveNfo @ 0x004eb750
            // Original implementation: LASTMODULE is the ResRef of the currently loaded module
            // Extraction priority:
            // 1. Direct from saveData.CurrentModule (most reliable)
            // 2. From CurrentAreaInstance via reflection (if area has Module property)
            // 3. From ModuleAreaMappings by finding which module contains the current area
            // 4. Fallback to empty string if all methods fail
            string lastModule = string.Empty;

            // Priority 1: Try to extract from CurrentAreaInstance using reflection
            if (saveData.CurrentAreaInstance != null)
            {
                try
                {
                    // Try to get module name from area instance via reflection
                    // Some area implementations have a Module property that references the parent module
                    var moduleProperty = saveData.CurrentAreaInstance.GetType().GetProperty("Module");
                    if (moduleProperty != null)
                    {
                        object moduleObj = moduleProperty.GetValue(saveData.CurrentAreaInstance);
                        if (moduleObj != null)
                        {
                            var resRefProperty = moduleObj.GetType().GetProperty("ResRef");
                            if (resRefProperty != null)
                            {
                                object resRefObj = resRefProperty.GetValue(moduleObj);
                                if (resRefObj != null)
                                {
                                    lastModule = resRefObj.ToString();
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Reflection failed - try next method
                }

                // Priority 2: Try ModuleAreaMappings if available (only on Core.SaveGameData)
                if (string.IsNullOrEmpty(lastModule) && coreSaveData != null)
                {
                    try
                    {
                        var moduleAreaMappingsProperty = coreSaveDataType.GetProperty("ModuleAreaMappings");
                        if (moduleAreaMappingsProperty != null)
                        {
                            var moduleAreaMappings = moduleAreaMappingsProperty.GetValue(coreSaveData) as Dictionary<string, List<string>>;
                            if (moduleAreaMappings != null && moduleAreaMappings.Count > 0)
                            {
                                string areaResRef = saveData.CurrentAreaInstance.ResRef;
                                if (!string.IsNullOrEmpty(areaResRef))
                                {
                                    foreach (var kvp in moduleAreaMappings)
                                    {
                                        string moduleResRef = kvp.Key;
                                        List<string> areaList = kvp.Value;
                                        if (areaList != null && areaList.Contains(areaResRef, StringComparer.OrdinalIgnoreCase))
                                        {
                                            lastModule = moduleResRef;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Reflection failed - skip this method
                    }
                }
            }

            // Priority 3: Try to infer from CurrentArea string if CurrentAreaInstance is null (only on Core.SaveGameData)
            if (string.IsNullOrEmpty(lastModule) && !string.IsNullOrEmpty(saveData.CurrentArea) && coreSaveData != null)
            {
                try
                {
                    var moduleAreaMappingsProperty = coreSaveDataType.GetProperty("ModuleAreaMappings");
                    if (moduleAreaMappingsProperty != null)
                    {
                        var moduleAreaMappings = moduleAreaMappingsProperty.GetValue(coreSaveData) as Dictionary<string, List<string>>;
                        if (moduleAreaMappings != null && moduleAreaMappings.Count > 0)
                        {
                            foreach (var kvp in moduleAreaMappings)
                            {
                                string moduleResRef = kvp.Key;
                                List<string> areaList = kvp.Value;
                                if (areaList != null && areaList.Contains(saveData.CurrentArea, StringComparer.OrdinalIgnoreCase))
                                {
                                    lastModule = moduleResRef;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Reflection failed - skip this method
                }
            }

            nfo.LastModule = lastModule ?? string.Empty;

            // SAVEGAMENAME: Display name of the save
            nfo.SavegameName = saveData.SaveName ?? string.Empty;

            // TIMEPLAYED: Total seconds played
            nfo.TimePlayedSeconds = saveData.TimePlayed;

            // TIMESTAMP: FILETIME structure
            if (saveData.Timestamp != null && saveData.Timestamp != default(DateTime))
            {
                long fileTime = saveData.Timestamp.ToFileTime();
                if (fileTime > 0)
                {
                    nfo.TimestampFileTime = (ulong)fileTime;
                }
            }

            // CHEATUSED: Cheat used flag
            // Try to get from GameState globals, otherwise default to false
            bool cheatUsed = false;
            if (saveData.GameState != null)
            {
                try
                {
                    cheatUsed = saveData.GameState.GetGlobal<bool>("CHEATUSED", false);
                }
                catch
                {
                    // If GetGlobal fails, use default
                    cheatUsed = false;
                }
            }
            nfo.CheatUsed = cheatUsed;

            // GAMEPLAYHINT: Gameplay hint flag
            byte gameplayHint = 0;
            if (saveData.GameState != null)
            {
                try
                {
                    bool hintValue = saveData.GameState.GetGlobal<bool>("GAMEPLAYHINT", false);
                    gameplayHint = (byte)(hintValue ? 1 : 0);
                }
                catch
                {
                    gameplayHint = 0;
                }
            }
            nfo.GameplayHint = gameplayHint;

            // STORYHINT0-9: Story hint flags (10 boolean flags)
            // Try to get from GameState globals
            if (saveData.GameState != null)
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        string hintField = "STORYHINT" + i;
                        bool hintValue = saveData.GameState.GetGlobal<bool>(hintField, false);
                        if (i < nfo.StoryHints.Count)
                        {
                            nfo.StoryHints[i] = hintValue;
                        }
                    }
                    catch
                    {
                        // If GetGlobal fails, leave default (false)
                    }
                }
            }

            // LIVECONTENT: Bitmask for live content
            // Try to get from GameState globals
            byte liveContentBitmask = 0;
            if (saveData.GameState != null)
            {
                try
                {
                    // Try to get as a list of booleans
                    var liveContentList = saveData.GameState.GetGlobal<List<bool>>("LIVECONTENT", null);
                    if (liveContentList != null)
                    {
                        for (int i = 0; i < liveContentList.Count && i < 32; i++)
                        {
                            if (liveContentList[i])
                            {
                                liveContentBitmask |= (byte)(1 << (i & 0x1F));
                            }
                        }
                    }
                    else
                    {
                        // Try to get as a byte bitmask directly
                        liveContentBitmask = saveData.GameState.GetGlobal<byte>("LIVECONTENT", 0);
                    }
                }
                catch
                {
                    liveContentBitmask = 0;
                }
            }
            nfo.LiveContentBitmask = liveContentBitmask;

            // LIVE1-9: Live content entry strings (up to 9 entries)
            if (saveData.GameState != null)
            {
                for (int i = 1; i <= 9; i++)
                {
                    try
                    {
                        string liveField = "LIVE" + i;
                        string liveValue = saveData.GameState.GetGlobal<string>(liveField, null);
                        if (!string.IsNullOrEmpty(liveValue))
                        {
                            int index = i - 1;
                            if (index < nfo.LiveEntries.Count)
                            {
                                nfo.LiveEntries[index] = liveValue;
                            }
                        }
                    }
                    catch
                    {
                        // If GetGlobal fails, leave default (empty string)
                    }
                }
            }

            // PORTRAIT0-2: Player portrait resource references
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): SerializeSaveNfo @ 0x004eb750
            // Portrait order: PORTRAIT0 = leader, PORTRAIT1/2 = selected party members (excluding leader)
            // Portraits are stored as ResRefs extracted from portraits.2da using PortraitId from creature components
            // Original implementation: Gets PortraitId from each party member's UTC data, looks up ResRef in portraits.2da
            nfo.Portrait0 = ResRef.FromBlank();
            nfo.Portrait1 = ResRef.FromBlank();
            nfo.Portrait2 = ResRef.FromBlank();

            if (saveData.PartyState != null && _gameDataManager != null)
            {
                try
                {
                    // Get leader portrait (PORTRAIT0)
                    if (saveData.PartyState.Leader != null)
                    {
                        CreatureComponent leaderCreature = saveData.PartyState.Leader.GetComponent<CreatureComponent>();
                        if (leaderCreature != null && leaderCreature.PortraitId >= 0)
                        {
                            GameDataManager.PortraitData leaderPortrait = _gameDataManager.GetPortrait(leaderCreature.PortraitId);
                            if (leaderPortrait != null && !string.IsNullOrEmpty(leaderPortrait.BaseResRef))
                            {
                                nfo.Portrait0 = ResRef.FromString(leaderPortrait.BaseResRef);
                            }
                        }
                    }

                    // Get selected party member portraits (PORTRAIT1, PORTRAIT2)
                    // Order: First two selected members that are not the leader
                    int portraitIndex = 1; // Start at PORTRAIT1
                    if (saveData.PartyState.Members != null)
                    {
                        string leaderTag = saveData.PartyState.Leader?.Tag;
                        foreach (IEntity member in saveData.PartyState.Members)
                        {
                            // Skip if we've filled both portrait slots
                            if (portraitIndex > 2)
                            {
                                break;
                            }

                            // Skip leader (already in PORTRAIT0)
                            if (member == null || (leaderTag != null && string.Equals(member.Tag, leaderTag, System.StringComparison.OrdinalIgnoreCase)))
                            {
                                continue;
                            }

                            // Get portrait from creature component
                            CreatureComponent memberCreature = member.GetComponent<CreatureComponent>();
                            if (memberCreature != null && memberCreature.PortraitId >= 0)
                            {
                                GameDataManager.PortraitData memberPortrait = _gameDataManager.GetPortrait(memberCreature.PortraitId);
                                if (memberPortrait != null && !string.IsNullOrEmpty(memberPortrait.BaseResRef))
                                {
                                    if (portraitIndex == 1)
                                    {
                                        nfo.Portrait1 = ResRef.FromString(memberPortrait.BaseResRef);
                                    }
                                    else if (portraitIndex == 2)
                                    {
                                        nfo.Portrait2 = ResRef.FromString(memberPortrait.BaseResRef);
                                    }
                                    portraitIndex++;
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // If portrait extraction fails, leave as blank ResRefs (graceful degradation)
                    // This can happen if GameDataManager is not initialized or portraits.2da is missing
                }
            }

            // PCNAME: Player character name from party system
            string pcName = string.Empty;
            if (saveData.PartyState != null)
            {
                try
                {
                    // Convert IPartyState to PartyState to use helper method
                    PartyState partyState = ConvertToPartyState(saveData.PartyState);
                    pcName = GetPlayerCharacterName(partyState);
                }
                catch
                {
                    // If conversion fails, try to get from Leader directly
                    if (saveData.PartyState.Leader != null)
                    {
                        pcName = saveData.PartyState.Leader.Tag ?? string.Empty;
                    }
                }
            }
            nfo.PcName = pcName ?? string.Empty;

            // Serialize NFOData to GFF bytes using NFOAuto
            // This creates a GFF with "NFO " signature and "V2.0" version
            return NFOAuto.BytesNfo(nfo);
        }

        /// <summary>
        /// Deserializes save game metadata from NFO format.
        /// </summary>
        /// <remarks>
        /// Reads NFO GFF and extracts save metadata.
        /// Validates NFO signature and version compatibility.
        /// Returns structured metadata for save game display.
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00707290 @ 0x00707290 (NFO loading)
        /// Based on swkotor.exe: 0x006c8e50 @ 0x006c8e50 (NFO loading)
        /// Located via string reference: "savenfo" @ 0x007be1f0
        ///
        /// Original implementation:
        /// 1. Loads GFF file from savenfo.res
        /// 2. Validates GFF signature is "NFO " (4 bytes)
        /// 3. Validates GFF version is "V1.0" (K1) or "V2.0" (K2)
        /// 4. Reads metadata fields from GFF root structure:
        ///    - AREANAME (string): Current area name
        ///    - LASTMODULE (string): Last module ResRef
        ///    - SAVEGAMENAME (string): Save game display name
        ///    - TIMEPLAYED (int32): Total seconds played
        ///    - TIMESTAMP (int64): FILETIME structure (Windows file time)
        ///    - PCNAME (string): Player character name
        ///    - CHEATUSED (byte): Cheat used flag
        ///    - GAMEPLAYHINT (byte): Gameplay hint flag
        ///    - STORYHINT0-9 (bytes): Story hint flags (10 boolean flags, K2 only)
        ///    - STORYHINT (byte): Legacy story hint (K1 only)
        ///    - LIVECONTENT (byte): Bitmask for live content
        ///    - LIVE1-9 (strings): Live content entry strings (up to 9 entries)
        ///    - PORTRAIT0-2 (ResRef): Player portrait resource references
        /// 5. Converts NFO data to SaveGameMetadata structure
        ///
        /// Error handling:
        /// - Throws ArgumentNullException if nfoData is null
        /// - Throws InvalidDataException if data is too small or invalid GFF
        /// - Throws InvalidDataException if GFF signature is not "NFO "
        /// - Throws InvalidDataException if GFF version is unsupported
        /// </remarks>
        public override SaveGameMetadata DeserializeSaveNfo(byte[] nfoData)
        {
            if (nfoData == null)
            {
                throw new ArgumentNullException(nameof(nfoData), "NFO data cannot be null");
            }

            if (nfoData.Length == 0)
            {
                throw new InvalidDataException("NFO data cannot be empty");
            }

            // Validate minimum GFF header size (56 bytes for GFF header)
            // GFF header: 4 bytes signature + 4 bytes version + 11 * 4 bytes offsets/counts = 56 bytes
            if (nfoData.Length < 56)
            {
                throw new InvalidDataException($"NFO data is too small ({nfoData.Length} bytes). Minimum GFF header size is 56 bytes.");
            }

            // Read and validate GFF signature
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GFF signature validation @ 0x00707290
            // Original implementation checks first 4 bytes for "NFO " signature
            string signature = Encoding.ASCII.GetString(nfoData, 0, 4);
            if (signature != "NFO ")
            {
                throw new InvalidDataException($"Invalid NFO signature. Expected 'NFO ', got '{signature}'");
            }

            // Read GFF version (bytes 4-7)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Version validation @ 0x00707290
            // Original implementation: K1 uses "V1.0", K2 uses "V2.0"
            // V3.2 is the modern GFF format used by BioWare.NET, but we accept it for compatibility
            // Note: BioWare.NET GFFBinaryWriter writes V3.2, but we can still read it
            string version = Encoding.ASCII.GetString(nfoData, 4, 4);
            if (version != "V1.0" && version != "V2.0" && version != "V3.2")
            {
                throw new InvalidDataException($"Unsupported NFO version. Expected 'V1.0', 'V2.0', or 'V3.2', got '{version}'");
            }

            // Read NFO data using NFOAuto.ReadNfo
            // This internally uses GFFAuto.ReadGff and NFOHelpers.ConstructNfo
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00707290 @ 0x00707290 reads GFF and constructs NFO data
            // Located via string reference: "savenfo" @ 0x007be1f0
            NFOData nfo;
            try
            {
                nfo = NFOAuto.ReadNfo(nfoData);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to read NFO data from GFF: {ex.Message}", ex);
            }

            // Validate that the GFF content type is NFO
            // This is a double-check since we already validated the signature
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Content type validation @ 0x00707290
            // Explicitly specify default parameters to resolve overload ambiguity
            GFF gff = GFFAuto.ReadGff(nfoData, 0, null);
            if (gff.Content != GFFContent.NFO)
            {
                throw new InvalidDataException($"GFF content type is not NFO. Got: {gff.Content}");
            }

            // Convert NFOData to SaveGameMetadata
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Metadata extraction @ 0x00707290
            // Original implementation extracts fields in this order:
            // 1. SAVEGAMENAME -> SaveName
            // 2. AREANAME -> AreaName
            // 3. TIMEPLAYED -> TimePlayed
            // 4. TIMESTAMP -> Timestamp (converted from FILETIME)
            // 5. Engine version detection (K1 vs K2 based on version string)
            var metadata = new SaveGameMetadata
            {
                SaveName = nfo.SavegameName ?? string.Empty,
                AreaName = nfo.AreaName ?? string.Empty,
                TimePlayed = nfo.TimePlayedSeconds,
                EngineVersion = "Odyssey",
                SaveVersion = (version == "V1.0") ? 1 : 2 // K1 uses version 1, K2 uses version 2
            };

            // Convert FILETIME to DateTime
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TIMESTAMP field handling @ 0x00707290 line 205
            // Original implementation: TIMESTAMP is stored as FILETIME (64-bit unsigned integer)
            // Windows FILETIME represents 100-nanosecond intervals since January 1, 1601
            if (nfo.TimestampFileTime.HasValue)
            {
                try
                {
                    // FILETIME is stored as ulong, convert to DateTime
                    // DateTime.FromFileTime expects long, but FILETIME is unsigned
                    // Handle potential overflow by checking if value is within valid range
                    ulong fileTime = nfo.TimestampFileTime.Value;
                    if (fileTime <= (ulong)long.MaxValue)
                    {
                        metadata.Timestamp = DateTime.FromFileTime((long)fileTime);
                    }
                    else
                    {
                        // If FILETIME exceeds long.MaxValue, use current time as fallback
                        // This should not happen in practice, but handle gracefully
                        metadata.Timestamp = DateTime.Now;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // If FILETIME is out of valid DateTime range, use current time as fallback
                    // This handles edge cases where FILETIME might be corrupted or invalid
                    metadata.Timestamp = DateTime.Now;
                }
            }
            else
            {
                // If TIMESTAMP is not present, use current time as default
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Default timestamp handling @ 0x00707290
                metadata.Timestamp = DateTime.Now;
            }

            // Validate that we have at least a save name
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Save name validation @ 0x00707290 line 173
            // Original implementation: If SAVEGAMENAME is missing or empty, defaults to "Old Save Game"
            if (string.IsNullOrEmpty(metadata.SaveName))
            {
                metadata.SaveName = "Old Save Game";
            }

            return metadata;
        }

        /// <summary>
        /// Serializes global game state.
        /// </summary>
        /// <remarks>
        /// Based on global variable serialization in swkotor2.exe.
        /// Saves quest states, player choices, persistent variables.
        /// Uses GFF format with variable categories.
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005ac670 @ 0x005ac670 (calls 0x005ab310 @ 0x005ab310 internally)
        /// Located via string reference: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Creates GFF with "GLOB" signature (for save games) containing VariableList array
        /// GFF structure: VariableList array with VariableName, VariableType, VariableValue fields
        /// VariableType values: 0 = BOOLEAN, 1 = INT, 3 = STRING
        ///
        /// Note: Location globals are not supported in VariableList format used by save games.
        /// The VariableList format is used for GLOBALVARS.res in save game ERF archives.
        ///
        /// Global categories:
        /// - QUEST: Quest completion states
        /// - CHOICE: Player dialogue choices
        /// - PERSISTENT: Long-term game state
        /// - MODULE: Per-module variables
        /// </remarks>
        public override byte[] SerializeGlobals(IGameState gameState)
        {
            if (gameState == null)
            {
                // Return empty GFF if game state is null
                var emptyGff = new GFF();
                var emptyRoot = emptyGff.Root;
                var emptyVarList = new GFFList();
                emptyRoot.SetList("VariableList", emptyVarList);
                return emptyGff.ToBytes();
            }

            // Create GFF with VariableList structure
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005ac670 @ 0x005ac670
            // GLOB GFF structure: VariableList array with VariableName, VariableType, VariableValue
            var gff = new GFF();
            var root = gff.Root;
            var varList = new GFFList();
            root.SetList("VariableList", varList);

            // Get all global variable names from game state
            // Based on IGameState interface: GetGlobalNames() returns all variable names
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005ac670 @ 0x005ac670 - no explicit error handling in original,
            // but we handle cases where GetGlobalNames is not implemented or fails gracefully
            IEnumerable<string> globalNames;
            try
            {
                globalNames = gameState.GetGlobalNames();
            }
            catch (NotImplementedException)
            {
                // GetGlobalNames is not implemented by this IGameState implementation
                // Return empty GFF with VariableList structure (matches original behavior when no globals exist)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Empty VariableList is valid GFF structure
                return gff.ToBytes();
            }
            catch (InvalidOperationException)
            {
                // GetGlobalNames failed due to invalid state (e.g., game state not initialized)
                // Return empty GFF with VariableList structure
                return gff.ToBytes();
            }
            catch (NullReferenceException)
            {
                // GetGlobalNames failed due to null reference (internal implementation error)
                // Return empty GFF with VariableList structure
                return gff.ToBytes();
            }
            catch (Exception)
            {
                // GetGlobalNames failed for any other reason
                // Return empty GFF with VariableList structure to ensure save file is still valid
                // This matches original engine behavior: if global enumeration fails, save continues with empty globals
                return gff.ToBytes();
            }

            if (globalNames == null)
            {
                // GetGlobalNames returned null (should not happen, but handle gracefully)
                // Return empty GFF with VariableList structure
                return gff.ToBytes();
            }

            // Track which variables we've already serialized to avoid duplicates
            var processedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Iterate through all global variable names and serialize them by type
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Processes each variable type separately
            foreach (string varName in globalNames)
            {
                if (string.IsNullOrEmpty(varName) || processedVariables.Contains(varName))
                {
                    // Skip empty names or already processed variables
                    continue;
                }

                // Check if variable exists in game state
                if (!gameState.HasGlobal(varName))
                {
                    continue;
                }

                // Determine variable type by attempting to retrieve it as each type
                // Priority: bool (type 0), int (type 1), string (type 3)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Variables are stored in separate typed dictionaries
                // The IGameState interface routes GetGlobal<T> to the appropriate dictionary based on T
                // Based on SaveSystem implementation: ScriptGlobals uses separate dictionaries (_globalBools, _globalInts, _globalStrings)

                bool serialized = false;

                // Boolean variables: Try to get as bool first (most specific type)
                // IGameState.GetGlobal<bool> should route to the bool dictionary if the variable is stored as bool
                try
                {
                    bool boolValue = gameState.GetGlobal<bool>(varName, false);
                    // If GetGlobal<bool> succeeds (doesn't throw), the variable is stored as bool
                    // Note: false is a valid bool value, so we serialize even if the value is false
                    var varStruct = varList.Add();
                    SetStringField(varStruct, "VariableName", varName);
                    SetIntField(varStruct, "VariableType", 0); // BOOLEAN
                    SetIntField(varStruct, "VariableValue", boolValue ? 1 : 0);
                    processedVariables.Add(varName);
                    serialized = true;
                }
                catch
                {
                    // GetGlobal<bool> threw exception - variable is not stored as bool, try next type
                }

                if (serialized)
                {
                    continue;
                }

                // Integer variables: Try to get as int
                // IGameState.GetGlobal<int> should route to the int dictionary if the variable is stored as int
                try
                {
                    int intValue = gameState.GetGlobal<int>(varName, 0);
                    // If GetGlobal<int> succeeds (doesn't throw), the variable is stored as int
                    // Note: 0 is a valid int value, so we serialize even if the value is 0
                    var varStruct = varList.Add();
                    SetStringField(varStruct, "VariableName", varName);
                    SetIntField(varStruct, "VariableType", 1); // INT
                    SetIntField(varStruct, "VariableValue", intValue);
                    processedVariables.Add(varName);
                    serialized = true;
                }
                catch
                {
                    // GetGlobal<int> threw exception - variable is not stored as int, try next type
                }

                if (serialized)
                {
                    continue;
                }

                // String variables: Try to get as string
                // IGameState.GetGlobal<string> should route to the string dictionary if the variable is stored as string
                try
                {
                    string stringValue = gameState.GetGlobal<string>(varName, string.Empty);
                    // If GetGlobal<string> succeeds (doesn't throw), the variable is stored as string
                    // Note: empty string is a valid string value, so we serialize even if the value is empty
                    var varStruct = varList.Add();
                    SetStringField(varStruct, "VariableName", varName);
                    SetIntField(varStruct, "VariableType", 3); // STRING
                    SetStringField(varStruct, "VariableValue", stringValue ?? string.Empty);
                    processedVariables.Add(varName);
                    serialized = true;
                }
                catch
                {
                    // GetGlobal<string> threw exception - variable is not stored as string
                }

                if (!serialized)
                {
                    // Variable exists (HasGlobal returned true) but couldn't be retrieved as any supported type
                    // This could happen for unsupported types like location, float, or custom objects
                    // Location globals are not supported in VariableList format (they use different GFF structure)
                    System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Skipping unsupported global variable type: {varName} (exists but not bool/int/string - may be location or other unsupported type)");
                }
            }

            // Serialize GFF to byte array
            return gff.ToBytes();
        }

        /// <summary>
        /// Deserializes global game state.
        /// </summary>
        /// <remarks>
        /// Restores global variables from save data.
        /// Updates quest states and player choice consequences.
        /// Validates variable integrity and types.
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005ac740 @ 0x005ac740 (global variables deserialization)
        /// Located via string reference: "GLOBALVARS" @ 0x007c27bc
        /// Original implementation: Reads GFF file from save game ERF archive, restores all global int/bool/string variables
        /// GFF structure: VariableList array with VariableName, VariableType, VariableValue fields
        /// VariableType values: 0 = BOOLEAN, 1 = INT, 3 = STRING
        /// </remarks>
        public override void DeserializeGlobals(byte[] globalsData, IGameState gameState)
        {
            if (globalsData == null || globalsData.Length == 0)
            {
                // Empty or null globals data - nothing to deserialize
                return;
            }

            if (gameState == null)
            {
                throw new ArgumentNullException(nameof(gameState), "GameState cannot be null for global variable deserialization");
            }

            // Parse GFF from byte array
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005ac540 @ 0x005ac540 loads GFF with "GVT " signature
            // However, save game GLOBALVARS uses "GLOB" signature with VariableList format
            GFF gff;
            try
            {
                gff = GFF.FromBytes(globalsData);
            }
            catch (Exception ex)
            {
                // Invalid GFF structure - log error but don't throw to allow save loading to continue
                System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to parse GLOBALVARS GFF: {ex.Message}");
                return;
            }

            if (gff == null || gff.Root == null)
            {
                // Invalid or empty GFF structure
                return;
            }

            // Extract VariableList from GFF root
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): VariableList contains all global variables with their types and values
            var varList = gff.Root.GetList("VariableList");
            if (varList == null)
            {
                // No VariableList found - empty globals
                return;
            }

            // Iterate through each variable in the list
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005abbe0 @ 0x005abbe0 processes each variable type
            foreach (GFFStruct varStruct in varList)
            {
                if (varStruct == null)
                {
                    continue;
                }

                // Extract variable name
                string varName = GetStringField(varStruct, "VariableName", null);
                if (string.IsNullOrEmpty(varName))
                {
                    // Skip variables without names
                    continue;
                }

                // Extract variable type
                // VariableType: 0 = BOOLEAN, 1 = INT, 3 = STRING
                int varType = GetIntField(varStruct, "VariableType", -1);

                // Process variable based on type
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Different handling for each variable type
                switch (varType)
                {
                    case 0: // BOOLEAN
                        {
                            // Boolean variables stored as int (0 = false, non-zero = true)
                            int boolVal = GetIntField(varStruct, "VariableValue", 0);
                            bool boolValue = boolVal != 0;

                            // Update game state with boolean global
                            try
                            {
                                gameState.SetGlobal(varName, boolValue);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to set boolean global '{varName}': {ex.Message}");
                            }
                            break;
                        }

                    case 1: // INT
                        {
                            // Integer variables stored as int32
                            int intVal = GetIntField(varStruct, "VariableValue", 0);

                            // Update game state with integer global
                            try
                            {
                                gameState.SetGlobal(varName, intVal);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to set integer global '{varName}': {ex.Message}");
                            }
                            break;
                        }

                    case 3: // STRING
                        {
                            // String variables stored as string field
                            string strVal = GetStringField(varStruct, "VariableValue", string.Empty);

                            // Update game state with string global
                            try
                            {
                                gameState.SetGlobal(varName, strVal ?? string.Empty);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to set string global '{varName}': {ex.Message}");
                            }
                            break;
                        }

                    default:
                        {
                            // Unknown variable type - log warning but continue processing other variables
                            System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Unknown variable type {varType} for global '{varName}', skipping");
                            break;
                        }
                }
            }
        }

        /// <summary>
        /// Helper method to safely get string field from GFF struct.
        /// </summary>
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

        /// <summary>
        /// Helper method to safely get integer field from GFF struct.
        /// </summary>
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

        /// <summary>
        /// Helper method to safely get single (float) field from GFF struct.
        /// </summary>
        private float GetSingleField(GFFStruct gffStruct, string fieldName, float defaultValue)
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

        /// <summary>
        /// Helper method to safely get uint8 (byte) field from GFF struct.
        /// </summary>
        private byte GetUInt8Field(GFFStruct gffStruct, string fieldName, byte defaultValue)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return defaultValue;
            }

            try
            {
                return gffStruct.GetUInt8(fieldName);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Helper method to safely set string field in GFF struct.
        /// </summary>
        private void SetStringField(GFFStruct gffStruct, string fieldName, string value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            try
            {
                gffStruct.SetString(fieldName, value ?? "");
            }
            catch
            {
                // Ignore errors when setting field
            }
        }

        /// <summary>
        /// Helper method to safely set integer field in GFF struct.
        /// </summary>
        private void SetIntField(GFFStruct gffStruct, string fieldName, int value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            try
            {
                gffStruct.SetInt32(fieldName, value);
            }
            catch
            {
                // Ignore errors when setting field
            }
        }

        /// <summary>
        /// Helper method to safely set float field in GFF struct.
        /// </summary>
        private void SetFloatField(GFFStruct gffStruct, string fieldName, float value)
        {
            if (gffStruct == null || string.IsNullOrEmpty(fieldName))
            {
                return;
            }

            try
            {
                gffStruct.SetSingle(fieldName, value);
            }
            catch
            {
                // Ignore errors when setting field
            }
        }

        /// <summary>
        /// Serializes party information.
        /// </summary>
        /// <remarks>
        /// Odyssey party serialization includes companions and their states.
        /// Saves companion approval, equipment, position, quest involvement.
        /// Includes party formation and leadership information.
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057bd70 @ 0x0057bd70
        /// Located via string reference: "PARTYTABLE" @ 0x007c1910
        /// Original implementation creates GFF with "PT  " signature and "V2.0" version.
        ///
        /// Party data includes:
        /// - Companion entities and their states
        /// - Approval ratings and relationship flags (K2 influence system)
        /// - Equipment and inventory
        /// - Position in party formation
        /// - Active quest involvement
        /// - Game state (gold, XP, components, chemicals, swoop times)
        /// - UI state (Pazaak cards, tutorial windows, messages, galaxy map)
        /// - AI and follow states
        /// </remarks>
        public override byte[] SerializeParty(IPartyState partyState)
        {
            // Use common helper to convert IPartyState to PartyState
            PartyState state = ConvertToPartyState(partyState);

            // Use BioWare.NET GFF writer
            // Original creates GFF with "PT  " signature and "V2.0" version
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057bd70 @ 0x0057bd70 creates GFF with "PT  " signature
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Note: BioWare.NET GFFBinaryWriter always writes "V3.2" version, but signature is correct
            var gff = new GFF(GFFContent.PT);
            var root = gff.Root;

            // PT_PCNAME - Player character name
            // Use common helper to extract PC name
            root.SetString("PT_PCNAME", GetPlayerCharacterName(state));

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
            // Use common helper to get party count
            int numMembers = GetSelectedPartyCount(state);
            root.SetUInt8("PT_NUM_MEMBERS", (byte)numMembers);

            // PT_MEMBERS - List of party members
            var membersList = new GFFList();
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
            root.SetList("PT_MEMBERS", membersList);

            // PT_NUM_PUPPETS - Number of puppets (byte)
            int numPuppets = state.Puppets != null ? state.Puppets.Count : 0;
            root.SetUInt8("PT_NUM_PUPPETS", (byte)numPuppets);

            // PT_PUPPETS - List of puppets
            var puppetsList = new GFFList();
            if (state.Puppets != null)
            {
                foreach (uint puppetId in state.Puppets)
                {
                    GFFStruct entry = puppetsList.Add();
                    entry.SetSingle("PT_PUPPET_ID", (float)puppetId);
                }
            }
            root.SetList("PT_PUPPETS", puppetsList);

            // PT_AVAIL_PUPS - Available puppets list (3 entries)
            var availPupsList = new GFFList();
            for (int i = 0; i < 3; i++)
            {
                GFFStruct entry = availPupsList.Add();
                bool available = state.AvailablePuppets != null && i < state.AvailablePuppets.Count && state.AvailablePuppets[i];
                entry.SetUInt8("PT_PUP_AVAIL", available ? (byte)1 : (byte)0);
                bool selectable = state.SelectablePuppets != null && i < state.SelectablePuppets.Count && state.SelectablePuppets[i];
                entry.SetUInt8("PT_PUP_SELECT", selectable ? (byte)1 : (byte)0);
            }
            root.SetList("PT_AVAIL_PUPS", availPupsList);

            // PT_AVAIL_NPCS - Available NPCs list (12 entries)
            var availNpcsList = new GFFList();
            List<PartyMemberState> memberList = state.AvailableMembers != null ? new List<PartyMemberState>(state.AvailableMembers.Values) : new List<PartyMemberState>();
            for (int i = 0; i < 12; i++)
            {
                GFFStruct entry = availNpcsList.Add();
                bool available = i < memberList.Count && memberList[i] != null;
                entry.SetUInt8("PT_NPC_AVAIL", available ? (byte)1 : (byte)0);
                bool selectable = available && memberList[i].IsSelectable;
                entry.SetUInt8("PT_NPC_SELECT", selectable ? (byte)1 : (byte)0);
            }
            root.SetList("PT_AVAIL_NPCS", availNpcsList);

            // PT_INFLUENCE - Influence values list (12 entries, K2 only)
            var influenceList = new GFFList();
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
            root.SetList("PT_INFLUENCE", influenceList);

            // PT_AISTATE - AI state (float)
            root.SetSingle("PT_AISTATE", (float)state.AIState);

            // PT_FOLLOWSTATE - Follow state (float)
            root.SetSingle("PT_FOLLOWSTATE", (float)state.FollowState);

            // GlxyMap - Galaxy map data
            var glxyMapStruct = new GFFStruct();
            glxyMapStruct.SetInt32("GlxyMapNumPnts", 16); // Always 16 points
            glxyMapStruct.SetInt32("GlxyMapPlntMsk", state.GalaxyMapPlanetMask);
            glxyMapStruct.SetSingle("GlxyMapSelPnt", (float)state.GalaxyMapSelectedPoint);
            root.SetStruct("GlxyMap", glxyMapStruct);

            // PT_PAZAAKCARDS - Pazaak cards list (23 entries)
            var pazaakCardsList = new GFFList();
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
            root.SetList("PT_PAZAAKCARDS", pazaakCardsList);

            // PT_PAZSIDELIST - Pazaak side list (10 entries)
            var pazaakSideList = new GFFList();
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
            root.SetList("PT_PAZSIDELIST", pazaakSideList);

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
            var fbMsgList = new GFFList();
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
            root.SetList("PT_FB_MSG_LIST", fbMsgList);

            // PT_DLG_MSG_LIST - Dialogue message list
            var dlgMsgList = new GFFList();
            if (state.DialogueMessages != null)
            {
                foreach (var msg in state.DialogueMessages)
                {
                    GFFStruct entry = dlgMsgList.Add();
                    entry.SetString("PT_DLG_MSG_SPKR", msg.Speaker ?? "");
                    entry.SetString("PT_DLG_MSG_MSG", msg.Message ?? "");
                }
            }
            root.SetList("PT_DLG_MSG_LIST", dlgMsgList);

            // PT_COM_MSG_LIST - Combat message list
            var comMsgList = new GFFList();
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
            root.SetList("PT_COM_MSG_LIST", comMsgList);

            // PT_COST_MULT_LIST - Cost multiplier list
            var costMultList = new GFFList();
            if (state.CostMultipliers != null)
            {
                foreach (var mult in state.CostMultipliers)
                {
                    GFFStruct entry = costMultList.Add();
                    entry.SetSingle("PT_COST_MULT_VALUE", mult);
                }
            }
            root.SetList("PT_COST_MULT_LIST", costMultList);

            // PT_DISABLEMAP - Disable map flag (float)
            root.SetSingle("PT_DISABLEMAP", state.DisableMap ? 1.0f : 0.0f);

            // PT_DISABLEREGEN - Disable regen flag (float)
            root.SetSingle("PT_DISABLEREGEN", state.DisableRegen ? 1.0f : 0.0f);

            // Serialize GFF to bytes
            return gff.ToBytes();
        }

        /// <summary>
        /// Helper to get member ID from ResRef.
        /// </summary>
        /// <remarks>
        /// Member IDs: -1 = Player, 0-8 = NPC slots (K1), 0-11 = NPC slots (K2)
        /// Based on nwscript.nss constants: NPC_PLAYER = -1, NPC_BASTILA = 0, etc.
        ///
        /// Implementation:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): partytable.2da maps NPC ResRefs to member IDs
        /// - Located via string reference: "PARTYTABLE" @ 0x007c1910
        /// - Original implementation: partytable.2da row index = member ID (0-11 for K2, 0-8 for K1)
        /// - Row label in partytable.2da is the NPC ResRef (e.g., "bastila", "atton")
        /// - Searches partytable.2da for matching ResRef (exact match, then partial match)
        /// - Falls back to hardcoded mapping if partytable.2da not available
        /// </remarks>
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): partytable.2da system
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Original implementation: partytable.2da maps NPC ResRefs to member IDs (row index = member ID)
            // partytable.2da structure: Row label is ResRef, row index is member ID (0-11 for K2, 0-8 for K1)
            if (_gameDataManager != null)
            {
                BioWare.NET.Resource.Formats.TwoDA.TwoDA partyTable = _gameDataManager.GetTable("partytable");
                if (partyTable != null)
                {
                    // Search partytable.2da for matching ResRef
                    // Row index in partytable.2da corresponds to member ID
                    for (int i = 0; i < partyTable.GetHeight(); i++)
                    {
                        BioWare.NET.Resource.Formats.TwoDA.TwoDARow row = partyTable.GetRow(i);
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
            // This matches original engine behavior when ResRef is not recognized
            return 0.0f;
        }

        /// <summary>
        /// Helper to get ResRef from member ID (reverse of GetMemberId).
        /// </summary>
        /// <remarks>
        /// Member IDs: -1 = Player, 0-8 = NPC slots (K1), 0-11 = NPC slots (K2)
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): partytable.2da system
        /// Located via string reference: "PARTYTABLE" @ 0x007c1910
        /// Original implementation: partytable.2da maps NPC ResRefs to member IDs (row index = member ID)
        /// partytable.2da structure: Row label is ResRef, row index is member ID (0-11 for K2, 0-8 for K1)
        /// Reverse mapping: member ID -> ResRef by reading row label at index = member ID
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 @ 0x0057dcd0 (LoadPartyTable function) reads PT_MEMBER_ID and converts to ResRef
        /// Original implementation reads PT_MEMBER_ID (float) and converts to ResRef using partytable.2da lookup
        ///
        /// CRITICAL: This method REQUIRES partytable.2da to be available via GameDataManager.
        /// K1 and K2 have different NPCs for the same member IDs (e.g., member ID 0 = "bastila" in K1, "atton" in K2).
        /// Without partytable.2da, we cannot determine the correct ResRef. This matches original engine behavior.
        /// </remarks>
        private string GetResRefFromMemberId(float memberId)
        {
            // Player character (member ID = -1)
            // Based on nwscript.nss constants: NPC_PLAYER = -1
            // Player ResRefs are typically "player", "pc", or start with "pc_"
            if (memberId < 0.0f || Math.Abs(memberId - (-1.0f)) < 0.001f)
            {
                return "player"; // Default player ResRef
            }

            int memberIdInt = (int)memberId;

            // REQUIRED: Load from partytable.2da to get correct ResRef
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): partytable.2da system
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            // Original implementation: partytable.2da row index = member ID (0-11 for K2, 0-8 for K1)
            // Row label in partytable.2da is the NPC ResRef (e.g., "bastila" for K1, "atton" for K2)
            // CRITICAL: K1 and K2 have different NPCs for the same member IDs, so partytable.2da is REQUIRED
            if (_gameDataManager != null)
            {
                BioWare.NET.Resource.Formats.TwoDA.TwoDA partyTable = _gameDataManager.GetTable("partytable");
                if (partyTable != null && memberIdInt >= 0 && memberIdInt < partyTable.GetHeight())
                {
                    BioWare.NET.Resource.Formats.TwoDA.TwoDARow row = partyTable.GetRow(memberIdInt);
                    string rowLabel = row.Label();
                    if (!string.IsNullOrEmpty(rowLabel))
                    {
                        return rowLabel;
                    }
                }
            }

            // If partytable.2da is not available, we cannot determine the correct ResRef
            // K1 and K2 have different NPCs for the same member IDs, so guessing is incorrect
            // This matches original engine behavior: returns empty string when member ID cannot be resolved
            // Original engine always has partytable.2da available, so this fallback should rarely be hit
            System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] GetResRefFromMemberId: partytable.2da not available, cannot resolve member ID {memberIdInt}");
            return "";
        }

        /// <summary>
        /// Deserializes party information.
        /// </summary>
        /// <remarks>
        /// Recreates party from save data.
        /// Restores companion states, relationships, equipment.
        /// Reestablishes party formation and leadership.
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 @ 0x0057dcd0 (LoadPartyTable function)
        /// Located via string reference: "PARTYTABLE" @ 0x007c1910
        /// Original implementation: Loads PARTYTABLE.res GFF file and deserializes all party state fields
        /// GFF structure: "PT  " signature with "V2.0" version (K2) or "V1.0" (K1)
        ///
        /// Deserialization order matches original engine:
        /// 1. PT_PCNAME - Player character name
        /// 2. PT_GOLD - Gold/credits
        /// 3. PT_ITEM_COMPONENT - Item component count
        /// 4. PT_ITEM_CHEMICAL - Item chemical count
        /// 5. PT_SWOOP1-3 - Swoop race times
        /// 6. PT_XP_POOL - Experience point pool
        /// 7. PT_PLAYEDSECONDS - Total seconds played
        /// 8. PT_CONTROLLED_NPC - Currently controlled NPC ID
        /// 9. PT_SOLOMODE - Solo mode flag
        /// 10. PT_CHEAT_USED - Cheat used flag
        /// 11. PT_NUM_MEMBERS - Number of party members
        /// 12. PT_MEMBERS - List of party members (PT_MEMBER_ID, PT_IS_LEADER)
        /// 13. PT_PUPPETS - List of puppets
        /// 14. PT_AVAIL_PUPS - Available puppets list
        /// 15. PT_AVAIL_NPCS - Available NPCs list
        /// 16. PT_INFLUENCE - Influence values (K2 only)
        /// 17. PT_AISTATE - AI state
        /// 18. PT_FOLLOWSTATE - Follow state
        /// 19. GlxyMap - Galaxy map data
        /// 20. PT_PAZAAKCARDS - Pazaak cards list
        /// 21. PT_PAZSIDELIST - Pazaak side list
        /// 22. PT_TUT_WND_SHOWN - Tutorial windows shown
        /// 23. PT_LAST_GUI_PNL - Last GUI panel
        /// 24. PT_FB_MSG_LIST - Feedback message list
        /// 25. PT_DLG_MSG_LIST - Dialogue message list
        /// 26. PT_COM_MSG_LIST - Combat message list
        /// 27. PT_COST_MULT_LIST - Cost multiplier list
        /// 28. PT_DISABLEMAP - Disable map flag
        /// 29. PT_DISABLEREGEN - Disable regen flag
        /// </remarks>
        public override void DeserializeParty(byte[] partyData, IPartyState partyState)
        {
            if (partyData == null || partyData.Length == 0)
            {
                // Empty or null party data - nothing to deserialize
                return;
            }

            if (partyState == null)
            {
                throw new ArgumentNullException(nameof(partyState), "PartyState cannot be null for party deserialization");
            }

            // Parse GFF from byte array
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 @ 0x0057dcd0 loads GFF with "PT  " signature
            // Located via string reference: "PARTYTABLE" @ 0x007c1910
            GFF gff;
            try
            {
                gff = GFF.FromBytes(partyData);
            }
            catch (Exception ex)
            {
                // Invalid GFF structure - log error but don't throw to allow save loading to continue
                System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to parse PARTYTABLE GFF: {ex.Message}");
                return;
            }

            if (gff == null || gff.Root == null)
            {
                // Invalid or empty GFF structure
                return;
            }

            // Check PARTYTABLE signature
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): PARTYTABLE GFF must have PT content type
            if (gff.Content != GFFContent.PT)
            {
                System.Diagnostics.Debug.WriteLine("[OdysseySaveSerializer] PARTYTABLE GFF has incorrect content type");
                return;
            }

            var root = gff.Root;

            // Try to cast IPartyState to PartyState to populate rich data
            // If cast fails, we'll only populate the basic IPartyState interface
            PartyState state = partyState as PartyState;
            bool hasRichState = state != null;

            // Initialize PartyState if we have it
            if (hasRichState)
            {
                if (state.AvailableMembers == null)
                {
                    state.AvailableMembers = new Dictionary<string, PartyMemberState>();
                }
                if (state.SelectedParty == null)
                {
                    state.SelectedParty = new List<string>();
                }
                if (state.Influence == null)
                {
                    state.Influence = new List<int>();
                }
                if (state.Puppets == null)
                {
                    state.Puppets = new List<uint>();
                }
                if (state.AvailablePuppets == null)
                {
                    state.AvailablePuppets = new List<bool>();
                }
                if (state.SelectablePuppets == null)
                {
                    state.SelectablePuppets = new List<bool>();
                }
                if (state.PazaakCards == null)
                {
                    state.PazaakCards = new List<int>();
                }
                if (state.PazaakSideList == null)
                {
                    state.PazaakSideList = new List<int>();
                }
                if (state.TutorialWindowsShown == null)
                {
                    state.TutorialWindowsShown = new List<bool>();
                }
                if (state.FeedbackMessages == null)
                {
                    state.FeedbackMessages = new List<FeedbackMessage>();
                }
                if (state.DialogueMessages == null)
                {
                    state.DialogueMessages = new List<DialogueMessage>();
                }
                if (state.CombatMessages == null)
                {
                    state.CombatMessages = new List<CombatMessage>();
                }
                if (state.CostMultipliers == null)
                {
                    state.CostMultipliers = new List<float>();
                }
            }

            // 1. PT_PCNAME - Player character name
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 66-68 reads PT_PCNAME
            string pcName = GetStringField(root, "PT_PCNAME", "");
            if (hasRichState && !string.IsNullOrEmpty(pcName))
            {
                // Store PC name in PartyState (if it has a property for it)
                // Note: PartyState doesn't have a direct PCName property, but we can store it in PlayerCharacter
                if (state.PlayerCharacter == null)
                {
                    state.PlayerCharacter = new CreatureState();
                }
                state.PlayerCharacter.Tag = pcName;
            }

            // 2. PT_GOLD - Gold/credits
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 71 reads PT_GOLD
            int gold = GetIntField(root, "PT_GOLD", 0);
            if (hasRichState)
            {
                state.Gold = gold;
            }

            // 3. PT_ITEM_COMPONENT - Item component count
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 73 reads PT_ITEM_COMPONENT
            int itemComponent = GetIntField(root, "PT_ITEM_COMPONENT", 0);
            if (hasRichState)
            {
                state.ItemComponent = itemComponent;
            }

            // 4. PT_ITEM_CHEMICAL - Item chemical count
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 75 reads PT_ITEM_CHEMICAL
            int itemChemical = GetIntField(root, "PT_ITEM_CHEMICAL", 0);
            if (hasRichState)
            {
                state.ItemChemical = itemChemical;
            }

            // 5. PT_SWOOP1-3 - Swoop race times
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 77-82 read PT_SWOOP1-3
            int swoop1 = GetIntField(root, "PT_SWOOP1", 0);
            int swoop2 = GetIntField(root, "PT_SWOOP2", 0);
            int swoop3 = GetIntField(root, "PT_SWOOP3", 0);
            if (hasRichState)
            {
                state.Swoop1 = swoop1;
                state.Swoop2 = swoop2;
                state.Swoop3 = swoop3;
            }

            // 6. PT_XP_POOL - Experience point pool (float)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 83 reads PT_XP_POOL as float
            float xpPool = GetSingleField(root, "PT_XP_POOL", 0.0f);
            if (hasRichState)
            {
                state.ExperiencePoints = (int)xpPool;
            }

            // 7. PT_PLAYEDSECONDS - Total seconds played
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 86-91 read PT_PLAYEDSECONDS (with fallback to PT_PLAYEDMINUTES)
            int timePlayedSeconds = GetIntField(root, "PT_PLAYEDSECONDS", -1);
            if (timePlayedSeconds < 0)
            {
                // Fallback to PT_PLAYEDMINUTES if PT_PLAYEDSECONDS not found
                int timePlayedMinutes = GetIntField(root, "PT_PLAYEDMINUTES", 0);
                timePlayedSeconds = timePlayedMinutes * 60;
            }
            if (hasRichState)
            {
                state.PlayTime = TimeSpan.FromSeconds(timePlayedSeconds);
            }

            // 8. PT_CONTROLLED_NPC - Currently controlled NPC ID (float, -1 if none)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 92 reads PT_CONTROLLED_NPC
            float controlledNpc = GetSingleField(root, "PT_CONTROLLED_NPC", -1.0f);
            if (hasRichState)
            {
                state.ControlledNPC = (int)controlledNpc;
            }

            // 9. PT_SOLOMODE - Solo mode flag (byte)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 94-98 read PT_SOLOMODE
            byte soloMode = GetUInt8Field(root, "PT_SOLOMODE", 0);
            bool soloModeBool = soloMode != 0;
            if (hasRichState)
            {
                state.SoloMode = soloModeBool;
            }

            // 10. PT_CHEAT_USED - Cheat used flag (byte)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 99 reads PT_CHEAT_USED
            byte cheatUsed = GetUInt8Field(root, "PT_CHEAT_USED", 0);
            bool cheatUsedBool = cheatUsed != 0;
            if (hasRichState)
            {
                state.CheatUsed = cheatUsedBool;
            }

            // 11. PT_NUM_MEMBERS - Number of party members (byte)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 101-107 read PT_NUM_MEMBERS
            // Original implementation: Uses list length if PT_NUM_MEMBERS is less than actual list size
            byte numMembers = GetUInt8Field(root, "PT_NUM_MEMBERS", 0);

            // 12. PT_MEMBERS - List of party members
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 108-122 read PT_MEMBERS list
            // Each member has PT_MEMBER_ID (float) and PT_IS_LEADER (byte)
            var membersList = root.GetList("PT_MEMBERS");
            if (membersList != null)
            {
                // Use actual list size if it's larger than PT_NUM_MEMBERS
                int actualListSize = membersList.Count;
                if (actualListSize > numMembers)
                {
                    numMembers = (byte)actualListSize;
                }

                string leaderResRef = null;
                List<string> selectedPartyResRefs = new List<string>();

                // Process each member in the list
                for (int i = 0; i < numMembers && i < membersList.Count; i++)
                {
                    GFFStruct memberStruct = membersList[i];
                    if (memberStruct == null)
                    {
                        continue;
                    }

                    // PT_MEMBER_ID - Member ID (float, -1 = PC, 0-11 = NPC slots)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 113 reads PT_MEMBER_ID
                    float memberId = GetSingleField(memberStruct, "PT_MEMBER_ID", -1.0f);

                    // Convert member ID to ResRef
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): partytable.2da system for reverse mapping
                    string memberResRef = GetResRefFromMemberId(memberId);

                    if (!string.IsNullOrEmpty(memberResRef))
                    {
                        selectedPartyResRefs.Add(memberResRef);

                        // PT_IS_LEADER - Whether this member is the leader (byte)
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 115-118 check PT_IS_LEADER
                        byte isLeader = GetUInt8Field(memberStruct, "PT_IS_LEADER", 0);
                        if (isLeader != 0)
                        {
                            leaderResRef = memberResRef;
                        }

                        // If we have rich state, populate AvailableMembers
                        if (hasRichState)
                        {
                            if (!state.AvailableMembers.ContainsKey(memberResRef))
                            {
                                var memberState = new PartyMemberState
                                {
                                    TemplateResRef = memberResRef,
                                    IsAvailable = true,
                                    IsSelectable = true
                                };
                                state.AvailableMembers[memberResRef] = memberState;
                            }
                        }
                    }
                }

                // Set selected party and leader
                if (hasRichState)
                {
                    state.SelectedParty = selectedPartyResRefs;
                    state.LeaderResRef = leaderResRef ?? (selectedPartyResRefs.Count > 0 ? selectedPartyResRefs[0] : null);
                }
            }

            // 13. PT_PUPPETS - List of puppets
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 123-146 read PT_PUPPETS
            byte numPuppets = GetUInt8Field(root, "PT_NUM_PUPPETS", 0);
            var puppetsList = root.GetList("PT_PUPPETS");
            if (puppetsList != null)
            {
                int actualPuppetsSize = puppetsList.Count;
                if (actualPuppetsSize > numPuppets)
                {
                    numPuppets = (byte)actualPuppetsSize;
                }
                if (numPuppets < 0)
                {
                    numPuppets = 0;
                }

                if (hasRichState)
                {
                    state.Puppets.Clear();
                    for (int i = 0; i < numPuppets && i < puppetsList.Count; i++)
                    {
                        GFFStruct puppetStruct = puppetsList[i];
                        if (puppetStruct != null)
                        {
                            float puppetId = GetSingleField(puppetStruct, "PT_PUPPET_ID", -1.0f);
                            if (puppetId >= 0)
                            {
                                state.Puppets.Add((uint)puppetId);
                            }
                        }
                    }
                }
            }

            // 14. PT_AVAIL_PUPS - Available puppets list (3 entries)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 147-167 read PT_AVAIL_PUPS
            var availPupsList = root.GetList("PT_AVAIL_PUPS");
            if (availPupsList != null)
            {
                int availPupsSize = availPupsList.Count;
                if (availPupsSize > 3)
                {
                    availPupsSize = 3;
                }

                if (hasRichState)
                {
                    state.AvailablePuppets.Clear();
                    state.SelectablePuppets.Clear();
                    for (int i = 0; i < availPupsSize && i < availPupsList.Count; i++)
                    {
                        GFFStruct pupStruct = availPupsList[i];
                        if (pupStruct != null)
                        {
                            byte pupAvail = GetUInt8Field(pupStruct, "PT_PUP_AVAIL", 0);
                            byte pupSelect = GetUInt8Field(pupStruct, "PT_PUP_SELECT", 0);
                            state.AvailablePuppets.Add(pupAvail != 0);
                            state.SelectablePuppets.Add(pupSelect != 0);
                        }
                        else
                        {
                            state.AvailablePuppets.Add(false);
                            state.SelectablePuppets.Add(false);
                        }
                    }
                }
            }

            // 15. PT_AVAIL_NPCS - Available NPCs list (12 entries)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 168-188 read PT_AVAIL_NPCS
            var availNpcsList = root.GetList("PT_AVAIL_NPCS");
            if (availNpcsList != null)
            {
                int availNpcsSize = availNpcsList.Count;
                if (availNpcsSize > 12)
                {
                    availNpcsSize = 12;
                }

                if (hasRichState)
                {
                    for (int i = 0; i < availNpcsSize && i < availNpcsList.Count; i++)
                    {
                        GFFStruct npcStruct = availNpcsList[i];
                        if (npcStruct != null)
                        {
                            byte npcAvail = GetUInt8Field(npcStruct, "PT_NPC_AVAIL", 0);
                            byte npcSelect = GetUInt8Field(npcStruct, "PT_NPC_SELECT", 0);

                            // Convert member ID to ResRef (member ID = index in list)
                            string npcResRef = GetResRefFromMemberId((float)i);
                            if (!string.IsNullOrEmpty(npcResRef))
                            {
                                if (!state.AvailableMembers.ContainsKey(npcResRef))
                                {
                                    var memberState = new PartyMemberState
                                    {
                                        TemplateResRef = npcResRef,
                                        IsAvailable = npcAvail != 0,
                                        IsSelectable = npcSelect != 0
                                    };
                                    state.AvailableMembers[npcResRef] = memberState;
                                }
                                else
                                {
                                    // Update existing member state
                                    var memberState = state.AvailableMembers[npcResRef];
                                    memberState.IsAvailable = npcAvail != 0;
                                    memberState.IsSelectable = npcSelect != 0;
                                }
                            }
                        }
                    }
                }
            }

            // 16. PT_INFLUENCE - Influence values list (12 entries, K2 only)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 189-207 read PT_INFLUENCE
            var influenceList = root.GetList("PT_INFLUENCE");
            if (influenceList != null)
            {
                int influenceSize = influenceList.Count;
                if (influenceSize > 12)
                {
                    influenceSize = 12;
                }

                if (hasRichState)
                {
                    state.Influence.Clear();
                    for (int i = 0; i < influenceSize && i < influenceList.Count; i++)
                    {
                        GFFStruct influenceStruct = influenceList[i];
                        if (influenceStruct != null)
                        {
                            float influence = GetSingleField(influenceStruct, "PT_NPC_INFLUENCE", 0.0f);
                            state.Influence.Add((int)influence);
                        }
                        else
                        {
                            state.Influence.Add(0);
                        }
                    }
                }
            }

            // 17. PT_AISTATE - AI state (float)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 208 reads PT_AISTATE
            float aiState = GetSingleField(root, "PT_AISTATE", 0.0f);
            if (hasRichState)
            {
                state.AIState = (int)aiState;
            }

            // 18. PT_FOLLOWSTATE - Follow state (float)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 210 reads PT_FOLLOWSTATE
            float followState = GetSingleField(root, "PT_FOLLOWSTATE", 0.0f);
            if (hasRichState)
            {
                state.FollowState = (int)followState;
            }

            // 19. GlxyMap - Galaxy map data
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 212-232 read GlxyMap struct
            GFFStruct glxyMapStruct = root.GetStruct("GlxyMap");
            if (glxyMapStruct != null)
            {
                int glxyMapNumPnts = GetIntField(glxyMapStruct, "GlxyMapNumPnts", 0);
                int glxyMapPlntMsk = GetIntField(glxyMapStruct, "GlxyMapPlntMsk", 0);
                float glxyMapSelPnt = GetSingleField(glxyMapStruct, "GlxyMapSelPnt", -1.0f);

                if (hasRichState)
                {
                    state.GalaxyMapPlanetMask = glxyMapPlntMsk;
                    state.GalaxyMapSelectedPoint = (int)glxyMapSelPnt;
                }
            }

            // 20. PT_PAZAAKCARDS - Pazaak cards list (23 entries)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 233-242 read PT_PAZAAKCARDS
            var pazaakCardsList = root.GetList("PT_PAZAAKCARDS");
            if (pazaakCardsList != null)
            {
                if (hasRichState)
                {
                    state.PazaakCards.Clear();
                    for (int i = 0; i < 23 && i < pazaakCardsList.Count; i++)
                    {
                        GFFStruct cardStruct = pazaakCardsList[i];
                        if (cardStruct != null)
                        {
                            float cardCount = GetSingleField(cardStruct, "PT_PAZAAKCOUNT", 0.0f);
                            state.PazaakCards.Add((int)cardCount);
                        }
                        else
                        {
                            state.PazaakCards.Add(0);
                        }
                    }
                }
            }

            // 21. PT_PAZSIDELIST - Pazaak side list (10 entries)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 243-252 read PT_PAZSIDELIST
            var pazaakSideList = root.GetList("PT_PAZSIDELIST");
            if (pazaakSideList != null)
            {
                if (hasRichState)
                {
                    state.PazaakSideList.Clear();
                    for (int i = 0; i < 10 && i < pazaakSideList.Count; i++)
                    {
                        GFFStruct sideStruct = pazaakSideList[i];
                        if (sideStruct != null)
                        {
                            float sideCard = GetSingleField(sideStruct, "PT_PAZSIDECARD", 0.0f);
                            state.PazaakSideList.Add((int)sideCard);
                        }
                        else
                        {
                            state.PazaakSideList.Add(0);
                        }
                    }
                }
            }

            // 22. PT_TUT_WND_SHOWN - Tutorial windows shown (array of 33 bytes)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 260 reads PT_TUT_WND_SHOWN
            byte[] tutWndShown = root.GetBinary("PT_TUT_WND_SHOWN");
            if (tutWndShown != null && tutWndShown.Length > 0)
            {
                if (hasRichState)
                {
                    state.TutorialWindowsShown.Clear();
                    for (int i = 0; i < 33 && i < tutWndShown.Length; i++)
                    {
                        state.TutorialWindowsShown.Add(tutWndShown[i] != 0);
                    }
                }
            }

            // 23. PT_LAST_GUI_PNL - Last GUI panel (float)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 263-265 read PT_LAST_GUI_PNL
            float lastGuiPanel = GetSingleField(root, "PT_LAST_GUI_PNL", 0.0f);
            if (hasRichState)
            {
                state.LastGUIPanel = (int)lastGuiPanel;
            }

            // 24. PT_FB_MSG_LIST - Feedback message list
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 272-297 read PT_FB_MSG_LIST
            var fbMsgList = root.GetList("PT_FB_MSG_LIST");
            if (fbMsgList != null)
            {
                if (hasRichState)
                {
                    state.FeedbackMessages.Clear();
                    foreach (GFFStruct msgStruct in fbMsgList)
                    {
                        if (msgStruct != null)
                        {
                            string msg = GetStringField(msgStruct, "PT_FB_MSG_MSG", "");
                            int msgType = GetIntField(msgStruct, "PT_FB_MSG_TYPE", 0);
                            byte msgColor = GetUInt8Field(msgStruct, "PT_FB_MSG_COLOR", 0);
                            if (!string.IsNullOrEmpty(msg))
                            {
                                state.FeedbackMessages.Add(new FeedbackMessage
                                {
                                    Message = msg,
                                    Type = msgType,
                                    Color = msgColor
                                });
                            }
                        }
                    }
                }
            }

            // 25. PT_DLG_MSG_LIST - Dialogue message list
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 300-331 read PT_DLG_MSG_LIST
            var dlgMsgList = root.GetList("PT_DLG_MSG_LIST");
            if (dlgMsgList != null)
            {
                if (hasRichState)
                {
                    state.DialogueMessages.Clear();
                    foreach (GFFStruct msgStruct in dlgMsgList)
                    {
                        if (msgStruct != null)
                        {
                            string speaker = GetStringField(msgStruct, "PT_DLG_MSG_SPKR", "");
                            string msg = GetStringField(msgStruct, "PT_DLG_MSG_MSG", "");
                            if (!string.IsNullOrEmpty(msg))
                            {
                                state.DialogueMessages.Add(new DialogueMessage
                                {
                                    Speaker = speaker,
                                    Message = msg
                                });
                            }
                        }
                    }
                }
            }

            // 26. PT_COM_MSG_LIST - Combat message list
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 332-357 read PT_COM_MSG_LIST
            var comMsgList = root.GetList("PT_COM_MSG_LIST");
            if (comMsgList != null)
            {
                if (hasRichState)
                {
                    state.CombatMessages.Clear();
                    foreach (GFFStruct msgStruct in comMsgList)
                    {
                        if (msgStruct != null)
                        {
                            string msg = GetStringField(msgStruct, "PT_COM_MSG_MSG", "");
                            int msgType = GetIntField(msgStruct, "PT_COM_MSG_TYPE", 0);
                            byte msgColor = GetUInt8Field(msgStruct, "PT_COM_MSG_COOR", 0);
                            if (!string.IsNullOrEmpty(msg))
                            {
                                state.CombatMessages.Add(new CombatMessage
                                {
                                    Message = msg,
                                    Type = msgType,
                                    Color = msgColor
                                });
                            }
                        }
                    }
                }
            }

            // 27. PT_COST_MULT_LIST - Cost multiplier list
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 lines 358-368 read PT_COST_MULT_LIST
            var costMultList = root.GetList("PT_COST_MULT_LIST");
            if (costMultList != null)
            {
                if (hasRichState)
                {
                    state.CostMultipliers.Clear();
                    foreach (GFFStruct multStruct in costMultList)
                    {
                        if (multStruct != null)
                        {
                            float multValue = GetSingleField(multStruct, "PT_COST_MULT_VALUE", 1.0f);
                            state.CostMultipliers.Add(multValue);
                        }
                    }
                }
            }

            // 28. PT_DISABLEMAP - Disable map flag (float)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 369 reads PT_DISABLEMAP
            float disableMap = GetSingleField(root, "PT_DISABLEMAP", 0.0f);
            if (hasRichState)
            {
                state.DisableMap = disableMap != 0.0f;
            }

            // 29. PT_DISABLEREGEN - Disable regen flag (float)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0057dcd0 line 371 reads PT_DISABLEREGEN
            float disableRegen = GetSingleField(root, "PT_DISABLEREGEN", 0.0f);
            if (hasRichState)
            {
                state.DisableRegen = disableRegen != 0.0f;
            }
        }

        /// <summary>
        /// Serializes area state.
        /// </summary>
        /// <remarks>
        /// Odyssey area serialization saves dynamic changes.
        /// Includes placed objects, modified containers, area effects.
        /// Saves transition states and dynamic object modifications.
        ///
        /// Area state includes:
        /// - Placed creatures and objects
        /// - Modified container contents
        /// - Active area effects
        /// - Door and transition states
        /// - Dynamic lighting changes
        ///
        /// Based on reverse engineering of:
        /// - swkotor2.exe: 0x005226d0 @ 0x005226d0 saves entity states to GFF format
        /// - swkotor2.exe: Area state stored in [module]_s.rim ERF archive as ARE resources
        /// - Located via string references: "Creature List" @ 0x007c0c80, "DoorList" @ 0x007c0c90
        /// - Original implementation: Saves entity positions, door/placeable states, HP, local variables, etc.
        /// - Each area state GFF contains:
        ///   - AreaResRef: Area resource reference
        ///   - CreatureList: List of creature entity states
        ///   - DoorList: List of door entity states
        ///   - PlaceableList: List of placeable entity states
        ///   - TriggerList: List of trigger entity states
        ///   - StoreList: List of store entity states
        ///   - SoundList: List of sound entity states
        ///   - WaypointList: List of waypoint entity states
        ///   - EncounterList: List of encounter entity states
        ///   - CameraList: List of camera entity states
        ///   - DestroyedList: List of destroyed entity ObjectIds
        ///   - SpawnedList: List of dynamically spawned entities
        ///   - LocalVariables: Area-level local variables
        /// </remarks>
        public override byte[] SerializeArea(IArea area)
        {
            if (area == null)
            {
                // Return empty GFF for null area
                var emptyGff = new GFF(GFFContent.GFF);
                return emptyGff.ToBytes();
            }

            // Extract AreaState from IArea
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 extracts entity states from area
            AreaState areaState = ExtractAreaStateFromArea(area);

            // Serialize AreaState to GFF format
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area state GFF creation matches SaveGameManager.CreateAreaStateGFF pattern
            GFF areaGff = SerializeAreaStateToGFF(areaState);

            // Convert GFF to byte array
            return areaGff.ToBytes();
        }

        /// <summary>
        /// Extracts AreaState from IArea by collecting all entity states.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 extracts entity states from area
        /// Collects all entities of each type and extracts their state information.
        /// </remarks>
        private AreaState ExtractAreaStateFromArea(IArea area)
        {
            var areaState = new AreaState();
            areaState.AreaResRef = area.ResRef ?? "";

            // Extract entity states for each entity type
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity states are extracted by iterating through area's entity collections
            foreach (IEntity creature in area.Creatures)
            {
                if (creature != null && creature.IsValid)
                {
                    EntityState entityState = ExtractEntityState(creature);
                    if (entityState != null)
                    {
                        areaState.CreatureStates.Add(entityState);
                    }
                }
            }

            foreach (IEntity door in area.Doors)
            {
                if (door != null && door.IsValid)
                {
                    EntityState entityState = ExtractEntityState(door);
                    if (entityState != null)
                    {
                        areaState.DoorStates.Add(entityState);
                    }
                }
            }

            foreach (IEntity placeable in area.Placeables)
            {
                if (placeable != null && placeable.IsValid)
                {
                    EntityState entityState = ExtractEntityState(placeable);
                    if (entityState != null)
                    {
                        areaState.PlaceableStates.Add(entityState);
                    }
                }
            }

            foreach (IEntity trigger in area.Triggers)
            {
                if (trigger != null && trigger.IsValid)
                {
                    EntityState entityState = ExtractEntityState(trigger);
                    if (entityState != null)
                    {
                        areaState.TriggerStates.Add(entityState);
                    }
                }
            }

            // Note: Stores and Cameras are not directly accessible via IArea interface
            // They are extracted via world entity iteration (similar to encounters)
            // Store extraction is done below after we get the world reference
            // Camera extraction is handled separately as cameras may not be runtime entities

            foreach (IEntity sound in area.Sounds)
            {
                if (sound != null && sound.IsValid)
                {
                    EntityState entityState = ExtractEntityState(sound);
                    if (entityState != null)
                    {
                        areaState.SoundStates.Add(entityState);
                    }
                }
            }

            foreach (IEntity waypoint in area.Waypoints)
            {
                if (waypoint != null && waypoint.IsValid)
                {
                    EntityState entityState = ExtractEntityState(waypoint);
                    if (entityState != null)
                    {
                        areaState.WaypointStates.Add(entityState);
                    }
                }
            }

            // Extract encounters (if area supports them)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Encounter entities are stored in area's encounter collection
            // Encounter entities have EncounterComponent and are in the world but not directly exposed via IArea interface
            // We find them by iterating through all entities in the world and filtering by AreaId and EncounterComponent
            IWorld world = null;

            // Get world reference from any entity in the area (creatures, doors, placeables, etc.)
            // This allows us to access GetAllEntities() to find encounter entities
            foreach (IEntity entity in area.Creatures)
            {
                if (entity != null && entity.World != null)
                {
                    world = entity.World;
                    break;
                }
            }

            if (world == null)
            {
                foreach (IEntity entity in area.Doors)
                {
                    if (entity != null && entity.World != null)
                    {
                        world = entity.World;
                        break;
                    }
                }
            }

            if (world == null)
            {
                foreach (IEntity entity in area.Placeables)
                {
                    if (entity != null && entity.World != null)
                    {
                        world = entity.World;
                        break;
                    }
                }
            }

            // Get area ID for filtering entities
            uint areaId = 0;
            if (world != null)
            {
                areaId = world.GetAreaId(area);
            }

            // Extract store entities from world by filtering for entities with StoreComponent in this area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 extracts store entity states from area
            // Original implementation: Stores are stored in area's store collection and extracted during save
            // Stores have StoreComponent and ObjectType.Store, similar to how encounters have EncounterComponent
            if (world != null && areaId != 0)
            {
                foreach (IEntity entity in world.GetAllEntities())
                {
                    if (entity == null || !entity.IsValid)
                    {
                        continue;
                    }

                    // Filter entities that belong to this area and have StoreComponent
                    if (entity.AreaId == areaId)
                    {
                        StoreComponent storeComp = entity.GetComponent<StoreComponent>();
                        if (storeComp != null)
                        {
                            // Verify entity has correct ObjectType for stores
                            if (entity.ObjectType == ObjectType.Store)
                            {
                                // Extract entity state for this store
                                EntityState entityState = ExtractEntityState(entity);
                                if (entityState != null)
                                {
                                    areaState.StoreStates.Add(entityState);
                                }
                            }
                        }
                    }
                }
            }

            // Extract encounter entities from world by filtering for entities with EncounterComponent in this area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 extracts encounter entity states from area
            // Original implementation: Encounters are stored in area's encounter collection and extracted during save
            if (world != null && areaId != 0)
            {
                foreach (IEntity entity in world.GetAllEntities())
                {
                    if (entity == null || !entity.IsValid)
                    {
                        continue;
                    }

                    // Filter entities that belong to this area and have EncounterComponent
                    if (entity.AreaId == areaId)
                    {
                        EncounterComponent encounterComp = entity.GetComponent<EncounterComponent>();
                        if (encounterComp != null)
                        {
                            // Extract entity state for this encounter
                            EntityState entityState = ExtractEntityState(entity);
                            if (entityState != null)
                            {
                                areaState.EncounterStates.Add(entityState);
                            }
                        }
                    }
                }
            }

            // Extract cameras (KOTOR-specific, if area supports them)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Camera entities are stored in area's camera collection
            // Original implementation: Cameras are stored in GIT CameraList but are NOT runtime entities
            // Cameras in GIT have position and FOV but don't have standard ObjectType or ObjectId
            // Since cameras are not runtime entities, they're stored in RuntimeArea when loading from GIT
            // Camera states are extracted from RuntimeArea.GetCameraStates() which was populated during area loading
            RuntimeArea runtimeArea = area as RuntimeArea;
            IReadOnlyList<EntityState> cameraStates = runtimeArea?.GetCameraStates();
            if (cameraStates != null)
            {
                foreach (EntityState cameraState in cameraStates)
                {
                    if (cameraState != null)
                    {
                        areaState.CameraStates.Add(cameraState);
                    }
                }
            }

            // Note: Destroyed entities and spawned entities are typically tracked separately
            // by the save system and would be added to areaState.DestroyedEntityIds and
            // areaState.SpawnedEntities by the calling code if needed

            // Extract area-level local variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area local variables are extracted from area's variable storage
            // Original implementation: 0x005226d0 @ 0x005226d0 extracts area variables when saving
            if (area is OdysseyArea odysseyArea)
            {
                LocalVariableSet areaVars = odysseyArea.GetLocalVariables();
                if (areaVars != null && !areaVars.IsEmpty)
                {
                    // Copy all variable types from area to areaState
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): All variable types are saved to area state
                    if (areaVars.Ints != null && areaVars.Ints.Count > 0)
                    {
                        foreach (var kvp in areaVars.Ints)
                        {
                            areaState.LocalVariables.Ints[kvp.Key] = kvp.Value;
                        }
                    }

                    if (areaVars.Floats != null && areaVars.Floats.Count > 0)
                    {
                        foreach (var kvp in areaVars.Floats)
                        {
                            areaState.LocalVariables.Floats[kvp.Key] = kvp.Value;
                        }
                    }

                    if (areaVars.Strings != null && areaVars.Strings.Count > 0)
                    {
                        foreach (var kvp in areaVars.Strings)
                        {
                            areaState.LocalVariables.Strings[kvp.Key] = kvp.Value ?? "";
                        }
                    }

                    if (areaVars.Objects != null && areaVars.Objects.Count > 0)
                    {
                        foreach (var kvp in areaVars.Objects)
                        {
                            areaState.LocalVariables.Objects[kvp.Key] = kvp.Value;
                        }
                    }

                    if (areaVars.Locations != null && areaVars.Locations.Count > 0)
                    {
                        foreach (var kvp in areaVars.Locations)
                        {
                            if (kvp.Value != null)
                            {
                                areaState.LocalVariables.Locations[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                }
            }

            return areaState;
        }

        /// <summary>
        /// Extracts EntityState from IEntity by collecting all component data.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 extracts entity state from entity
        /// Collects position, HP, door/placeable states, local variables, effects, etc.
        /// </remarks>
        private EntityState ExtractEntityState(IEntity entity)
        {
            if (entity == null || !entity.IsValid)
            {
                return null;
            }

            var entityState = new EntityState
            {
                ObjectId = entity.ObjectId,
                Tag = entity.Tag ?? "",
                ObjectType = entity.ObjectType,
                TemplateResRef = entity.GetData<string>("TemplateResRef")
            };

            // Extract transform component (position and facing)
            var transformComponent = entity.GetComponent<ITransformComponent>();
            if (transformComponent != null)
            {
                entityState.Position = transformComponent.Position;
                entityState.Facing = transformComponent.Facing;
            }

            // Extract stats component (HP, FP, etc.)
            var statsComponent = entity.GetComponent<IStatsComponent>();
            if (statsComponent != null)
            {
                entityState.CurrentHP = statsComponent.CurrentHP;
                entityState.MaxHP = statsComponent.MaxHP;
            }

            // Extract door component state
            var doorComponent = entity.GetComponent<IDoorComponent>();
            if (doorComponent != null)
            {
                entityState.IsOpen = doorComponent.IsOpen;
                entityState.IsLocked = doorComponent.IsLocked;
            }

            // Extract placeable component state
            var placeableComponent = entity.GetComponent<IPlaceableComponent>();
            if (placeableComponent != null)
            {
                entityState.IsOpen = placeableComponent.IsOpen;
                entityState.IsLocked = placeableComponent.IsLocked;

                // Check for IsDestroyed property using reflection (placeables can be destroyed)
                var isDestroyedProperty = placeableComponent.GetType().GetProperty("IsDestroyed");
                if (isDestroyedProperty != null && isDestroyedProperty.CanRead)
                {
                    try
                    {
                        object isDestroyedValue = isDestroyedProperty.GetValue(placeableComponent);
                        if (isDestroyedValue is bool v)
                        {
                            entityState.IsDestroyed = v;
                        }
                    }
                    catch
                    {
                        // Ignore errors when reading IsDestroyed property
                    }
                }
            }

            // Extract script hooks component (local variables)
            var scriptHooksComponent = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooksComponent != null)
            {
                // Extract local variables using reflection (local variables are stored in private fields)
                // Based on BaseScriptHooksComponent implementation which stores _localInts, _localFloats, _localStrings
                Type componentType = scriptHooksComponent.GetType();
                FieldInfo localIntsField = componentType.GetField("_localInts", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localFloatsField = componentType.GetField("_localFloats", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localStringsField = componentType.GetField("_localStrings", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localObjectsField = componentType.GetField("_localObjects", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo localLocationsField = componentType.GetField("_localLocations", BindingFlags.NonPublic | BindingFlags.Instance);

                if (localIntsField != null)
                {
                    var localInts = localIntsField.GetValue(scriptHooksComponent) as Dictionary<string, int>;
                    if (localInts != null)
                    {
                        foreach (var kvp in localInts)
                        {
                            entityState.LocalVariables.Ints[kvp.Key] = kvp.Value;
                        }
                    }
                }

                if (localFloatsField != null)
                {
                    var localFloats = localFloatsField.GetValue(scriptHooksComponent) as Dictionary<string, float>;
                    if (localFloats != null)
                    {
                        foreach (var kvp in localFloats)
                        {
                            entityState.LocalVariables.Floats[kvp.Key] = kvp.Value;
                        }
                    }
                }

                if (localStringsField != null)
                {
                    var localStrings = localStringsField.GetValue(scriptHooksComponent) as Dictionary<string, string>;
                    if (localStrings != null)
                    {
                        foreach (var kvp in localStrings)
                        {
                            entityState.LocalVariables.Strings[kvp.Key] = kvp.Value ?? "";
                        }
                    }
                }

                if (localObjectsField != null)
                {
                    var localObjects = localObjectsField.GetValue(scriptHooksComponent) as Dictionary<string, uint>;
                    if (localObjects != null)
                    {
                        foreach (var kvp in localObjects)
                        {
                            entityState.LocalVariables.Objects[kvp.Key] = kvp.Value;
                        }
                    }
                }

                if (localLocationsField != null)
                {
                    var localLocations = localLocationsField.GetValue(scriptHooksComponent) as Dictionary<string, SavedLocation>;
                    if (localLocations != null)
                    {
                        foreach (var kvp in localLocations)
                        {
                            entityState.LocalVariables.Locations[kvp.Key] = kvp.Value;
                        }
                    }
                }
            }

            // Extract effects component (active effects/buffs/debuffs)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 saves active effects on entities
            // Extract active effects from entity using world's EffectSystem
            if (entity.World != null && entity.World.EffectSystem != null)
            {
                var activeEffects = entity.World.EffectSystem.GetEffects(entity);
                foreach (var activeEffect in activeEffects)
                {
                    if (activeEffect != null && activeEffect.Effect != null)
                    {
                        SavedEffect savedEffect = ConvertActiveEffectToSavedEffect(activeEffect);
                        if (savedEffect != null)
                        {
                            entityState.ActiveEffects.Add(savedEffect);
                        }
                    }
                }
            }

            // Extract template ResRef if available
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TemplateResRef is stored in entity's template data
            // TemplateResRef is stored using entity.SetData("TemplateResRef", ...) when entities are created
            // Based on EclipseArea.cs: TemplateResRef is accessed via entity.GetData<string>("TemplateResRef")
            // Located via string references: "TemplateResRef" @ 0x007bd00c in swkotor2.exe
            try
            {
                // Try to get TemplateResRef from entity data
                // IEntity interface provides GetData<T> method for accessing stored data
                string templateResRef = entity.GetData<string>("TemplateResRef");
                if (!string.IsNullOrEmpty(templateResRef))
                {
                    entityState.TemplateResRef = templateResRef;
                }
            }
            catch
            {
                // If GetData fails or TemplateResRef is not set, leave it empty
                // This is expected for dynamically spawned entities or entities without templates
                entityState.TemplateResRef = null;
            }

            return entityState;
        }

        /// <summary>
        /// Serializes AreaState to GFF format.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area state GFF creation matches SaveGameManager.CreateAreaStateGFF pattern
        /// Creates GFF with all entity lists, destroyed entities, spawned entities, and local variables.
        /// </remarks>
        private GFF SerializeAreaStateToGFF(AreaState areaState)
        {
            var gff = new GFF(GFFContent.GFF);
            var root = gff.Root;

            if (areaState == null)
            {
                return gff;
            }

            // Area ResRef
            SetStringField(root, "AreaResRef", areaState.AreaResRef ?? "");

            // Visited flag
            root.SetUInt8("Visited", areaState.Visited ? (byte)1 : (byte)0);

            // Serialize entity state lists
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity lists are stored as GFF lists
            SerializeEntityStateList(root, "CreatureList", areaState.CreatureStates);
            SerializeEntityStateList(root, "DoorList", areaState.DoorStates);
            SerializeEntityStateList(root, "PlaceableList", areaState.PlaceableStates);
            SerializeEntityStateList(root, "TriggerList", areaState.TriggerStates);
            SerializeEntityStateList(root, "StoreList", areaState.StoreStates);
            SerializeEntityStateList(root, "SoundList", areaState.SoundStates);
            SerializeEntityStateList(root, "WaypointList", areaState.WaypointStates);
            SerializeEntityStateList(root, "EncounterList", areaState.EncounterStates);
            SerializeEntityStateList(root, "CameraList", areaState.CameraStates);

            // Destroyed entity IDs
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Destroyed entities stored as list of ObjectIds
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

            // Spawned entities (dynamically created, not in original GIT)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spawned entities stored with BlueprintResRef
            if (areaState.SpawnedEntities != null && areaState.SpawnedEntities.Count > 0)
            {
                var spawnedList = new GFFList();
                root.SetList("SpawnedList", spawnedList);
                foreach (SpawnedEntityState spawnedState in areaState.SpawnedEntities)
                {
                    var entityStruct = spawnedList.Add();
                    SerializeEntityStateToGFF(entityStruct, spawnedState);
                    SetStringField(entityStruct, "BlueprintResRef", spawnedState.BlueprintResRef ?? "");
                    SetStringField(entityStruct, "SpawnedBy", spawnedState.SpawnedBy ?? "");
                }
            }

            // Area-level local variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area local variables are stored in area's variable system
            if (areaState.LocalVariables != null && GetLocalVariableSetCount(areaState.LocalVariables) > 0)
            {
                var localVarStruct = new GFFStruct();
                root.SetStruct("LocalVariables", localVarStruct);
                SerializeLocalVariableSet(localVarStruct, areaState.LocalVariables);
            }

            return gff;
        }

        /// <summary>
        /// Serializes a list of EntityState objects to a GFF list.
        /// </summary>
        private void SerializeEntityStateList(GFFStruct root, string listName, List<EntityState> entityStates)
        {
            if (entityStates == null || entityStates.Count == 0)
            {
                return;
            }

            var entityList = new GFFList();
            root.SetList(listName, entityList);
            foreach (EntityState entityState in entityStates)
            {
                if (entityState != null)
                {
                    var entityStruct = entityList.Add();
                    SerializeEntityStateToGFF(entityStruct, entityState);
                }
            }
        }

        /// <summary>
        /// Serializes EntityState to GFF struct.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 saves entity state to GFF struct
        /// Matches SaveGameManager.SaveEntityStateToGFF pattern.
        /// </remarks>
        private void SerializeEntityStateToGFF(GFFStruct entityStruct, EntityState entityState)
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
                SerializeLocalVariableSet(localVarStruct, entityState.LocalVariables);
            }

            // Active effects (if present)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Active effects are stored as list of effect structs
            if (entityState.ActiveEffects != null && entityState.ActiveEffects.Count > 0)
            {
                var effectsList = new GFFList();
                entityStruct.SetList("ActiveEffects", effectsList);
                foreach (SavedEffect effect in entityState.ActiveEffects)
                {
                    if (effect != null)
                    {
                        var effectStruct = effectsList.Add();
                        SerializeEffectToGFF(effectStruct, effect);
                    }
                }
            }
        }

        /// <summary>
        /// Serializes LocalVariableSet to GFF struct.
        /// </summary>
        /// <summary>
        /// Gets the total count of variables in a LocalVariableSet by summing all dictionary counts.
        /// </summary>
        private int GetLocalVariableSetCount(LocalVariableSet localVariables)
        {
            if (localVariables == null)
            {
                return 0;
            }
            int count = 0;
            if (localVariables.Ints != null) count += localVariables.Ints.Count;
            if (localVariables.Floats != null) count += localVariables.Floats.Count;
            if (localVariables.Strings != null) count += localVariables.Strings.Count;
            if (localVariables.Objects != null) count += localVariables.Objects.Count;
            if (localVariables.Locations != null) count += localVariables.Locations.Count;
            return count;
        }

        private void SerializeLocalVariableSet(GFFStruct localVarStruct, LocalVariableSet localVariables)
        {
            if (localVarStruct == null || localVariables == null)
            {
                return;
            }

            // Save integer variables
            if (localVariables.Ints != null && localVariables.Ints.Count > 0)
            {
                var intList = new GFFList();
                localVarStruct.SetList("IntList", intList);
                foreach (var kvp in localVariables.Ints)
                {
                    var varStruct = intList.Add();
                    SetStringField(varStruct, "Name", kvp.Key);
                    SetIntField(varStruct, "Value", kvp.Value);
                }
            }

            // Save float variables
            if (localVariables.Floats != null && localVariables.Floats.Count > 0)
            {
                var floatList = new GFFList();
                localVarStruct.SetList("FloatList", floatList);
                foreach (var kvp in localVariables.Floats)
                {
                    var varStruct = floatList.Add();
                    SetStringField(varStruct, "Name", kvp.Key);
                    SetFloatField(varStruct, "Value", kvp.Value);
                }
            }

            // Save string variables
            if (localVariables.Strings != null && localVariables.Strings.Count > 0)
            {
                var stringList = new GFFList();
                localVarStruct.SetList("StringList", stringList);
                foreach (var kvp in localVariables.Strings)
                {
                    var varStruct = stringList.Add();
                    SetStringField(varStruct, "Name", kvp.Key);
                    SetStringField(varStruct, "Value", kvp.Value ?? "");
                }
            }

            // Save object reference variables
            if (localVariables.Objects != null && localVariables.Objects.Count > 0)
            {
                var objectList = new GFFList();
                localVarStruct.SetList("ObjectList", objectList);
                foreach (var kvp in localVariables.Objects)
                {
                    var varStruct = objectList.Add();
                    SetStringField(varStruct, "Name", kvp.Key);
                    SetIntField(varStruct, "Value", (int)kvp.Value);
                }
            }

            // Save location variables
            if (localVariables.Locations != null && localVariables.Locations.Count > 0)
            {
                var locationList = new GFFList();
                localVarStruct.SetList("LocationList", locationList);
                foreach (var kvp in localVariables.Locations)
                {
                    var varStruct = locationList.Add();
                    SetStringField(varStruct, "Name", kvp.Key);
                    SerializeLocationToGFF(varStruct, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Serializes SavedEffect to GFF struct.
        /// </summary>
        private void SerializeEffectToGFF(GFFStruct effectStruct, SavedEffect effect)
        {
            if (effectStruct == null || effect == null)
            {
                return;
            }

            SetIntField(effectStruct, "EffectType", effect.EffectType);
            SetIntField(effectStruct, "SubType", effect.SubType);
            SetIntField(effectStruct, "DurationType", effect.DurationType);
            SetFloatField(effectStruct, "RemainingDuration", effect.RemainingDuration);
            SetIntField(effectStruct, "CreatorId", (int)effect.CreatorId);
            SetIntField(effectStruct, "SpellId", effect.SpellId);

            // Serialize effect parameters
            if (effect.IntParams != null && effect.IntParams.Count > 0)
            {
                var intList = new GFFList();
                effectStruct.SetList("IntParams", intList);
                foreach (int param in effect.IntParams)
                {
                    var paramStruct = intList.Add();
                    SetIntField(paramStruct, "Value", param);
                }
            }

            if (effect.FloatParams != null && effect.FloatParams.Count > 0)
            {
                var floatList = new GFFList();
                effectStruct.SetList("FloatParams", floatList);
                foreach (float param in effect.FloatParams)
                {
                    var paramStruct = floatList.Add();
                    SetFloatField(paramStruct, "Value", param);
                }
            }

            if (effect.StringParams != null && effect.StringParams.Count > 0)
            {
                var stringList = new GFFList();
                effectStruct.SetList("StringParams", stringList);
                foreach (string param in effect.StringParams)
                {
                    var paramStruct = stringList.Add();
                    SetStringField(paramStruct, "Value", param ?? "");
                }
            }

            if (effect.ObjectParams != null && effect.ObjectParams.Count > 0)
            {
                var objectList = new GFFList();
                effectStruct.SetList("ObjectParams", objectList);
                foreach (uint param in effect.ObjectParams)
                {
                    var paramStruct = objectList.Add();
                    SetIntField(paramStruct, "Value", (int)param);
                }
            }
        }

        /// <summary>
        /// Serializes SavedLocation to GFF struct.
        /// </summary>
        private void SerializeLocationToGFF(GFFStruct locationStruct, SavedLocation location)
        {
            if (locationStruct == null || location == null)
            {
                return;
            }

            SetFloatField(locationStruct, "X", location.Position.X);
            SetFloatField(locationStruct, "Y", location.Position.Y);
            SetFloatField(locationStruct, "Z", location.Position.Z);
            SetFloatField(locationStruct, "Facing", location.Facing);
            SetStringField(locationStruct, "AreaResRef", location.AreaResRef ?? "");
        }

        /// <summary>
        /// Deserializes area state.
        /// </summary>
        /// <remarks>
        /// Restores dynamic area changes from save data.
        /// Recreates placed objects and restores modified states.
        /// Applies area effects and transition states.
        ///
        /// Based on reverse engineering of:
        /// - swkotor2.exe: 0x005fb0f0 @ 0x005fb0f0 loads area state from save game
        /// - swkotor2.exe: Area state stored in [module]_s.rim ERF archive as ARE resources
        /// - Located via string references: "Creature List" @ 0x007c0c80, "DoorList" @ 0x007c0c90
        /// - Original implementation: Loads entity states, door/placeable states, destroyed entities, spawned entities
        /// - Each area state GFF contains:
        ///   - AreaResRef: Area resource reference
        ///   - CreatureList: List of creature entity states
        ///   - DoorList: List of door entity states
        ///   - PlaceableList: List of placeable entity states
        ///   - TriggerList: List of trigger entity states
        ///   - StoreList: List of store entity states
        ///   - SoundList: List of sound entity states
        ///   - WaypointList: List of waypoint entity states
        ///   - EncounterList: List of encounter entity states
        ///   - CameraList: List of camera entity states
        ///   - DestroyedList: List of destroyed entity ObjectIds
        ///   - SpawnedList: List of dynamically spawned entities
        ///   - LocalVariables: Area-level local variables
        ///
        /// Implementation flow:
        /// 1. Parse GFF area data into AreaState structure
        /// 2. For each entity state, find matching entity by ObjectId or Tag
        /// 3. Update entity position, HP, door/placeable states, local variables
        /// 4. Remove entities marked as destroyed
        /// 5. Spawn dynamically created entities not in original GIT
        /// 6. Apply area-level local variables
        /// </remarks>
        public override void DeserializeArea(byte[] areaData, IArea area)
        {
            if (areaData == null || areaData.Length == 0)
            {
                return;
            }

            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }

            // Parse GFF area data into AreaState structure
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 loads area state from GFF
            AreaState areaState = DeserializeAreaStateFromGFF(areaData);
            if (areaState == null)
            {
                return;
            }

            // Cast to OdysseyArea for entity manipulation (if needed)
            // We'll try to work with IArea interface first, but may need OdysseyArea-specific methods
            OdysseyArea odysseyArea = area as OdysseyArea;

            // Get all entities from area for lookup
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities are stored in area's entity collections
            Dictionary<uint, IEntity> entityMap = BuildEntityMap(area);

            // 1. Apply entity states to existing entities
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity states are applied by matching ObjectId
            ApplyEntityStates(areaState.CreatureStates, entityMap, area);
            ApplyEntityStates(areaState.DoorStates, entityMap, area);
            ApplyEntityStates(areaState.PlaceableStates, entityMap, area);
            ApplyEntityStates(areaState.TriggerStates, entityMap, area);
            ApplyEntityStates(areaState.StoreStates, entityMap, area);
            ApplyEntityStates(areaState.SoundStates, entityMap, area);
            ApplyEntityStates(areaState.WaypointStates, entityMap, area);
            ApplyEntityStates(areaState.EncounterStates, entityMap, area);
            ApplyEntityStates(areaState.CameraStates, entityMap, area);

            // 2. Remove destroyed entities
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Destroyed entities are removed from area
            if (areaState.DestroyedEntityIds != null && areaState.DestroyedEntityIds.Count > 0)
            {
                RemoveDestroyedEntities(areaState.DestroyedEntityIds, entityMap, odysseyArea);
            }

            // 3. Spawn dynamically created entities
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spawned entities are created from BlueprintResRef
            if (areaState.SpawnedEntities != null && areaState.SpawnedEntities.Count > 0)
            {
                SpawnDynamicEntities(areaState.SpawnedEntities, odysseyArea);
            }

            // 4. Apply area-level local variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area local variables are stored in area's variable system
            if (areaState.LocalVariables != null && GetLocalVariableSetCount(areaState.LocalVariables) > 0)
            {
                ApplyAreaLocalVariables(areaState.LocalVariables, odysseyArea);
            }
        }

        /// <summary>
        /// Deserializes AreaState from GFF byte array.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area state GFF parsing
        /// Located via string references: "Creature List" @ 0x007c0c80
        /// </remarks>
        private AreaState DeserializeAreaStateFromGFF(byte[] areaData)
        {
            if (areaData == null || areaData.Length == 0)
            {
                return null;
            }

            try
            {
                // Parse GFF from byte array
                GFF gff = GFF.FromBytes(areaData);
                if (gff == null || gff.Root == null)
                {
                    return null;
                }

                GFFStruct root = gff.Root;
                var areaState = new AreaState();

                // Area ResRef
                if (root.Exists("AreaResRef"))
                {
                    areaState.AreaResRef = root.GetString("AreaResRef") ?? "";
                }

                // Visited flag
                if (root.Exists("Visited"))
                {
                    areaState.Visited = root.GetUInt8("Visited") != 0;
                }

                // Deserialize entity state lists
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity lists are stored as GFF lists
                DeserializeEntityStateList(root, "CreatureList", areaState.CreatureStates);
                DeserializeEntityStateList(root, "DoorList", areaState.DoorStates);
                DeserializeEntityStateList(root, "PlaceableList", areaState.PlaceableStates);
                DeserializeEntityStateList(root, "TriggerList", areaState.TriggerStates);
                DeserializeEntityStateList(root, "StoreList", areaState.StoreStates);
                DeserializeEntityStateList(root, "SoundList", areaState.SoundStates);
                DeserializeEntityStateList(root, "WaypointList", areaState.WaypointStates);
                DeserializeEntityStateList(root, "EncounterList", areaState.EncounterStates);
                DeserializeEntityStateList(root, "CameraList", areaState.CameraStates);

                // Destroyed entity IDs
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Destroyed entities stored as list of ObjectIds
                if (root.Exists("DestroyedList"))
                {
                    GFFList destroyedList = root.GetList("DestroyedList");
                    if (destroyedList != null)
                    {
                        foreach (GFFStruct item in destroyedList)
                        {
                            if (item.Exists("ObjectId"))
                            {
                                areaState.DestroyedEntityIds.Add(item.GetUInt32("ObjectId"));
                            }
                        }
                    }
                }

                // Spawned entities (dynamically created, not in original GIT)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spawned entities stored with BlueprintResRef
                if (root.Exists("SpawnedList"))
                {
                    GFFList spawnedList = root.GetList("SpawnedList");
                    if (spawnedList != null)
                    {
                        foreach (GFFStruct item in spawnedList)
                        {
                            var spawnedState = new SpawnedEntityState();
                            DeserializeEntityStateFromGFF(item, spawnedState);
                            if (item.Exists("BlueprintResRef"))
                            {
                                ResRef blueprintResRef = item.GetResRef("BlueprintResRef");
                                if (blueprintResRef != null)
                                {
                                    spawnedState.BlueprintResRef = blueprintResRef.ToString();
                                }
                            }
                            if (item.Exists("SpawnedBy"))
                            {
                                spawnedState.SpawnedBy = item.GetString("SpawnedBy") ?? "";
                            }
                            areaState.SpawnedEntities.Add(spawnedState);
                        }
                    }
                }

                // Area-level local variables
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area local variables stored in nested struct
                if (root.Exists("LocalVariables"))
                {
                    GFFStruct localVarStruct = root.GetStruct("LocalVariables");
                    if (localVarStruct != null)
                    {
                        DeserializeLocalVariableSet(localVarStruct, areaState.LocalVariables);
                    }
                }

                return areaState;
            }
            catch (Exception ex)
            {
                // Log error but don't throw - allow area to continue loading with default state
                System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to deserialize area state: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deserializes an entity state list from GFF.
        /// </summary>
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
                DeserializeEntityStateFromGFF(item, entityState);
                targetList.Add(entityState);
            }
        }

        /// <summary>
        /// Deserializes an entity state from a GFF struct.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity state deserialization
        /// Located via string references: "ObjectId" @ 0x007bce5c, "Tag" @ 0x007bd00c
        /// </remarks>
        private void DeserializeEntityStateFromGFF(GFFStruct structData, EntityState state)
        {
            if (structData == null || state == null)
            {
                return;
            }

            // Tag
            if (structData.Exists("Tag"))
            {
                state.Tag = structData.GetString("Tag") ?? "";
            }

            // ObjectId
            if (structData.Exists("ObjectId"))
            {
                state.ObjectId = structData.GetUInt32("ObjectId");
            }

            // ObjectType
            if (structData.Exists("ObjectType"))
            {
                state.ObjectType = (ObjectType)structData.GetInt32("ObjectType");
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

            // Position (can be stored as X/Y/Z or as Vector3)
            if (structData.Exists("X") && structData.Exists("Y") && structData.Exists("Z"))
            {
                state.Position = new System.Numerics.Vector3(
                    structData.GetSingle("X"),
                    structData.GetSingle("Y"),
                    structData.GetSingle("Z")
                );
            }
            else if (structData.Exists("Position"))
            {
                System.Numerics.Vector3 pos = structData.GetVector3("Position");
                state.Position = pos;
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
            if (structData.Exists("LocalVariables") || structData.Exists("LocalVars"))
            {
                GFFStruct localVarStruct = structData.GetStruct("LocalVariables") ?? structData.GetStruct("LocalVars");
                if (localVarStruct != null)
                {
                    DeserializeLocalVariableSet(localVarStruct, state.LocalVariables);
                }
            }

            // Active effects
            if (structData.Exists("ActiveEffects"))
            {
                GFFList effectsList = structData.GetList("ActiveEffects");
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
                            effect.CreatorId = effectStruct.GetUInt32("CreatorId");
                        }
                        if (effectStruct.Exists("SpellId"))
                        {
                            effect.SpellId = effectStruct.GetInt32("SpellId");
                        }
                        // Effect parameters would be deserialized here if needed
                        state.ActiveEffects.Add(effect);
                    }
                }
            }
        }

        /// <summary>
        /// Deserializes local variable set from GFF struct.
        /// </summary>
        private void DeserializeLocalVariableSet(GFFStruct localVarStruct, LocalVariableSet localVars)
        {
            if (localVarStruct == null || localVars == null)
            {
                return;
            }

            // Integer variables
            if (localVarStruct.Exists("IntList") || localVarStruct.Exists("Ints"))
            {
                GFFList intList = localVarStruct.GetList("IntList") ?? localVarStruct.GetList("Ints");
                if (intList != null)
                {
                    foreach (GFFStruct varStruct in intList)
                    {
                        string name = varStruct.Exists("Name") ? (varStruct.GetString("Name") ?? "") : "";
                        if (string.IsNullOrEmpty(name) && varStruct.Exists("Key"))
                        {
                            name = varStruct.GetString("Key") ?? "";
                        }
                        int value = varStruct.Exists("Value") ? varStruct.GetInt32("Value") : 0;
                        if (!string.IsNullOrEmpty(name))
                        {
                            localVars.Ints[name] = value;
                        }
                    }
                }
            }

            // Float variables
            if (localVarStruct.Exists("FloatList") || localVarStruct.Exists("Floats"))
            {
                GFFList floatList = localVarStruct.GetList("FloatList") ?? localVarStruct.GetList("Floats");
                if (floatList != null)
                {
                    foreach (GFFStruct varStruct in floatList)
                    {
                        string name = varStruct.Exists("Name") ? (varStruct.GetString("Name") ?? "") : "";
                        if (string.IsNullOrEmpty(name) && varStruct.Exists("Key"))
                        {
                            name = varStruct.GetString("Key") ?? "";
                        }
                        float value = varStruct.Exists("Value") ? varStruct.GetSingle("Value") : 0.0f;
                        if (!string.IsNullOrEmpty(name))
                        {
                            localVars.Floats[name] = value;
                        }
                    }
                }
            }

            // String variables
            if (localVarStruct.Exists("StringList") || localVarStruct.Exists("Strings"))
            {
                GFFList stringList = localVarStruct.GetList("StringList") ?? localVarStruct.GetList("Strings");
                if (stringList != null)
                {
                    foreach (GFFStruct varStruct in stringList)
                    {
                        string name = varStruct.Exists("Name") ? (varStruct.GetString("Name") ?? "") : "";
                        if (string.IsNullOrEmpty(name) && varStruct.Exists("Key"))
                        {
                            name = varStruct.GetString("Key") ?? "";
                        }
                        string value = varStruct.Exists("Value") ? (varStruct.GetString("Value") ?? "") : "";
                        if (!string.IsNullOrEmpty(name))
                        {
                            localVars.Strings[name] = value;
                        }
                    }
                }
            }

            // Object reference variables
            if (localVarStruct.Exists("ObjectList") || localVarStruct.Exists("Objects"))
            {
                GFFList objectList = localVarStruct.GetList("ObjectList") ?? localVarStruct.GetList("Objects");
                if (objectList != null)
                {
                    foreach (GFFStruct varStruct in objectList)
                    {
                        string name = varStruct.Exists("Name") ? (varStruct.GetString("Name") ?? "") : "";
                        if (string.IsNullOrEmpty(name) && varStruct.Exists("Key"))
                        {
                            name = varStruct.GetString("Key") ?? "";
                        }
                        uint value = varStruct.Exists("Value") ? varStruct.GetUInt32("Value") : 0;
                        if (!string.IsNullOrEmpty(name))
                        {
                            localVars.Objects[name] = value;
                        }
                    }
                }
            }

            // Location variables
            if (localVarStruct.Exists("LocationList") || localVarStruct.Exists("Locations"))
            {
                GFFList locationList = localVarStruct.GetList("LocationList") ?? localVarStruct.GetList("Locations");
                if (locationList != null)
                {
                    foreach (GFFStruct varStruct in locationList)
                    {
                        string name = varStruct.Exists("Name") ? (varStruct.GetString("Name") ?? "") : "";
                        if (string.IsNullOrEmpty(name) && varStruct.Exists("Key"))
                        {
                            name = varStruct.GetString("Key") ?? "";
                        }
                        if (!string.IsNullOrEmpty(name) && varStruct.Exists("Location"))
                        {
                            GFFStruct locationStruct = varStruct.GetStruct("Location");
                            if (locationStruct != null)
                            {
                                var location = new SavedLocation();
                                if (locationStruct.Exists("X") && locationStruct.Exists("Y") && locationStruct.Exists("Z"))
                                {
                                    location.Position = new System.Numerics.Vector3(
                                        locationStruct.GetSingle("X"),
                                        locationStruct.GetSingle("Y"),
                                        locationStruct.GetSingle("Z")
                                    );
                                }
                                if (locationStruct.Exists("Facing"))
                                {
                                    location.Facing = locationStruct.GetSingle("Facing");
                                }
                                if (locationStruct.Exists("AreaResRef"))
                                {
                                    location.AreaResRef = locationStruct.GetString("AreaResRef") ?? "";
                                }
                                localVars.Locations[name] = location;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds a map of ObjectId to IEntity for efficient lookup.
        /// </summary>
        private Dictionary<uint, IEntity> BuildEntityMap(IArea area)
        {
            var entityMap = new Dictionary<uint, IEntity>();

            if (area == null)
            {
                return entityMap;
            }

            // Collect all entities from area
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities are stored in area's entity collections
            IEnumerable<IEntity> allEntities = area.Creatures
                .Concat(area.Placeables)
                .Concat(area.Doors)
                .Concat(area.Triggers)
                .Concat(area.Waypoints)
                .Concat(area.Sounds);

            foreach (IEntity entity in allEntities)
            {
                if (entity != null && entity.IsValid)
                {
                    entityMap[entity.ObjectId] = entity;
                }
            }

            return entityMap;
        }

        /// <summary>
        /// Applies entity states to existing entities in the area.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity state application
        /// Located via string references: "ObjectId" @ 0x007bce5c
        /// </remarks>
        private void ApplyEntityStates(List<EntityState> entityStates, Dictionary<uint, IEntity> entityMap, IArea area)
        {
            if (entityStates == null || entityMap == null || area == null)
            {
                return;
            }

            foreach (EntityState entityState in entityStates)
            {
                if (entityState == null || entityState.ObjectId == 0)
                {
                    continue;
                }

                // Find entity by ObjectId
                if (!entityMap.TryGetValue(entityState.ObjectId, out IEntity entity))
                {
                    // Try to find by Tag as fallback
                    if (!string.IsNullOrEmpty(entityState.Tag))
                    {
                        entity = area.GetObjectByTag(entityState.Tag);
                    }

                    if (entity == null)
                    {
                        // Entity not found - may have been destroyed or not yet loaded
                        continue;
                    }
                }

                // Apply entity state
                ApplyEntityState(entity, entityState);
            }
        }

        /// <summary>
        /// Applies a single entity state to an entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 applies entity state
        /// Updates position, HP, door/placeable states, local variables
        /// </remarks>
        private void ApplyEntityState(IEntity entity, EntityState entityState)
        {
            if (entity == null || entityState == null)
            {
                return;
            }

            // Cast to OdysseyEntity for component access
            OdysseyEntity odysseyEntity = entity as OdysseyEntity;
            if (odysseyEntity == null)
            {
                // Not an OdysseyEntity - try to apply basic properties via IEntity interface
                if (!string.IsNullOrEmpty(entityState.Tag))
                {
                    entity.Tag = entityState.Tag;
                }
                return;
            }

            // Update Transform component (position and facing)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Position stored as X, Y, Z in GFF
            var transformComponent = entity.GetComponent<ITransformComponent>();
            if (transformComponent != null)
            {
                if (entityState.Position.X != 0.0f || entityState.Position.Y != 0.0f || entityState.Position.Z != 0.0f)
                {
                    transformComponent.Position = entityState.Position;
                }
                if (entityState.Facing != 0.0f)
                {
                    transformComponent.Facing = entityState.Facing;
                }
            }

            // Update Stats component (HP)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): HP stored as CurrentHP and MaxHP
            var statsComponent = entity.GetComponent<IStatsComponent>();
            if (statsComponent != null)
            {
                if (entityState.CurrentHP > 0)
                {
                    // Use reflection to set CurrentHP if available
                    var currentHpProperty = statsComponent.GetType().GetProperty("CurrentHP");
                    if (currentHpProperty != null && currentHpProperty.CanWrite)
                    {
                        currentHpProperty.SetValue(statsComponent, entityState.CurrentHP);
                    }
                }
                if (entityState.MaxHP > 0)
                {
                    // Use reflection to set MaxHP if available
                    var maxHpProperty = statsComponent.GetType().GetProperty("MaxHP");
                    if (maxHpProperty != null && maxHpProperty.CanWrite)
                    {
                        maxHpProperty.SetValue(statsComponent, entityState.MaxHP);
                    }
                }
            }

            // Update Door component (open/locked state)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Door states stored as IsOpen and IsLocked
            if (entity.ObjectType == ObjectType.Door)
            {
                var doorComponent = entity.GetComponent<IDoorComponent>();
                if (doorComponent != null)
                {
                    // Use reflection to set door state if available
                    var isOpenProperty = doorComponent.GetType().GetProperty("IsOpen");
                    if (isOpenProperty != null && isOpenProperty.CanWrite)
                    {
                        isOpenProperty.SetValue(doorComponent, entityState.IsOpen);
                    }
                    var isLockedProperty = doorComponent.GetType().GetProperty("IsLocked");
                    if (isLockedProperty != null && isLockedProperty.CanWrite)
                    {
                        isLockedProperty.SetValue(doorComponent, entityState.IsLocked);
                    }
                }
            }

            // Update Placeable component (open/locked/destroyed state)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Placeable states stored as IsOpen, IsLocked, IsDestroyed
            if (entity.ObjectType == ObjectType.Placeable)
            {
                var placeableComponent = entity.GetComponent<IPlaceableComponent>();
                if (placeableComponent != null)
                {
                    // Use reflection to set placeable state if available
                    var isOpenProperty = placeableComponent.GetType().GetProperty("IsOpen");
                    if (isOpenProperty != null && isOpenProperty.CanWrite)
                    {
                        isOpenProperty.SetValue(placeableComponent, entityState.IsOpen);
                    }
                    var isLockedProperty = placeableComponent.GetType().GetProperty("IsLocked");
                    if (isLockedProperty != null && isLockedProperty.CanWrite)
                    {
                        isLockedProperty.SetValue(placeableComponent, entityState.IsLocked);
                    }
                    var isDestroyedProperty = placeableComponent.GetType().GetProperty("IsDestroyed");
                    if (isDestroyedProperty != null && isDestroyedProperty.CanWrite)
                    {
                        isDestroyedProperty.SetValue(placeableComponent, entityState.IsDestroyed);
                    }
                }
            }

            // Apply local variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Local variables stored in entity's variable system
            if (entityState.LocalVariables != null && !entityState.LocalVariables.IsEmpty)
            {
                ApplyLocalVariablesToEntity(odysseyEntity, entityState.LocalVariables);
            }

            // Apply active effects
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Active effects stored in entity's effect system
            if (entityState.ActiveEffects != null && entityState.ActiveEffects.Count > 0)
            {
                ApplyActiveEffectsToEntity(odysseyEntity, entityState.ActiveEffects);
            }
        }

        /// <summary>
        /// Applies local variables to an entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Local variable application
        /// Uses entity's variable system to store local variables via IScriptGlobals interface
        ///
        /// Local variables are stored per entity (by ObjectId) in the ScriptGlobals system.
        /// This method applies all variable types from the LocalVariableSet:
        /// - Integer variables (SetLocalInt)
        /// - Float variables (SetLocalFloat)
        /// - String variables (SetLocalString)
        /// - Object reference variables (SetLocalObject)
        /// - Location variables (SetLocalLocation)
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 applies entity local variables during save game load
        /// Located via string references: "LocalVariables" @ entity state structure
        /// Original implementation: Reads local variables from GFF structure and applies them to entity's variable storage
        /// </remarks>
        private void ApplyLocalVariablesToEntity(OdysseyEntity entity, LocalVariableSet localVars)
        {
            if (entity == null || localVars == null)
            {
                return;
            }

            // Check if ScriptGlobals is available
            if (_scriptGlobals == null)
            {
                // ScriptGlobals not set - cannot apply local variables
                // This is expected if SetScriptGlobals has not been called
                // Local variables will not be restored in this case
                return;
            }

            // Cast entity to IEntity for ScriptGlobals interface
            IEntity entityInterface = entity;

            // Apply integer variables
            if (localVars.Ints != null)
            {
                foreach (KeyValuePair<string, int> kvp in localVars.Ints)
                {
                    _scriptGlobals.SetLocalInt(entityInterface, kvp.Key, kvp.Value);
                }
            }

            // Apply float variables
            if (localVars.Floats != null)
            {
                foreach (KeyValuePair<string, float> kvp in localVars.Floats)
                {
                    _scriptGlobals.SetLocalFloat(entityInterface, kvp.Key, kvp.Value);
                }
            }

            // Apply string variables
            if (localVars.Strings != null)
            {
                foreach (KeyValuePair<string, string> kvp in localVars.Strings)
                {
                    // Handle null strings - ScriptGlobals may expect empty string instead of null
                    string value = kvp.Value ?? string.Empty;
                    _scriptGlobals.SetLocalString(entityInterface, kvp.Key, value);
                }
            }

            // Apply object reference variables
            // Object references are stored as ObjectId (uint) and need to be resolved to IEntity
            // Based on ScriptGlobals implementation: SetLocalObject stores ObjectId, GetLocalObject resolves via entity.World
            if (localVars.Objects != null && localVars.Objects.Count > 0)
            {
                // Entities have a World property that provides access to IWorld for entity lookup
                IWorld world = entityInterface.World;
                if (world != null)
                {
                    foreach (KeyValuePair<string, uint> kvp in localVars.Objects)
                    {
                        uint objectId = kvp.Value;
                        // Resolve ObjectId to IEntity
                        // ObjectId 0x7F000000 (OBJECT_INVALID) represents null/invalid object reference
                        if (objectId == 0x7F000000)
                        {
                            // Invalid object reference - set to null
                            _scriptGlobals.SetLocalObject(entityInterface, kvp.Key, null);
                        }
                        else
                        {
                            // Resolve ObjectId to entity via world lookup
                            IEntity referencedEntity = world.GetEntity(objectId);
                            _scriptGlobals.SetLocalObject(entityInterface, kvp.Key, referencedEntity);
                        }
                    }
                }
            }

            // Apply location variables
            if (localVars.Locations != null)
            {
                foreach (KeyValuePair<string, SavedLocation> kvp in localVars.Locations)
                {
                    SavedLocation savedLocation = kvp.Value;
                    if (savedLocation != null)
                    {
                        // Create Location object from SavedLocation
                        // Based on ScriptGlobals implementation: Location type is used for location variables
                        // Location contains Position (Vector3) and Facing (float)
                        // Note: AreaResRef is stored separately in SavedLocation but Location type doesn't include it
                        // Area context is implicit based on where the entity/script is executing
                        Location locationObject = new Location(savedLocation.Position, savedLocation.Facing);
                        _scriptGlobals.SetLocalLocation(entityInterface, kvp.Key, locationObject);
                    }
                }
            }
        }

        /// <summary>
        /// Converts an ActiveEffect to a SavedEffect for serialization.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Effect serialization format
        /// Maps Effect properties and ActiveEffect metadata to SavedEffect structure
        /// </remarks>
        private SavedEffect ConvertActiveEffectToSavedEffect(ActiveEffect activeEffect)
        {
            if (activeEffect == null || activeEffect.Effect == null)
            {
                return null;
            }

            var savedEffect = new SavedEffect
            {
                EffectType = (int)activeEffect.Effect.Type,
                SubType = activeEffect.Effect.SubType,
                DurationType = (int)activeEffect.Effect.DurationType,
                RemainingDuration = activeEffect.RemainingRounds, // Store remaining rounds as duration
                CreatorId = activeEffect.Creator != null ? activeEffect.Creator.ObjectId : 0,
                SpellId = 0, // SpellId would need to be stored in ActiveEffect if needed
                IntParams = new List<int>(),
                FloatParams = new List<float>(),
                StringParams = new List<string>(),
                ObjectParams = new List<uint>()
            };

            // Store effect-specific parameters in IntParams
            // Amount, VisualEffectId, and other integer properties
            savedEffect.IntParams.Add(activeEffect.Effect.Amount);
            savedEffect.IntParams.Add(activeEffect.Effect.VisualEffectId);
            savedEffect.IntParams.Add(activeEffect.Effect.IsSupernatural ? 1 : 0);
            savedEffect.IntParams.Add(activeEffect.Effect.DurationRounds); // Original duration

            // Store AppliedAt timestamp as float if needed
            savedEffect.FloatParams.Add(activeEffect.AppliedAt);

            return savedEffect;
        }

        /// <summary>
        /// Converts a SavedEffect back to an Effect for application.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Effect deserialization
        /// Reconstructs Effect object from SavedEffect data
        /// </remarks>
        private Effect ConvertSavedEffectToEffect(SavedEffect savedEffect)
        {
            if (savedEffect == null)
            {
                return null;
            }

            // Create effect with the stored type
            EffectType effectType = (EffectType)savedEffect.EffectType;
            var effect = new Effect(effectType);

            // Restore effect properties
            effect.SubType = savedEffect.SubType;
            effect.DurationType = (EffectDurationType)savedEffect.DurationType;

            // Restore parameters from IntParams list
            if (savedEffect.IntParams != null && savedEffect.IntParams.Count >= 4)
            {
                effect.Amount = savedEffect.IntParams[0];
                effect.VisualEffectId = savedEffect.IntParams[1];
                effect.IsSupernatural = savedEffect.IntParams[2] != 0;
                // Use RemainingDuration for DurationRounds to restore the actual remaining time
                // This ensures effects expire at the correct time after loading
                effect.DurationRounds = (int)savedEffect.RemainingDuration;
            }
            else
            {
                // Fallback: use RemainingDuration if IntParams not available
                effect.DurationRounds = (int)savedEffect.RemainingDuration;
            }

            return effect;
        }

        /// <summary>
        /// Applies active effects to an entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Active effect application
        /// Uses entity's effect system to restore active effects
        /// </remarks>
        private void ApplyActiveEffectsToEntity(OdysseyEntity entity, List<SavedEffect> activeEffects)
        {
            if (entity == null || activeEffects == null || activeEffects.Count == 0)
            {
                return;
            }

            // Get world and effect system from entity
            if (entity.World == null || entity.World.EffectSystem == null)
            {
                return;
            }

            EffectSystem effectSystem = entity.World.EffectSystem;

            // Convert each SavedEffect to Effect and apply it
            foreach (SavedEffect savedEffect in activeEffects)
            {
                if (savedEffect == null)
                {
                    continue;
                }

                // Convert SavedEffect to Effect
                Effect effect = ConvertSavedEffectToEffect(savedEffect);
                if (effect == null)
                {
                    continue;
                }

                // Find creator entity by ObjectId if available
                IEntity creator = null;
                if (savedEffect.CreatorId != 0)
                {
                    creator = entity.World.GetEntity(savedEffect.CreatorId);
                }

                // Apply effect via EffectSystem
                // Note: The remaining duration will be set by the EffectSystem when creating ActiveEffect
                // We restore the original duration, and EffectSystem will handle remaining rounds
                effectSystem.ApplyEffect(entity, effect, creator);

                // After applying, we may need to update the remaining rounds on the ActiveEffect
                // This requires getting the ActiveEffect back from EffectSystem and updating RemainingRounds
                // However, since EffectSystem doesn't expose a way to get a specific ActiveEffect,
                // we rely on the EffectSystem's internal duration management
                // The RemainingDuration in SavedEffect represents remaining rounds, but EffectSystem
                // uses DurationRounds for initial application
                // For accurate restoration, we would need to store RemainingRounds separately or
                // modify EffectSystem to support setting remaining rounds on application
            }
        }

        /// <summary>
        /// Removes destroyed entities from the area.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Destroyed entity removal
        /// Located via string references: "EVENT_REMOVE_FROM_AREA" @ 0x007bcddc
        /// </remarks>
        private void RemoveDestroyedEntities(List<uint> destroyedEntityIds, Dictionary<uint, IEntity> entityMap, OdysseyArea odysseyArea)
        {
            if (destroyedEntityIds == null || destroyedEntityIds.Count == 0)
            {
                return;
            }

            if (odysseyArea == null)
            {
                // Cannot remove entities without OdysseyArea access
                return;
            }

            // Use reflection to access private entity collections
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities are removed from area's entity collections
            var creaturesField = typeof(OdysseyArea).GetField("_creatures", BindingFlags.NonPublic | BindingFlags.Instance);
            var placeablesField = typeof(OdysseyArea).GetField("_placeables", BindingFlags.NonPublic | BindingFlags.Instance);
            var doorsField = typeof(OdysseyArea).GetField("_doors", BindingFlags.NonPublic | BindingFlags.Instance);
            var triggersField = typeof(OdysseyArea).GetField("_triggers", BindingFlags.NonPublic | BindingFlags.Instance);
            var waypointsField = typeof(OdysseyArea).GetField("_waypoints", BindingFlags.NonPublic | BindingFlags.Instance);
            var soundsField = typeof(OdysseyArea).GetField("_sounds", BindingFlags.NonPublic | BindingFlags.Instance);

            System.Collections.IList[] collections = new System.Collections.IList[]
            {
                creaturesField?.GetValue(odysseyArea) as System.Collections.IList,
                placeablesField?.GetValue(odysseyArea) as System.Collections.IList,
                doorsField?.GetValue(odysseyArea) as System.Collections.IList,
                triggersField?.GetValue(odysseyArea) as System.Collections.IList,
                waypointsField?.GetValue(odysseyArea) as System.Collections.IList,
                soundsField?.GetValue(odysseyArea) as System.Collections.IList
            };

            foreach (uint objectId in destroyedEntityIds)
            {
                if (objectId == 0)
                {
                    continue;
                }

                if (!entityMap.TryGetValue(objectId, out IEntity entity))
                {
                    continue;
                }

                // Remove from appropriate collection
                foreach (System.Collections.IList collection in collections)
                {
                    if (collection != null)
                    {
                        collection.Remove(entity);
                    }
                }

                // Mark entity as invalid
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Destroyed entities are marked as invalid
                if (entity is OdysseyEntity odysseyEntity)
                {
                    var isValidField = typeof(OdysseyEntity).GetField("_isValid", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (isValidField != null)
                    {
                        isValidField.SetValue(odysseyEntity, false);
                    }
                }
            }
        }

        /// <summary>
        /// Spawns dynamically created entities in the area.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Dynamic entity spawning
        /// Located via string references: "CreateObject" @ 0x007bd0e0
        /// Spawns entities from BlueprintResRef that were not in original GIT
        ///
        /// Original implementation (swkotor2.exe: 0x005fb0f0 @ 0x005fb0f0):
        /// - Loads SpawnedList from area state GFF
        /// - For each spawned entity:
        ///   1. Creates entity from BlueprintResRef template (UTC, UTP, UTD, etc.)
        ///   2. Applies saved entity state (position, HP, components, local variables)
        ///   3. Sets ObjectId from saved state (preserves original ObjectId)
        ///   4. Registers entity with world
        ///   5. Adds entity to appropriate area collection (Creatures, Placeables, Doors, etc.)
        /// - Entities spawned this way are dynamically created (not in original GIT file)
        /// - BlueprintResRef identifies the template resource (UTC for creatures, UTP for placeables, etc.)
        /// - SpawnedBy field contains script name that originally spawned the entity (for debugging)
        /// </remarks>
        private void SpawnDynamicEntities(List<SpawnedEntityState> spawnedEntities, OdysseyArea odysseyArea)
        {
            if (spawnedEntities == null || spawnedEntities.Count == 0)
            {
                return;
            }

            if (odysseyArea == null)
            {
                // Cannot spawn entities without OdysseyArea access
                return;
            }

            // Get World from area entities
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): World is accessed via entity references
            IWorld world = GetWorldFromAreaEntities(odysseyArea);
            if (world == null)
            {
                System.Diagnostics.Debug.WriteLine("[OdysseySaveSerializer] SpawnDynamicEntities: Cannot spawn entities - no world reference available");
                return;
            }

            // Get Module from world
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module is required for loading template resources (UTC, UTP, UTD, etc.)
            RuntimeIModule runtimeModule = world.CurrentModule;
            if (runtimeModule == null)
            {
                System.Diagnostics.Debug.WriteLine("[OdysseySaveSerializer] SpawnDynamicEntities: Cannot spawn entities - no module loaded");
                return;
            }

            // Get parsing Module for resource loading
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module resources (UTC, UTP, etc.) are loaded from module archives
            BioWare.NET.Common.Module parsingModule = GetParsingModuleFromRuntimeModule(runtimeModule);
            if (parsingModule == null)
            {
                System.Diagnostics.Debug.WriteLine("[OdysseySaveSerializer] SpawnDynamicEntities: Cannot spawn entities - parsing module not available");
                return;
            }

            // Create EntityFactory for loading templates
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EntityFactory loads GFF templates (UTC, UTP, UTD, etc.) and creates entities
            // Located via string references: "TemplateResRef" @ 0x007bd00c, "Creature template '%s' doesn't exist.\n" @ 0x007bf78c
            // Original implementation: 0x005fb0f0 @ 0x005fb0f0 loads creature templates from GFF
            var entityFactory = new Loading.EntityFactory();

            // Spawn each dynamically created entity
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities are spawned from BlueprintResRef
            foreach (SpawnedEntityState spawnedState in spawnedEntities)
            {
                if (spawnedState == null || string.IsNullOrEmpty(spawnedState.BlueprintResRef))
                {
                    continue;
                }

                // Determine ObjectType from entity state
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ObjectType is stored in entity state
                ObjectType objectType = spawnedState.ObjectType;
                if (objectType == ObjectType.Invalid || objectType == 0)
                {
                    // Try to infer ObjectType from BlueprintResRef resource type
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Template resource types determine ObjectType
                    // UTC = Creature, UTP = Placeable, UTD = Door, UTT = Trigger, UTW = Waypoint, UTS = Sound
                    objectType = InferObjectTypeFromBlueprint(spawnedState.BlueprintResRef, parsingModule);
                    if (objectType == ObjectType.Invalid)
                    {
                        // Default to Creature if cannot determine
                        objectType = ObjectType.Creature;
                    }
                }

                // Create entity from template
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 creates entity from template ResRef
                IEntity entity = null;
                System.Numerics.Vector3 position = spawnedState.Position;
                float facing = spawnedState.Facing;

                // Use ObjectId from saved state if available, otherwise EntityFactory will generate one
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ObjectId is preserved from save game to maintain entity references
                uint objectId = spawnedState.ObjectId;
                if (objectId != 0 && objectId != 0x7F000000) // 0x7F000000 = OBJECT_INVALID
                {
                    // Create entity with specific ObjectId to preserve save game references
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ObjectId must be preserved for script references and entity lookups
                    entity = CreateEntityWithObjectId(objectType, objectId, position, facing);
                }
                else
                {
                    // Use EntityFactory to create entity (will generate new ObjectId)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): New ObjectId assigned if not in save state
                    entity = CreateEntityFromTemplate(entityFactory, parsingModule, spawnedState.BlueprintResRef, objectType, position, facing);
                }

                if (entity == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] SpawnDynamicEntities: Failed to create entity from template: {spawnedState.BlueprintResRef}");
                    continue;
                }

                // Load template data into entity
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Template properties are loaded after entity creation
                if (objectId != 0 && objectId != 0x7F000000)
                {
                    // Entity was created with specific ObjectId, need to load template manually
                    LoadTemplateIntoEntity(entity, parsingModule, spawnedState.BlueprintResRef, objectType);
                }

                // Apply saved entity state (position, HP, components, local variables, etc.)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 applies entity state after template loading
                ApplyEntityState(entity, spawnedState);

                // Set Tag from entity state if not already set from template
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Tag is preserved from save game
                if (!string.IsNullOrEmpty(spawnedState.Tag) && string.IsNullOrEmpty(entity.Tag))
                {
                    entity.Tag = spawnedState.Tag;
                }

                // Set TemplateResRef for reference
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TemplateResRef is stored in entity data
                if (entity is Runtime.Core.Entities.Entity coreEntity)
                {
                    coreEntity.SetData("TemplateResRef", spawnedState.BlueprintResRef);
                    if (!string.IsNullOrEmpty(spawnedState.SpawnedBy))
                    {
                        coreEntity.SetData("SpawnedBy", spawnedState.SpawnedBy);
                    }
                }

                // Register entity with world
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities must be registered with world for lookups and updates
                // Located via string references: "RegisterEntity" functionality in world system
                if (entity.World == null)
                {
                    entity.World = world;
                }
                if (world.GetEntity(entity.ObjectId) == null)
                {
                    world.RegisterEntity(entity);
                }

                // Set AreaId for entity
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AreaId links entity to area for area-specific operations
                uint areaId = world.GetAreaId(odysseyArea);
                if (areaId != 0)
                {
                    entity.AreaId = areaId;
                }

                // Add entity to appropriate area collection
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities are added to type-specific area collections
                // Located via string references: Area entity collections (Creatures, Placeables, Doors, etc.)
                // Note: AddEntityToArea is protected, so we use the internal method via BaseArea
                if (odysseyArea is BaseArea baseArea)
                {
                    baseArea.AddEntityToArea(entity);
                }

                System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] SpawnDynamicEntities: Successfully spawned entity {entity.ObjectId} ({spawnedState.BlueprintResRef}) at {position}");
            }
        }

        /// <summary>
        /// Gets World reference from area entities.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): World is accessed via entity references
        /// Tries to get World from any entity in the area
        /// </remarks>
        private IWorld GetWorldFromAreaEntities(OdysseyArea odysseyArea)
        {
            if (odysseyArea == null)
            {
                return null;
            }

            // Try to get World from creatures first
            foreach (IEntity creature in odysseyArea.Creatures)
            {
                if (creature != null && creature.World != null)
                {
                    return creature.World;
                }
            }

            // Try other entity types
            foreach (IEntity entity in odysseyArea.Placeables.Concat(odysseyArea.Doors).Concat(odysseyArea.Triggers).Concat(odysseyArea.Waypoints).Concat(odysseyArea.Sounds))
            {
                if (entity != null && entity.World != null)
                {
                    return entity.World;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets parsing Module from runtime Module.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module resources are accessed via parsing Module
        /// Runtime Module may contain reference to parsing Module for resource loading
        /// </remarks>
        private BioWare.NET.Common.Module GetParsingModuleFromRuntimeModule(RuntimeIModule runtimeModule)
        {
            if (runtimeModule == null)
            {
                return null;
            }

            // Try to get parsing Module via reflection or interface
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module system provides resource access
            // Runtime Module implementations may expose parsing Module
            var parsingModuleProperty = runtimeModule.GetType().GetProperty("ParsingModule");
            if (parsingModuleProperty != null)
            {
                return parsingModuleProperty.GetValue(runtimeModule) as BioWare.NET.Common.Module;
            }

            // Try to get via GetParsingModule method if available
            var getParsingModuleMethod = runtimeModule.GetType().GetMethod("GetParsingModule");
            if (getParsingModuleMethod != null)
            {
                return getParsingModuleMethod.Invoke(runtimeModule, null) as BioWare.NET.Common.Module;
            }

            // Try to access ModuleLoader if available
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ModuleLoader provides Module access
            var moduleLoaderField = runtimeModule.GetType().GetField("_moduleLoader", BindingFlags.NonPublic | BindingFlags.Instance);
            if (moduleLoaderField != null)
            {
                object moduleLoader = moduleLoaderField.GetValue(runtimeModule);
                if (moduleLoader != null)
                {
                    var getParsingModuleMethod2 = moduleLoader.GetType().GetMethod("GetParsingModule");
                    if (getParsingModuleMethod2 != null)
                    {
                        return getParsingModuleMethod2.Invoke(moduleLoader, null) as BioWare.NET.Common.Module;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Infers ObjectType from BlueprintResRef by checking available resource types.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Template resource types determine ObjectType
        /// UTC = Creature, UTP = Placeable, UTD = Door, UTT = Trigger, UTW = Waypoint, UTS = Sound
        /// </remarks>
        private Runtime.Core.Enums.ObjectType InferObjectTypeFromBlueprint(string blueprintResRef, BioWare.NET.Common.Module module)
        {
            if (string.IsNullOrEmpty(blueprintResRef) || module == null)
            {
                return ObjectType.Invalid;
            }

            // Try Creature (UTC) first (most common)
            if (module.Creature(blueprintResRef) != null)
            {
                return ObjectType.Creature;
            }

            // Try Placeable (UTP)
            if (module.Placeable(blueprintResRef) != null)
            {
                return ObjectType.Placeable;
            }

            // Try Door (UTD)
            if (module.Door(blueprintResRef) != null)
            {
                return ObjectType.Door;
            }

            // Try Trigger (UTT)
            if (module.Trigger(blueprintResRef) != null)
            {
                return ObjectType.Trigger;
            }

            // Try Waypoint (UTW)
            if (module.Waypoint(blueprintResRef) != null)
            {
                return ObjectType.Waypoint;
            }

            // Try Sound (UTS)
            if (module.Sound(blueprintResRef) != null)
            {
                return ObjectType.Sound;
            }

            // Try Store (UTM)
            if (module.Store(blueprintResRef) != null)
            {
                return ObjectType.Store;
            }

            return ObjectType.Invalid;
        }

        /// <summary>
        /// Creates an entity with a specific ObjectId.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities can be created with specific ObjectId to preserve save game references
        /// </remarks>
        private IEntity CreateEntityWithObjectId(Runtime.Core.Enums.ObjectType objectType, uint objectId, System.Numerics.Vector3 position, float facing)
        {
            // Create entity using Core.Entities.Entity constructor with specific ObjectId
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity constructor accepts ObjectId parameter
            var entity = new Runtime.Core.Entities.Entity(objectId, objectType);
            entity.Position = position;
            entity.Facing = facing;
            return entity;
        }

        /// <summary>
        /// Creates an entity from template using EntityFactory.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): EntityFactory creates entities from templates
        /// </remarks>
        private IEntity CreateEntityFromTemplate(Loading.EntityFactory entityFactory, BioWare.NET.Common.Module module, string templateResRef, Runtime.Core.Enums.ObjectType objectType, System.Numerics.Vector3 position, float facing)
        {
            if (entityFactory == null || module == null || string.IsNullOrEmpty(templateResRef))
            {
                return null;
            }

            // Use appropriate EntityFactory method based on ObjectType
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Different template types use different creation methods
            switch (objectType)
            {
                case ObjectType.Creature:
                    return entityFactory.CreateCreatureFromTemplate(module, templateResRef, position, facing);

                case ObjectType.Placeable:
                    return entityFactory.CreatePlaceableFromTemplate(module, templateResRef, position, facing);

                case ObjectType.Door:
                    return entityFactory.CreateDoorFromTemplate(module, templateResRef, position, facing);

                case ObjectType.Store:
                    return entityFactory.CreateStoreFromTemplate(module, templateResRef, position, facing);

                default:
                    // For other types, try creature as fallback
                    return entityFactory.CreateCreatureFromTemplate(module, templateResRef, position, facing);
            }
        }

        /// <summary>
        /// Loads template data into an existing entity.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Template data is loaded into entity after creation
        /// Used when entity is created with specific ObjectId (not via EntityFactory)
        /// </remarks>
        private void LoadTemplateIntoEntity(IEntity entity, BioWare.NET.Common.Module module, string templateResRef, Runtime.Core.Enums.ObjectType objectType)
        {
            if (entity == null || module == null || string.IsNullOrEmpty(templateResRef))
            {
                return;
            }

            // Create temporary EntityFactory to access template loading methods
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Template loading uses EntityFactory internal methods
            var entityFactory = new Loading.EntityFactory();

            // Use reflection to access private template loading methods
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Template loading is internal to EntityFactory
            MethodInfo loadMethod = null;
            switch (objectType)
            {
                case ObjectType.Creature:
                    loadMethod = typeof(Loading.EntityFactory).GetMethod("LoadCreatureTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;

                case ObjectType.Placeable:
                    loadMethod = typeof(Loading.EntityFactory).GetMethod("LoadPlaceableTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;

                case ObjectType.Door:
                    loadMethod = typeof(Loading.EntityFactory).GetMethod("LoadDoorTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;

                case ObjectType.Store:
                    loadMethod = typeof(Loading.EntityFactory).GetMethod("LoadStoreTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
                    break;
            }

            if (loadMethod != null && entity is Runtime.Core.Entities.Entity coreEntity)
            {
                try
                {
                    loadMethod.Invoke(entityFactory, new object[] { coreEntity as object, module, templateResRef });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] LoadTemplateIntoEntity: Failed to load template {templateResRef}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Applies area-level local variables.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Area local variable storage
        /// Area variables are stored separately from entity variables
        /// Original implementation: 0x005226d0 @ 0x005226d0 applies area variables from save file
        /// Variables are restored to area's variable storage system when area is loaded from save
        /// </remarks>
        private void ApplyAreaLocalVariables(LocalVariableSet localVariables, OdysseyArea odysseyArea)
        {
            if (localVariables == null || localVariables.IsEmpty)
            {
                return;
            }

            if (odysseyArea == null)
            {
                return;
            }

            // Get area's variable storage
            LocalVariableSet areaVars = odysseyArea.GetLocalVariables();

            // Copy integer variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Integer variables are restored from save file
            if (localVariables.Ints != null && localVariables.Ints.Count > 0)
            {
                foreach (var kvp in localVariables.Ints)
                {
                    areaVars.Ints[kvp.Key] = kvp.Value;
                }
            }

            // Copy float variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Float variables are restored from save file
            if (localVariables.Floats != null && localVariables.Floats.Count > 0)
            {
                foreach (var kvp in localVariables.Floats)
                {
                    areaVars.Floats[kvp.Key] = kvp.Value;
                }
            }

            // Copy string variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): String variables are restored from save file
            if (localVariables.Strings != null && localVariables.Strings.Count > 0)
            {
                foreach (var kvp in localVariables.Strings)
                {
                    areaVars.Strings[kvp.Key] = kvp.Value ?? "";
                }
            }

            // Copy object reference variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Object reference variables are restored from save file
            if (localVariables.Objects != null && localVariables.Objects.Count > 0)
            {
                foreach (var kvp in localVariables.Objects)
                {
                    areaVars.Objects[kvp.Key] = kvp.Value;
                }
            }

            // Copy location variables
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Location variables are restored from save file
            if (localVariables.Locations != null && localVariables.Locations.Count > 0)
            {
                foreach (var kvp in localVariables.Locations)
                {
                    if (kvp.Value != null)
                    {
                        areaVars.Locations[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Serializes entity collection.
        /// </summary>
        /// <remarks>
        /// Based on [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004e28c0) - Saves creature stats, inventory, position, scripts.
        /// Uses GFF format with entity ObjectId as key.
        ///
        /// Entity data includes:
        /// - ObjectId, Tag, ObjectType
        /// - Position and orientation
        /// - Stats (HP, FP, attributes)
        /// - Equipment and inventory
        /// - Active scripts and effects
        /// - AI state and waypoints
        ///
        /// Implementation details:
        /// - Creates GFF with "Creature List" list ([TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x007c0c80))
        /// - For each entity, serializes to GFF and adds root struct to list
        /// - Each list entry contains ObjectId and all entity component data
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004e28c0): 0x004e28c0 creates "Creature List" structure
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x005226d0): 0x005226d0 serializes individual entity data into struct
        /// </remarks>
        public override byte[] SerializeEntities(IEnumerable<IEntity> entities)
        {
            if (entities == null)
            {
                // Return empty GFF with empty CreatureList
                var emptyGff = new GFF();
                emptyGff.Root.SetList("Creature List", new GFFList());
                return emptyGff.ToBytes();
            }

            // Create GFF structure for entity collection
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x004e28c0 @ 0x004e28c0 creates "Creature List" structure
            // Located via string reference: "Creature List" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007c0c80))
            var gff = new GFF();
            var root = gff.Root;
            var creatureList = new GFFList();

            // Serialize each entity
            foreach (IEntity entity in entities)
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Check if entity is a BaseEntity (has Serialize method)
                BaseEntity baseEntity = entity as BaseEntity;
                if (baseEntity == null)
                {
                    // For non-BaseEntity IEntity instances, create minimal serialization
                    var entityStruct = creatureList.Add();
                    entityStruct.SetUInt32("ObjectId", entity.ObjectId);
                    entityStruct.SetString("Tag", entity.Tag ?? "");
                    entityStruct.SetInt32("ObjectType", (int)entity.ObjectType);
                    entityStruct.SetUInt32("AreaId", entity.AreaId);
                    continue;
                }

                // Serialize entity to GFF
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 serializes entity to GFF struct
                byte[] entityData = baseEntity.Serialize();
                if (entityData == null || entityData.Length == 0)
                {
                    continue;
                }

                // Parse entity's serialized GFF
                GFF entityGff = GFF.FromBytes(entityData);
                GFFStruct entityRoot = entityGff.Root;

                // Create struct entry in CreatureList
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00413600 creates struct entry, 0x00413880 sets ObjectId
                var listEntry = creatureList.Add();

                // Copy all fields from entity's root struct to list entry
                // This includes ObjectId, Tag, ObjectType, AreaId, and all component data
                CopyGffStructFields(entityRoot, listEntry);
            }

            // Set CreatureList in root
            root.SetList("Creature List", creatureList);

            // Serialize GFF to bytes
            return gff.ToBytes();
        }

        /// <summary>
        /// Copies all fields from source GFF struct to destination struct.
        /// </summary>
        /// <remarks>
        /// Helper method to merge entity serialization data into list entries.
        /// Handles all GFF field types including nested structs and lists.
        /// </remarks>
        private void CopyGffStructFields(GFFStruct source, GFFStruct destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            // Iterate through all fields in source struct
            foreach (var (label, fieldType, value) in source)
            {
                if (string.IsNullOrEmpty(label))
                {
                    continue;
                }

                // Copy field based on type
                switch (fieldType)
                {
                    case GFFFieldType.UInt8:
                        destination.SetUInt8(label, source.GetUInt8(label));
                        break;
                    case GFFFieldType.Int8:
                        destination.SetInt8(label, source.GetInt8(label));
                        break;
                    case GFFFieldType.UInt16:
                        destination.SetUInt16(label, source.GetUInt16(label));
                        break;
                    case GFFFieldType.Int16:
                        destination.SetInt16(label, source.GetInt16(label));
                        break;
                    case GFFFieldType.UInt32:
                        destination.SetUInt32(label, source.GetUInt32(label));
                        break;
                    case GFFFieldType.Int32:
                        destination.SetInt32(label, source.GetInt32(label));
                        break;
                    case GFFFieldType.UInt64:
                        destination.SetUInt64(label, source.GetUInt64(label));
                        break;
                    case GFFFieldType.Int64:
                        destination.SetInt64(label, source.GetInt64(label));
                        break;
                    case GFFFieldType.Single:
                        destination.SetSingle(label, source.GetSingle(label));
                        break;
                    case GFFFieldType.Double:
                        destination.SetDouble(label, source.GetDouble(label));
                        break;
                    case GFFFieldType.String:
                        destination.SetString(label, source.GetString(label) ?? "");
                        break;
                    case GFFFieldType.ResRef:
                        ResRef resRefValue = source.GetResRef(label);
                        if (resRefValue == null || resRefValue.IsBlank())
                        {
                            destination.SetResRef(label, ResRef.FromBlank());
                        }
                        else
                        {
                            destination.SetResRef(label, resRefValue);
                        }
                        break;
                    case GFFFieldType.LocalizedString:
                        destination.SetLocString(label, source.GetLocString(label));
                        break;
                    case GFFFieldType.Vector3:
                        destination.SetVector3(label, source.GetVector3(label));
                        break;
                    case GFFFieldType.Vector4:
                        destination.SetVector4(label, source.GetVector4(label));
                        break;
                    case GFFFieldType.Binary:
                        destination.SetBinary(label, source.GetBinary(label));
                        break;
                    case GFFFieldType.Struct:
                        // Recursively copy nested structs
                        GFFStruct nestedSource = source.GetStruct(label);
                        if (nestedSource != null)
                        {
                            var nestedDest = new GFFStruct();
                            CopyGffStructFields(nestedSource, nestedDest);
                            destination.SetStruct(label, nestedDest);
                        }
                        break;
                    case GFFFieldType.List:
                        // Copy lists (shallow copy - lists are reference types in GFF)
                        GFFList sourceList = source.GetList(label);
                        if (sourceList != null)
                        {
                            destination.SetList(label, sourceList);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Deserializes entity collection.
        /// </summary>
        /// <remarks>
        /// Based on [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x005fb0f0) - Recreates entities from save data.
        /// Recreates entities from save data.
        /// Restores all components and state information.
        /// Handles entity interdependencies.
        ///
        /// Implementation details:
        /// - Parses GFF with "Creature List" list ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007c0c80))
        /// - For each entity struct, creates OdysseyEntity and deserializes data
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x005fb0f0): 0x005fb0f0 loads entities from "Creature List" structure
        /// - Each list entry contains ObjectId and all entity component data
        /// - Note: Full deserialization requires OdysseyEntity.Deserialize() to be implemented
        /// </remarks>
        public override IEnumerable<IEntity> DeserializeEntities(byte[] entitiesData)
        {
            if (entitiesData == null || entitiesData.Length == 0)
            {
                yield break;
            }

            // Parse GFF structure
            GFF gff;
            try
            {
                gff = GFF.FromBytes(entitiesData);
            }
            catch (Exception)
            {
                // Invalid GFF data
                yield break;
            }

            GFFStruct root = gff.Root;

            // Get Creature List
            if (!root.Exists("Creature List"))
            {
                yield break;
            }

            GFFList creatureList = root.GetList("Creature List");
            if (creatureList == null)
            {
                yield break;
            }

            // Deserialize each entity
            foreach (GFFStruct entityStruct in creatureList)
            {
                if (entityStruct == null)
                {
                    continue;
                }

                // Read basic entity properties
                if (!entityStruct.Exists("ObjectId") || !entityStruct.Exists("ObjectType"))
                {
                    continue;
                }

                uint objectId = entityStruct.GetUInt32("ObjectId");
                int objectTypeInt = entityStruct.GetInt32("ObjectType");
                ObjectType objectType = (ObjectType)objectTypeInt;
                string tag = entityStruct.Exists("Tag") ? (entityStruct.GetString("Tag") ?? "") : null;

                // Create entity with ObjectId, ObjectType, and Tag
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 creates entities from save data
                // Entity creation: ObjectId, ObjectType, Tag are required for entity creation
                // Located via string references:
                // - "ObjectId"   ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007bce5c))
                // - "ObjectType" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007bd00c))
                // - "Tag"        ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007bd00c))
                OdysseyEntity entity = new OdysseyEntity(objectId, objectType, tag);

                // Initialize all components based on ObjectType
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Component initialization occurs during entity creation
                // ComponentInitializer ensures all required components are attached:
                // - TransformComponent (all entities)
                // - RenderableComponent (creatures, doors, placeables, items)
                // - AnimationComponent (creatures, doors, placeables)
                // - Type-specific components (CreatureComponent, DoorComponent, etc.)
                // - ScriptHooksComponent (all entities)
                // - ActionQueueComponent (creatures, doors, placeables)
                // This matches the component initialization pattern used in EntityFactory and ModuleLoader
                try
                {
                    Runtime.Core.Entities.ComponentInitializer.InitializeComponents(entity);
                }
                catch (Exception ex)
                {
                    // Log component initialization error but continue - entity may still be partially functional
                    System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to initialize components for entity {objectId} ({objectType}): {ex.Message}");
                }

                // Restore AreaId if present
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AreaId links entity to area for area-specific operations
                // Located via string references: "AreaId" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007bef48))
                if (entityStruct.Exists("AreaId"))
                {
                    entity.AreaId = entityStruct.GetUInt32("AreaId");
                }

                // Convert entity struct to GFF bytes for deserialization
                // Create a new GFF with this struct as root
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entity data is stored as GFF structure within "Creature List"
                var entityGff = new GFF();
                CopyGffStructFields(entityStruct, entityGff.Root);
                byte[] entityData = entityGff.ToBytes();

                // Deserialize entity to restore all component state
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005fb0f0 @ 0x005fb0f0 deserializes entity component data
                // OdysseyEntity.Deserialize() restores:
                // - TransformComponent (position, facing)
                // - StatsComponent (HP, abilities, skills, saves, known spells)
                // - InventoryComponent (equipped items, inventory contents)
                // - DoorComponent (open/closed, locked, hitpoints, etc.)
                // - PlaceableComponent (open/closed, locked, inventory, etc.)
                // - ScriptHooksComponent (script hooks and local variables)
                // - All other component state
                try
                {
                    entity.Deserialize(entityData);
                }
                catch (NotImplementedException)
                {
                    // Deserialize not yet fully implemented - restore basic properties manually
                    // This fallback ensures entities can still be loaded even if Deserialize() is incomplete
                    // Restore basic transform if available
                    if (entityStruct.Exists("X") && entityStruct.Exists("Y") && entityStruct.Exists("Z"))
                    {
                        var transformComponent = entity.GetComponent<ITransformComponent>();
                        if (transformComponent != null)
                        {
                            float x = entityStruct.GetSingle("X");
                            float y = entityStruct.GetSingle("Y");
                            float z = entityStruct.GetSingle("Z");
                            float facing = entityStruct.Exists("Facing") ? entityStruct.GetSingle("Facing") : 0.0f;
                            transformComponent.Position = new System.Numerics.Vector3(x, y, z);
                            transformComponent.Facing = facing;
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log deserialization error but continue - entity has components initialized
                    // Caller can still register entity with world, though state may be incomplete
                    System.Diagnostics.Debug.WriteLine($"[OdysseySaveSerializer] Failed to deserialize entity {objectId} ({objectType}): {ex.Message}");
                }

                // Entity is now fully initialized with all components and ready for world registration
                // Caller should register entity with world using World.RegisterEntity(entity)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities must be registered with world for lookups and updates
                // Registration occurs after deserialization to ensure entity is complete

                yield return entity;
            }
        }

        /// <summary>
        /// Creates a save game directory structure.
        /// </summary>
        /// <remarks>
        /// Creates numbered save directories following KotOR conventions.
        /// Creates NFO, SAV, and supporting files.
        /// Includes screenshot and metadata files.
        ///
        /// Directory structure:
        /// - Save game root directory
        /// - Numbered save subdirectories (format: "%06d - %s" e.g., "000001 - MySave")
        /// - savenfo.res: Metadata file (GFF with "NFO " signature)
        /// - savegame.sav: Main save data (ERF archive with "MOD V1.0" signature)
        /// - screen.tga: Screenshot (TGA format)
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004eb750): Creates save directory with format "%06d - %s" (save number and name)
        /// Located via string references: "savenfo" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be1f0)), "SAVEGAME" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be28c)), "SAVES:" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be284))
        /// Original implementation: Creates save directory with format "%06d - %s" (save number and name)
        /// Path format: "SAVES:\{saveNumber:06d} - {saveName}\"
        /// </remarks>
        public override void CreateSaveDirectory(string saveName, Common.SaveGameData saveData)
        {
            if (string.IsNullOrEmpty(saveName))
            {
                throw new ArgumentException("Save name cannot be null or empty", nameof(saveName));
            }

            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            // Get saves directory - use constructor parameter or throw exception
            string savesDirectory = _savesDirectory;
            if (string.IsNullOrEmpty(savesDirectory))
            {
                // Try to get from environment or use default
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Save directory is typically "SAVES:" or user's Documents folder
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                savesDirectory = Path.Combine(documentsPath, "SWKotOR", "saves");

                // If still not set, throw exception
                if (string.IsNullOrEmpty(savesDirectory))
                {
                    throw new InvalidOperationException("Saves directory not specified. Provide saves directory to OdysseySaveSerializer constructor.");
                }
            }

            // Ensure saves directory exists
            if (!Directory.Exists(savesDirectory))
            {
                Directory.CreateDirectory(savesDirectory);
            }

            // Get save number from saveData if available, otherwise get next available
            int saveNumber = 0;
            try
            {
                // Try to get SaveNumber property via reflection (SaveGameData from Common namespace may not have it)
                var saveNumberProperty = saveData.GetType().GetProperty("SaveNumber");
                if (saveNumberProperty != null)
                {
                    object saveNumberObj = saveNumberProperty.GetValue(saveData);
                    if (saveNumberObj != null && saveNumberObj is int)
                    {
                        saveNumber = (int)saveNumberObj;
                    }
                }
            }
            catch
            {
                // Property doesn't exist or can't be accessed - will use 0 or get next available
            }

            // If save number is 0 or negative, get next available save number
            if (saveNumber <= 0)
            {
                saveNumber = GetNextSaveNumber(savesDirectory);
            }

            // Format save directory name using common format: "%06d - %s"
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x007be298) - Format string "SAVES:%06d - %s"
            // Located via string reference: "%06d - %s" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be298))
            string formattedSaveName = string.Format("{0:D6} - {1}", saveNumber, saveName);
            string saveDir = Path.Combine(savesDirectory, formattedSaveName);

            // Create save directory if it doesn't exist
            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            // Write NFO file (save metadata)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004eb750): 0x004eb750 @ 0x004eb750 creates NFO GFF
            // Located via string reference: "savenfo" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be1f0))
            byte[] nfoData = SerializeSaveNfo(saveData);
            if (nfoData != null && nfoData.Length > 0)
            {
                string nfoPath = Path.Combine(saveDir, "savenfo.res");
                File.WriteAllBytes(nfoPath, nfoData);
            }

            // Write SAV file (ERF archive with save data)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004eb750): 0x004eb750 @ 0x004eb750 creates ERF archive
            // Located via string reference: "SAVEGAME" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be28c)), "MOD V1.0" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be0d4))
            // Original implementation: Creates ERF archive with "MOD V1.0" signature
            byte[] savData = SerializeSaveArchive(saveData);
            if (savData != null && savData.Length > 0)
            {
                string savPath = Path.Combine(saveDir, "savegame.sav");
                File.WriteAllBytes(savPath, savData);
            }

            // Write screenshot if available
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004eb750): Screenshot saved as screen.tga
            if (saveData.Screenshot != null && saveData.Screenshot.Length > 0)
            {
                string screenshotPath = Path.Combine(saveDir, "screen.tga");
                File.WriteAllBytes(screenshotPath, saveData.Screenshot);
            }
        }

        /// <summary>
        /// Serializes save game data to ERF archive format.
        /// </summary>
        /// <remarks>
        /// Creates ERF archive with "MOD V1.0" signature containing:
        /// - savenfo.res: Save metadata (GFF with "NFO " signature)
        /// - GLOBALVARS.res: Global variable state (GFF with "GLOB" signature)
        /// - PARTYTABLE.res: Party state (GFF with "PT  " signature)
        /// - [module]_s.rim: Per-module state ERF archive (area states, entity positions, etc.)
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004eb750)
        /// Located via string reference: "MOD V1.0" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be0d4))
        /// Original implementation: Creates ERF archive with "MOD V1.0" signature
        /// </remarks>
        private byte[] SerializeSaveArchive(Common.SaveGameData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            // Create ERF archive with MOD type (used for save files)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ERF with "MOD V1.0" signature @ 0x007be0d4
            var erf = new ERF(ERFType.MOD, isSave: true);

            // Add savenfo.res (save metadata) - already serialized as NFO GFF
            byte[] nfoData = SerializeSaveNfo(saveData);
            if (nfoData != null && nfoData.Length > 0)
            {
                erf.SetData("savenfo", ResourceType.GFF, nfoData);
            }

            // Add GLOBALVARS.res (global variable state)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005ac670 @ 0x005ac670 saves global variables
            if (saveData.GameState != null)
            {
                try
                {
                    byte[] globalVarsData = SerializeGlobals(GameState.FromInterface(saveData.GameState));
                    if (globalVarsData != null && globalVarsData.Length > 0)
                    {
                        erf.SetData("GLOBALVARS", ResourceType.GFF, globalVarsData);
                    }
                }
                catch (NotImplementedException)
                {
                    // Global serialization not yet implemented - skip GLOBALVARS.res
                    // Save will still be created but without global variables
                }
            }

            // Add PARTYTABLE.res (party state)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x0057bd70): 0x0057bd70 @ 0x0057bd70 saves party state
            // Located via string reference: "PARTYTABLE" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007c1910))
            if (saveData.PartyState != null)
            {
                byte[] partyData = SerializeParty(PartyState.FromInterface(saveData.PartyState));
                if (partyData != null && partyData.Length > 0)
                {
                    erf.SetData("PARTYTABLE", ResourceType.GFF, partyData);
                }
            }

            // Add module state files ([module]_s.rim)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state saved as ERF archive containing area state GFF files
            // Original implementation: Each visited area has its state saved (entity positions, door/placeable states, etc.)
            if (saveData.CurrentAreaInstance != null)
            {
                // Try to get module name from CurrentAreaInstance or CurrentArea
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state lookup priority
                // Original implementation: Uses CurrentModule if available, then tries area instance, then infers from area name
                // Located via string reference: "Mod_Area_list" @ 0x007be748 (swkotor2.exe)
                string moduleName = null;

                // Priority 1: Try to get module name from area instance via reflection
                // CurrentModule property not available on Common.SaveGameData
                {
                    try
                    {
                        // Try to get module name from area instance
                        var moduleProperty = saveData.CurrentAreaInstance.GetType().GetProperty("Module");
                        if (moduleProperty != null)
                        {
                            object moduleObj = moduleProperty.GetValue(saveData.CurrentAreaInstance);
                            if (moduleObj != null)
                            {
                                var resRefProperty = moduleObj.GetType().GetProperty("ResRef");
                                if (resRefProperty != null)
                                {
                                    object resRefObj = resRefProperty.GetValue(moduleObj);
                                    if (resRefObj != null)
                                    {
                                        moduleName = resRefObj.ToString();
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Property access failed - try alternative methods
                    }
                }

                // Priority 2: Try to infer from CurrentAreaInstance ResRef using ModuleAreaMappings
                if (string.IsNullOrEmpty(moduleName) && saveData.CurrentAreaInstance != null && saveData.ModuleAreaMappings != null && saveData.ModuleAreaMappings.Count > 0)
                {
                    try
                    {
                        // Get area ResRef from CurrentAreaInstance
                        string areaResRef = saveData.CurrentAreaInstance.ResRef;
                        if (!string.IsNullOrEmpty(areaResRef))
                        {
                            // Search ModuleAreaMappings to find which module contains this area
                            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state lookup by area name
                            // Original implementation: Searches ModuleAreaMappings to find which module contains the current area
                            // Located via string reference: "Mod_Area_list" @ 0x007be748 (swkotor2.exe)
                            // Module IFO file contains Mod_Area_list (GFF List) with Area_Name fields for each area
                            foreach (var kvp in saveData.ModuleAreaMappings)
                            {
                                string moduleResRef = kvp.Key;
                                List<string> areaList = kvp.Value;
                                if (areaList != null && areaList.Contains(areaResRef, StringComparer.OrdinalIgnoreCase))
                                {
                                    moduleName = moduleResRef;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Property access failed - try alternative methods
                    }
                }

                // Priority 3: If module name not found, try to infer from CurrentArea string using ModuleAreaMappings
                if (string.IsNullOrEmpty(moduleName) && !string.IsNullOrEmpty(saveData.CurrentArea) && saveData.ModuleAreaMappings != null && saveData.ModuleAreaMappings.Count > 0)
                {
                    // Module name might be derivable from area name
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module state lookup by area name
                    // Original implementation: Searches ModuleAreaMappings to find which module contains the current area
                    // Located via string reference: "Mod_Area_list" @ 0x007be748 (swkotor2.exe)
                    // Module IFO file contains Mod_Area_list (GFF List) with Area_Name fields for each area
                    foreach (var kvp in saveData.ModuleAreaMappings)
                    {
                        string moduleResRef = kvp.Key;
                        List<string> areaList = kvp.Value;
                        if (areaList != null && areaList.Contains(saveData.CurrentArea, StringComparer.OrdinalIgnoreCase))
                        {
                            moduleName = moduleResRef;
                            break;
                        }
                    }
                }

                // Serialize area state if we have module name
                if (!string.IsNullOrEmpty(moduleName))
                {
                    try
                    {
                        byte[] areaData = SerializeArea(saveData.CurrentAreaInstance);
                        if (areaData != null && areaData.Length > 0)
                        {
                            // Module state is stored as RIM format in save ERF
                            string moduleRimName = moduleName + "_s";
                            erf.SetData(moduleRimName, ResourceType.RIM, areaData);
                        }
                    }
                    catch (NotImplementedException)
                    {
                        // Area serialization not yet implemented - skip module state
                        // Save will still be created but without area state
                    }
                }
            }

            // Serialize ERF to bytes
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ERF archive written to disk
            var writer = new ERFBinaryWriter(erf);
            return writer.Write();
        }

        /// <summary>
        /// Gets the next available save number by scanning existing save directories.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Save numbers are auto-incremented
        /// Original implementation: Scans existing saves to find highest number, then increments
        /// </remarks>
        private int GetNextSaveNumber(string savesDirectory)
        {
            if (string.IsNullOrEmpty(savesDirectory) || !Directory.Exists(savesDirectory))
            {
                return 1; // Start from 1 if directory doesn't exist
            }

            int maxSaveNumber = 0;

            // Scan existing save directories to find highest save number
            // Format: "%06d - %s" (e.g., "000001 - MySave")
            foreach (string dir in Directory.GetDirectories(savesDirectory))
            {
                string dirName = Path.GetFileName(dir);

                // Parse save number from directory name
                // Format: "000001 - SaveName" -> extract "000001" and convert to int
                if (dirName.Length >= 6 && dirName[6] == ' ')
                {
                    string numberPart = dirName.Substring(0, 6);
                    if (int.TryParse(numberPart, out int saveNumber))
                    {
                        if (saveNumber > maxSaveNumber)
                        {
                            maxSaveNumber = saveNumber;
                        }
                    }
                }
            }

            return maxSaveNumber + 1;
        }

        /// <summary>
        /// Validates save game compatibility.
        /// </summary>
        /// <remarks>
        /// Checks KotOR save compatibility.
        /// Validates NFO signature and version.
        /// Checks for required save files.
        /// Returns compatibility status with details.
        ///
        /// Based on reverse engineering of:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x00707290): 0x00707290 @ 0x00707290 loads save and checks for corruption
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x00708990): 0x00708990 @ 0x00708990 validates save structure during load
        /// - Located via string references: "savenfo" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be1f0)), "SAVEGAME" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x007be28c)), "CORRUPT" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x00707602))
        ///
        /// Compatibility checks performed:
        /// 1. Save path existence and directory structure
        /// 2. savegame.sav ERF archive existence and validity
        /// 3. Required resources (savenfo, GLOBALVARS, PARTYTABLE)
        /// 4. NFO GFF signature ("NFO ") and version ("V1.0" for K1, "V2.0" for K2)
        /// 5. Save version compatibility (K1 vs K2)
        /// 6. Corruption markers (CORRUPT file presence)
        /// 7. GFF structure integrity
        /// 8. ERF archive structure integrity
        /// </remarks>
        public override SaveCompatibility CheckCompatibility(string savePath)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                return SaveCompatibility.Incompatible;
            }

            // 1. Check if save path exists and is a directory
            if (!System.IO.Directory.Exists(savePath))
            {
                return SaveCompatibility.Incompatible;
            }

            // 2. Check for corruption marker (CORRUPT file)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x00707290): 0x00707290 @ 0x00707290 checks for "CORRUPT" file
            // Located via string reference: "CORRUPT" ([TODO: Data address] @ (K1: TODO: Find this address, TSL: 0x00707602))
            string corruptPath = System.IO.Path.Combine(savePath, "CORRUPT");
            if (System.IO.File.Exists(corruptPath))
            {
                // Save is marked as corrupted
                return SaveCompatibility.Incompatible;
            }

            // 3. Check if savegame.sav exists
            string savegamePath = System.IO.Path.Combine(savePath, "savegame.sav");
            if (!System.IO.File.Exists(savegamePath))
            {
                // Also check for SAVEGAME.sav (uppercase variant)
                savegamePath = System.IO.Path.Combine(savePath, "SAVEGAME.sav");
                if (!System.IO.File.Exists(savegamePath))
                {
                    return SaveCompatibility.Incompatible;
                }
            }

            try
            {
                // 4. Validate ERF archive structure
                byte[] erfData = System.IO.File.ReadAllBytes(savegamePath);
                if (erfData == null || erfData.Length < 160) // Minimum ERF header size
                {
                    return SaveCompatibility.Incompatible;
                }

                ERF erf;
                try
                {
                    erf = ERFAuto.ReadErf(erfData);
                }
                catch (Exception)
                {
                    // Invalid ERF structure
                    return SaveCompatibility.Incompatible;
                }

                if (erf == null || erf.Count == 0)
                {
                    return SaveCompatibility.Incompatible;
                }

                // 5. Check for required resources
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Required resources are savenfo, GLOBALVARS, PARTYTABLE
                byte[] nfoData = erf.Get("savenfo", ResourceType.GFF);
                if (nfoData == null || nfoData.Length == 0)
                {
                    return SaveCompatibility.Incompatible;
                }

                byte[] globalsData = erf.Get("GLOBALVARS", ResourceType.GFF);
                if (globalsData == null || globalsData.Length == 0)
                {
                    // GLOBALVARS is required for save compatibility
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                byte[] partyData = erf.Get("PARTYTABLE", ResourceType.GFF);
                if (partyData == null || partyData.Length == 0)
                {
                    // PARTYTABLE is required for save compatibility
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // 6. Validate NFO GFF signature and version
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): NFO GFF must have "NFO " signature
                // Version should be "V2.0" for KotOR 2, "V1.0" for KotOR 1
                GFF nfoGff;
                try
                {
                    nfoGff = GFF.FromBytes(nfoData);
                }
                catch (Exception)
                {
                    // Invalid GFF structure
                    return SaveCompatibility.Incompatible;
                }

                if (nfoGff == null)
                {
                    return SaveCompatibility.Incompatible;
                }

                // Check NFO signature
                if (nfoGff.Content != GFFContent.NFO)
                {
                    // NFO GFF must have NFO content type
                    return SaveCompatibility.Incompatible;
                }

                // 7. Check save version compatibility
                // KotOR 1 uses version 1, KotOR 2 uses version 2
                // Current implementation is for KotOR 2 (SaveVersion = 2)
                // We need to detect the save version from the NFO file
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Version is stored in GFF header, but we can infer from structure
                // K2 saves have additional fields like PCNAME, INFLUENCE, etc.
                bool hasK2Fields = nfoGff.Root.Exists("PCNAME");
                int detectedVersion = hasK2Fields ? 2 : 1;

                // Check version compatibility
                if (detectedVersion != SaveVersion)
                {
                    // Version mismatch - K1 save in K2 engine or vice versa
                    if (detectedVersion == 1 && SaveVersion == 2)
                    {
                        // K1 save in K2 engine - may require migration
                        return SaveCompatibility.RequiresMigration;
                    }
                    else if (detectedVersion == 2 && SaveVersion == 1)
                    {
                        // K2 save in K1 engine - incompatible
                        return SaveCompatibility.Incompatible;
                    }
                }

                // 8. Validate GFF structures integrity
                // Check that required NFO fields exist
                if (!nfoGff.Root.Exists("SAVEGAMENAME"))
                {
                    // Missing required field
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // Validate GLOBALVARS GFF structure
                try
                {
                    GFF globalsGff = GFF.FromBytes(globalsData);
                    if (globalsGff == null || globalsGff.Root == null)
                    {
                        return SaveCompatibility.CompatibleWithWarnings;
                    }
                }
                catch (Exception)
                {
                    // Invalid GLOBALVARS GFF structure
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // Validate PARTYTABLE GFF structure
                try
                {
                    GFF partyGff = GFF.FromBytes(partyData);
                    if (partyGff == null || partyGff.Root == null)
                    {
                        return SaveCompatibility.CompatibleWithWarnings;
                    }

                    // Check PARTYTABLE signature
                    if (partyGff.Content != GFFContent.PT)
                    {
                        // PARTYTABLE GFF must have PT content type
                        return SaveCompatibility.CompatibleWithWarnings;
                    }
                }
                catch (Exception)
                {
                    // Invalid PARTYTABLE GFF structure
                    return SaveCompatibility.CompatibleWithWarnings;
                }

                // 9. Check for nested module corruption (EventQueue in module.ifo)
                // Based on HTInstallation.IsSaveCorrupted implementation
                // Check each .sav resource (cached modules) for EventQueue corruption
                bool hasCorruptedModule = false;
                foreach (var resource in erf)
                {
                    if (resource.ResType != ResourceType.SAV)
                    {
                        continue;
                    }

                    try
                    {
                        // Read the nested module ERF
                        ERF innerErf = ERFAuto.ReadErf(resource.Data);
                        if (innerErf == null)
                        {
                            continue;
                        }

                        // Look for module.ifo in this cached module
                        byte[] moduleIfoData = innerErf.Get("module", ResourceType.IFO);
                        if (moduleIfoData != null && moduleIfoData.Length > 0)
                        {
                            try
                            {
                                GFF moduleIfo = GFF.FromBytes(moduleIfoData);
                                if (moduleIfo != null && moduleIfo.Root != null && moduleIfo.Root.Exists("EventQueue"))
                                {
                                    var eventQueue = moduleIfo.Root.GetList("EventQueue");
                                    if (eventQueue != null && eventQueue.Count > 0)
                                    {
                                        // EventQueue should be empty in saved modules - corruption detected
                                        hasCorruptedModule = true;
                                        break;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                // Skip malformed module.ifo
                                continue;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Skip malformed nested ERFs
                        continue;
                    }
                }

                if (hasCorruptedModule)
                {
                    return SaveCompatibility.Incompatible;
                }

                // All checks passed - save is compatible
                return SaveCompatibility.Compatible;
            }
            catch (System.IO.IOException)
            {
                // File access error
                return SaveCompatibility.Incompatible;
            }
            catch (Exception)
            {
                // Unexpected error during validation
                return SaveCompatibility.Incompatible;
            }
        }
    }
}
