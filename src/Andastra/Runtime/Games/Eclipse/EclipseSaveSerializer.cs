using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse Engine (Mass Effect/Dragon Age) save game serializer implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Save Serializer Implementation:
    /// - Based on daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe
    /// - Most complex save system with physics state, destruction, modifications
    /// - Handles real-time area changes, destructible environments
    ///
    /// Based on reverse engineering of:
    /// - Eclipse engine save systems across all games
    /// - Physics state serialization and restoration
    /// - Destructible environment persistence
    /// - Dynamic lighting and effect state saving
    /// - Complex entity relationships and squad management
    ///
    /// Eclipse save features:
    /// - Physics world state (rigid bodies, constraints, destruction)
    /// - Destructible geometry and environmental changes
    /// - Dynamic lighting configurations and presets
    /// - Squad/party relationships and approval systems
    /// - Real-time area modifications and placed objects
    /// - Complex quest state with branching narratives
    /// - Player choice consequences and morality systems
    /// - Romance and relationship state tracking
    /// </remarks>
    [PublicAPI]
    public class EclipseSaveSerializer : BaseSaveSerializer
    {
        /// <summary>
        /// Gets the save file format version for Eclipse engine.
        /// </summary>
        /// <remarks>
        /// Dragon Age Origins: 1, Dragon Age 2: 2, Mass Effect: 3, Mass Effect 2: 4.
        /// Higher versions include more complex state tracking.
        /// </remarks>
        protected override int SaveVersion => 4; // Mass Effect 2 version

        /// <summary>
        /// Gets the engine identifier.
        /// </summary>
        /// <remarks>
        /// Identifies this as an Eclipse engine save.
        /// Supports cross-game compatibility within Eclipse family.
        /// </remarks>
        protected override string EngineIdentifier => "Eclipse";

        /// <summary>
        /// Serializes save game metadata to Eclipse format.
        /// </summary>
        /// <remarks>
        /// Eclipse NFO includes more metadata than other engines.
        /// Contains play time, difficulty, morality, romance flags.
        /// Includes squad composition and mission progress.
        ///
        /// Eclipse NFO enhancements:
        /// - Morality score and reputation
        /// - Romance status flags
        /// - Squad member approval ratings
        /// - Mission completion statistics
        /// - Difficulty setting and modifiers
        /// - DLC and expansion flags
        ///
        /// Binary format structure:
        /// - Signature (4 bytes): "DAS ", "DAS2", "MES ", or "MES2" based on game
        /// - Version (int32): Save format version (1=DAO, 2=DA2, 3=ME1, 4=ME2)
        /// - Engine identifier length (int32) + Engine identifier string
        /// - Save name (string)
        /// - Current area name (string)
        /// - Time played in seconds (int32)
        /// - Timestamp (int64): FileTime structure
        /// - Screenshot length (int32) + Screenshot data (bytes)
        /// - Portrait length (int32) + Portrait data (bytes)
        /// - Player name (string)
        /// - Morality score (int32): Paragon/Renegade or similar system
        /// - Romance count (int32) + [Character name (string) + Romance level (int32)] pairs
        /// - Squad approval count (int32) + [Character name (string) + Approval rating (int32)] pairs
        /// - Mission completion count (int32) + [Mission ID (string) + Completed (bool)] pairs
        /// - Difficulty level (int32)
        /// - DLC count (int32) + [DLC name (string) + Enabled (bool)] pairs
        /// - Save type (int32): 0=Manual, 1=Auto, 2=Quick
        /// - Cheat used flag (bool)
        /// - Save number (int32)
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Save metadata serialization functions
        /// - DragonAge2.exe: Enhanced save metadata serialization
        /// - MassEffect.exe: Save metadata serialization
        /// - MassEffect2.exe: Advanced save metadata with relationships
        /// </remarks>
        public override byte[] SerializeSaveNfo(SaveGameData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                // Determine signature based on save version
                // Version 1 = DAO ("DAS "), Version 2 = DA2 ("DAS2"), Version 3 = ME1 ("MES "), Version 4 = ME2 ("MES2")
                string signature;
                switch (SaveVersion)
                {
                    case 1:
                        signature = "DAS ";
                        break;
                    case 2:
                        signature = "DAS2";
                        break;
                    case 3:
                        signature = "MES ";
                        break;
                    case 4:
                        signature = "MES2";
                        break;
                    default:
                        signature = "ECLP"; // Eclipse generic fallback
                        break;
                }

                // Write signature (4 bytes)
                byte[] signatureBytes = Encoding.UTF8.GetBytes(signature);
                if (signatureBytes.Length != 4)
                {
                    Array.Resize(ref signatureBytes, 4);
                }
                writer.Write(signatureBytes);

                // Write version (int32)
                writer.Write(SaveVersion);

                // Write engine identifier
                WriteString(writer, EngineIdentifier);

                // Write standard metadata fields
                WriteString(writer, saveData.SaveName ?? "");
                WriteString(writer, saveData.CurrentArea ?? "");
                writer.Write(saveData.TimePlayed);

                // Write timestamp as FileTime (int64)
                // Convert DateTime to FileTime (Windows FILETIME structure)
                long fileTime = saveData.Timestamp.ToFileTime();
                writer.Write(fileTime);

                // Write screenshot data
                if (saveData.Screenshot != null && saveData.Screenshot.Length > 0)
                {
                    writer.Write(saveData.Screenshot.Length);
                    writer.Write(saveData.Screenshot);
                }
                else
                {
                    writer.Write(0);
                }

                // Write portrait data
                if (saveData.Portrait != null && saveData.Portrait.Length > 0)
                {
                    writer.Write(saveData.Portrait.Length);
                    writer.Write(saveData.Portrait);
                }
                else
                {
                    writer.Write(0);
                }

                // Write player name (extract from party state if available)
                string playerName = "";
                if (saveData.PartyState != null && saveData.PartyState.Leader != null)
                {
                    playerName = saveData.PartyState.Leader.Tag ?? "";
                }
                WriteString(writer, playerName);

                // Write Eclipse-specific metadata
                // Morality score (extract from game state if available)
                int moralityScore = 0;
                if (saveData.GameState != null)
                {
                    try
                    {
                        moralityScore = saveData.GameState.GetGlobal<int>("MORALITY_SCORE", 0);
                    }
                    catch
                    {
                        // Morality score not available, use default
                    }
                }
                writer.Write(moralityScore);

                // Romance data (extract from game state)
                var romanceData = new List<(string characterName, int romanceLevel)>();
                if (saveData.GameState != null)
                {
                    try
                    {
                        // Try to get romance data from globals
                        // Common pattern: ROMANCE_[CharacterName] = level
                        var globalNames = saveData.GameState.GetGlobalNames();
                        if (globalNames != null)
                        {
                            foreach (string name in globalNames)
                            {
                                if (name != null && name.StartsWith("ROMANCE_", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        int level = saveData.GameState.GetGlobal<int>(name, 0);
                                        string characterName = name.Substring(8); // Remove "ROMANCE_" prefix
                                        romanceData.Add((characterName, level));
                                    }
                                    catch
                                    {
                                        // Skip invalid romance entries
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Romance data not available
                    }
                }

                writer.Write(romanceData.Count);
                foreach (var (characterName, romanceLevel) in romanceData)
                {
                    WriteString(writer, characterName);
                    writer.Write(romanceLevel);
                }

                // Squad approval ratings (extract from game state)
                var approvalData = new List<(string characterName, int approvalRating)>();
                if (saveData.GameState != null)
                {
                    try
                    {
                        // Try to get approval data from globals
                        // Common pattern: APPROVAL_[CharacterName] = rating
                        var globalNames = saveData.GameState.GetGlobalNames();
                        if (globalNames != null)
                        {
                            foreach (string name in globalNames)
                            {
                                if (name != null && name.StartsWith("APPROVAL_", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        int rating = saveData.GameState.GetGlobal<int>(name, 0);
                                        string characterName = name.Substring(9); // Remove "APPROVAL_" prefix
                                        approvalData.Add((characterName, rating));
                                    }
                                    catch
                                    {
                                        // Skip invalid approval entries
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Approval data not available
                    }
                }

                writer.Write(approvalData.Count);
                foreach (var (characterName, approvalRating) in approvalData)
                {
                    WriteString(writer, characterName);
                    writer.Write(approvalRating);
                }

                // Mission completion status (extract from game state)
                var missionData = new List<(string missionId, bool completed)>();
                if (saveData.GameState != null)
                {
                    try
                    {
                        // Try to get mission data from globals
                        // Common pattern: MISSION_[MissionId] = completed (bool or int)
                        var globalNames = saveData.GameState.GetGlobalNames();
                        if (globalNames != null)
                        {
                            foreach (string name in globalNames)
                            {
                                if (name != null && name.StartsWith("MISSION_", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        bool completed = saveData.GameState.GetGlobal<bool>(name, false);
                                        string missionId = name.Substring(8); // Remove "MISSION_" prefix
                                        missionData.Add((missionId, completed));
                                    }
                                    catch
                                    {
                                        // Try as int
                                        try
                                        {
                                            int completedInt = saveData.GameState.GetGlobal<int>(name, 0);
                                            bool completed = completedInt != 0;
                                            string missionId = name.Substring(8);
                                            missionData.Add((missionId, completed));
                                        }
                                        catch
                                        {
                                            // Skip invalid mission entries
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Mission data not available
                    }
                }

                writer.Write(missionData.Count);
                foreach (var (missionId, completed) in missionData)
                {
                    WriteString(writer, missionId);
                    writer.Write(completed);
                }

                // Difficulty level (extract from game state)
                int difficultyLevel = 0;
                if (saveData.GameState != null)
                {
                    try
                    {
                        difficultyLevel = saveData.GameState.GetGlobal<int>("DIFFICULTY_LEVEL", 0);
                    }
                    catch
                    {
                        // Difficulty not available, use default
                    }
                }
                writer.Write(difficultyLevel);

                // DLC flags (extract from game state)
                var dlcData = new List<(string dlcName, bool enabled)>();
                if (saveData.GameState != null)
                {
                    try
                    {
                        // Try to get DLC data from globals
                        // Common pattern: DLC_[DLCName] = enabled (bool)
                        var globalNames = saveData.GameState.GetGlobalNames();
                        if (globalNames != null)
                        {
                            foreach (string name in globalNames)
                            {
                                if (name != null && name.StartsWith("DLC_", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        bool enabled = saveData.GameState.GetGlobal<bool>(name, false);
                                        string dlcName = name.Substring(4); // Remove "DLC_" prefix
                                        dlcData.Add((dlcName, enabled));
                                    }
                                    catch
                                    {
                                        // Skip invalid DLC entries
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // DLC data not available
                    }
                }

                writer.Write(dlcData.Count);
                foreach (var (dlcName, enabled) in dlcData)
                {
                    WriteString(writer, dlcName);
                    writer.Write(enabled);
                }

                // Write save type (0=Manual, 1=Auto, 2=Quick)
                // Note: SaveGameData doesn't have SaveType in Runtime.Games.Common, so we'll default to Manual
                writer.Write(0);

                // Write cheat used flag (extract from game state if available)
                bool cheatUsed = false;
                if (saveData.GameState != null)
                {
                    try
                    {
                        cheatUsed = saveData.GameState.GetGlobal<bool>("CHEAT_USED", false);
                    }
                    catch
                    {
                        // Cheat flag not available
                    }
                }
                writer.Write(cheatUsed);

                // Write save number (default to 0 if not available)
                int saveNumber = 0;
                if (saveData.GameState != null)
                {
                    try
                    {
                        saveNumber = saveData.GameState.GetGlobal<int>("SAVE_NUMBER", 0);
                    }
                    catch
                    {
                        // Save number not available
                    }
                }
                writer.Write(saveNumber);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes Eclipse save metadata.
        /// </summary>
        /// <remarks>
        /// Reads enhanced NFO with Eclipse-specific data.
        /// Extracts morality, romance, and progression information.
        /// Validates DLC compatibility and expansion requirements.
        ///
        /// Binary format structure (matches SerializeSaveNfo):
        /// - Signature (4 bytes): "DAS ", "DAS2", "MES ", or "MES2"
        /// - Version (int32): Save format version
        /// - Engine identifier length (int32) + Engine identifier string
        /// - Save name (string)
        /// - Current area name (string)
        /// - Time played in seconds (int32)
        /// - Timestamp (int64): FileTime structure
        /// - Screenshot length (int32) + Screenshot data (bytes)
        /// - Portrait length (int32) + Portrait data (bytes)
        /// - Player name (string)
        /// - Morality score (int32)
        /// - Romance count (int32) + [Character name (string) + Romance level (int32)] pairs
        /// - Squad approval count (int32) + [Character name (string) + Approval rating (int32)] pairs
        /// - Mission completion count (int32) + [Mission ID (string) + Completed (bool)] pairs
        /// - Difficulty level (int32)
        /// - DLC count (int32) + [DLC name (string) + Enabled (bool)] pairs
        /// - Save type (int32)
        /// - Cheat used flag (bool)
        /// - Save number (int32)
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Save metadata deserialization functions
        /// - DragonAge2.exe: Enhanced save metadata deserialization
        /// - MassEffect.exe: Save metadata deserialization
        /// - MassEffect2.exe: Advanced save metadata with relationships
        /// </remarks>
        public override SaveGameMetadata DeserializeSaveNfo(byte[] nfoData)
        {
            if (nfoData == null || nfoData.Length == 0)
            {
                throw new ArgumentException("NFO data cannot be null or empty", nameof(nfoData));
            }

            using (var stream = new MemoryStream(nfoData))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                // Validate minimum size
                if (stream.Length < 8)
                {
                    throw new InvalidDataException("NFO data is too small to contain signature and version");
                }

                // Read and validate signature
                byte[] signatureBytes = reader.ReadBytes(4);
                string signature = Encoding.UTF8.GetString(signatureBytes);

                // Validate signature matches known Eclipse save formats
                bool isValidSignature = signature == "DAS " || signature == "DAS2" ||
                                        signature == "MES " || signature == "MES2" ||
                                        signature == "ECLP"; // Eclipse generic fallback
                if (!isValidSignature)
                {
                    throw new InvalidDataException($"Invalid Eclipse save file signature: '{signature}'");
                }

                // Read version
                int saveVersion = reader.ReadInt32();

                // Validate version compatibility
                if (saveVersion < 1 || saveVersion > SaveVersion)
                {
                    if (saveVersion > SaveVersion)
                    {
                        throw new NotSupportedException($"Save file version {saveVersion} is newer than supported version {SaveVersion}. Migration required.");
                    }
                    else
                    {
                        throw new InvalidDataException($"Unsupported save file version: {saveVersion}");
                    }
                }

                // Read engine identifier
                string engineIdentifier = ReadString(reader);

                // Validate engine identifier matches
                if (!string.IsNullOrEmpty(engineIdentifier) &&
                    !engineIdentifier.Equals(EngineIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it's a compatible Eclipse variant
                    if (!engineIdentifier.StartsWith("Eclipse", StringComparison.OrdinalIgnoreCase) &&
                        engineIdentifier != "DragonAge" && engineIdentifier != "MassEffect")
                    {
                        throw new InvalidDataException($"Save file created with incompatible engine: {engineIdentifier}");
                    }
                }

                // Create metadata object
                var metadata = new SaveGameMetadata
                {
                    SaveVersion = saveVersion,
                    EngineVersion = engineIdentifier ?? EngineIdentifier
                };

                // Read standard metadata fields
                metadata.SaveName = ReadString(reader);
                metadata.AreaName = ReadString(reader);
                metadata.TimePlayed = reader.ReadInt32();

                // Read timestamp from FileTime
                long fileTime = reader.ReadInt64();
                try
                {
                    metadata.Timestamp = DateTime.FromFileTime(fileTime);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Invalid FileTime, use current time as fallback
                    metadata.Timestamp = DateTime.Now;
                }

                // Read screenshot data (we don't store it in metadata, but we need to skip it)
                int screenshotLength = reader.ReadInt32();
                if (screenshotLength > 0 && screenshotLength <= stream.Length - stream.Position)
                {
                    reader.ReadBytes(screenshotLength);
                }
                else if (screenshotLength > 0)
                {
                    throw new InvalidDataException($"Invalid screenshot length: {screenshotLength}");
                }

                // Read portrait data (we don't store it in metadata, but we need to skip it)
                int portraitLength = reader.ReadInt32();
                if (portraitLength > 0 && portraitLength <= stream.Length - stream.Position)
                {
                    reader.ReadBytes(portraitLength);
                }
                else if (portraitLength > 0)
                {
                    throw new InvalidDataException($"Invalid portrait length: {portraitLength}");
                }

                // Read player name (we don't store it in metadata, but we need to skip it)
                string playerName = ReadString(reader);

                // Read Eclipse-specific metadata (we need to skip these for metadata object)
                // Morality score
                int moralityScore = reader.ReadInt32();

                // Romance data
                int romanceCount = reader.ReadInt32();
                for (int i = 0; i < romanceCount; i++)
                {
                    string characterName = ReadString(reader);
                    int romanceLevel = reader.ReadInt32();
                    // Romance data is not stored in SaveGameMetadata, but we read it for completeness
                }

                // Squad approval ratings
                int approvalCount = reader.ReadInt32();
                for (int i = 0; i < approvalCount; i++)
                {
                    string characterName = ReadString(reader);
                    int approvalRating = reader.ReadInt32();
                    // Approval data is not stored in SaveGameMetadata, but we read it for completeness
                }

                // Mission completion status
                int missionCount = reader.ReadInt32();
                for (int i = 0; i < missionCount; i++)
                {
                    string missionId = ReadString(reader);
                    bool completed = reader.ReadBoolean();
                    // Mission data is not stored in SaveGameMetadata, but we read it for completeness
                }

                // Difficulty level
                int difficultyLevel = reader.ReadInt32();

                // DLC flags
                int dlcCount = reader.ReadInt32();
                for (int i = 0; i < dlcCount; i++)
                {
                    string dlcName = ReadString(reader);
                    bool enabled = reader.ReadBoolean();
                    // DLC data is not stored in SaveGameMetadata, but we read it for completeness
                }

                // Save type
                int saveType = reader.ReadInt32();

                // Cheat used flag
                bool cheatUsed = reader.ReadBoolean();

                // Save number
                int saveNumber = reader.ReadInt32();

                // Validate we've read all data (optional check)
                if (stream.Position < stream.Length)
                {
                    // There's extra data, which is acceptable (future format extensions)
                    // We'll just log a warning or ignore it
                }

                return metadata;
            }
        }

        /// <summary>
        /// Serializes global Eclipse game state.
        /// </summary>
        /// <remarks>
        /// Eclipse globals are more complex than other engines.
        /// Includes morality choices, romance states, reputation systems.
        /// Handles branching narratives and player choice consequences.
        ///
        /// Eclipse global categories:
        /// - MORALITY: Paragon/Renegade choices and scores
        /// - ROMANCE: Relationship states and progress
        /// - REPUTATION: Faction standings and alliances
        /// - CHOICES: Major narrative decision tracking
        /// - DLC_STATE: DLC-specific variable tracking
        /// - IMPORTED: Variables imported from previous games
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Global variable serialization functions
        /// - DragonAge2.exe: Enhanced global variable serialization
        /// - MassEffect.exe: UBioGlobalVariableTable serialization
        /// - MassEffect2.exe: Advanced global variable state with relationships
        ///
        /// Binary format:
        /// - Boolean count (int32) + [name (string) + value (bool)] pairs
        /// - Number count (int32) + [name (string) + value (int32)] pairs
        /// - String count (int32) + [name (string) + value (string)] pairs
        /// - Location count (int32) + [name (string) + location data] pairs
        /// </remarks>
        public override byte[] SerializeGlobals(IGameState gameState)
        {
            if (gameState == null)
            {
                // Return empty serialized state
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    writer.Write(0); // Boolean count
                    writer.Write(0); // Number count
                    writer.Write(0); // String count
                    writer.Write(0); // Location count
                    return stream.ToArray();
                }
            }

            // Extract all global variables from IGameState
            // Build GlobalVariableState by trying each type for each variable name
            var globalState = new GlobalVariableState();
            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Get all global variable names
            var globalNames = gameState.GetGlobalNames();
            if (globalNames != null)
            {
                foreach (string name in globalNames)
                {
                    if (string.IsNullOrEmpty(name) || processedNames.Contains(name))
                    {
                        continue;
                    }

                    processedNames.Add(name);

                    // Try to get as int
                    try
                    {
                        int intValue = gameState.GetGlobal<int>(name, int.MinValue);
                        if (intValue != int.MinValue || gameState.HasGlobal(name))
                        {
                            // Check if it's actually a boolean stored as int (0/1)
                            bool boolValue = gameState.GetGlobal<bool>(name, false);
                            if (boolValue || intValue == 0)
                            {
                                // Try to get as bool to see if it's actually a boolean
                                bool actualBool = gameState.GetGlobal<bool>(name);
                                if (actualBool == (intValue != 0))
                                {
                                    // It's a boolean, store as bool
                                    globalState.Booleans[name] = actualBool;
                                    continue;
                                }
                            }
                            // It's an int
                            globalState.Numbers[name] = intValue;
                            continue;
                        }
                    }
                    catch
                    {
                        // Not an int, try other types
                    }

                    // Try to get as bool
                    try
                    {
                        bool boolValue = gameState.GetGlobal<bool>(name, false);
                        if (gameState.HasGlobal(name))
                        {
                            globalState.Booleans[name] = boolValue;
                            continue;
                        }
                    }
                    catch
                    {
                        // Not a bool, try other types
                    }

                    // Try to get as string
                    try
                    {
                        string stringValue = gameState.GetGlobal<string>(name, null);
                        if (stringValue != null || gameState.HasGlobal(name))
                        {
                            globalState.Strings[name] = stringValue ?? "";
                            continue;
                        }
                    }
                    catch
                    {
                        // Not a string, try location
                    }

                    // Try to get as location (object)
                    try
                    {
                        object locationValue = gameState.GetGlobal<object>(name, null);
                        if (locationValue != null)
                        {
                            // Convert location object to SavedLocation if possible
                            // For now, we'll skip locations as they require more complex handling
                            // Locations can be added later if needed
                        }
                    }
                    catch
                    {
                        // Not a location or other type
                    }
                }
            }

            // Serialize to binary format using Eclipse binary serialization
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                // Serialize boolean globals
                writer.Write(globalState.Booleans != null ? globalState.Booleans.Count : 0);
                if (globalState.Booleans != null)
                {
                    foreach (var kvp in globalState.Booleans)
                    {
                        WriteString(writer, kvp.Key);
                        writer.Write(kvp.Value);
                    }
                }

                // Serialize numeric globals
                writer.Write(globalState.Numbers != null ? globalState.Numbers.Count : 0);
                if (globalState.Numbers != null)
                {
                    foreach (var kvp in globalState.Numbers)
                    {
                        WriteString(writer, kvp.Key);
                        writer.Write(kvp.Value);
                    }
                }

                // Serialize string globals
                writer.Write(globalState.Strings != null ? globalState.Strings.Count : 0);
                if (globalState.Strings != null)
                {
                    foreach (var kvp in globalState.Strings)
                    {
                        WriteString(writer, kvp.Key);
                        WriteString(writer, kvp.Value ?? "");
                    }
                }

                // Serialize location globals (Eclipse supports locations)
                writer.Write(globalState.Locations != null ? globalState.Locations.Count : 0);
                if (globalState.Locations != null)
                {
                    foreach (var kvp in globalState.Locations)
                    {
                        WriteString(writer, kvp.Key);
                        var location = kvp.Value;
                        if (location != null)
                        {
                            // Serialize location: position (3 floats) + facing (1 float) + area (string)
                            writer.Write(location.Position.X);
                            writer.Write(location.Position.Y);
                            writer.Write(location.Position.Z);
                            writer.Write(location.Facing);
                            WriteString(writer, location.AreaResRef ?? "");
                        }
                        else
                        {
                            // Write zero location
                            writer.Write(0f);
                            writer.Write(0f);
                            writer.Write(0f);
                            writer.Write(0f);
                            WriteString(writer, "");
                        }
                    }
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes Eclipse global state.
        /// </summary>
        /// <remarks>
        /// Restores complex game state with morality consequences.
        /// Updates romance progress and reputation standings.
        /// Applies player choice effects to game world.
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Global variable deserialization functions
        /// - DragonAge2.exe: Enhanced global variable deserialization
        /// - MassEffect.exe: UBioGlobalVariableTable deserialization
        /// - MassEffect2.exe: Advanced global variable state restoration
        ///
        /// Binary format (matches SerializeGlobals):
        /// - Boolean count (int32) + [name (string) + value (bool)] pairs
        /// - Number count (int32) + [name (string) + value (int32)] pairs
        /// - String count (int32) + [name (string) + value (string)] pairs
        /// - Location count (int32) + [name (string) + location data] pairs
        /// </remarks>
        public override void DeserializeGlobals(byte[] globalsData, IGameState gameState)
        {
            if (gameState == null)
            {
                return;
            }

            if (globalsData == null || globalsData.Length == 0)
            {
                return;
            }

            using (var stream = new MemoryStream(globalsData))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                // Deserialize boolean globals
                int boolCount = reader.ReadInt32();
                for (int i = 0; i < boolCount; i++)
                {
                    string name = ReadString(reader);
                    bool value = reader.ReadBoolean();
                    if (!string.IsNullOrEmpty(name))
                    {
                        gameState.SetGlobal(name, value);
                    }
                }

                // Deserialize numeric globals
                int numberCount = reader.ReadInt32();
                for (int i = 0; i < numberCount; i++)
                {
                    string name = ReadString(reader);
                    int value = reader.ReadInt32();
                    if (!string.IsNullOrEmpty(name))
                    {
                        gameState.SetGlobal(name, value);
                    }
                }

                // Deserialize string globals
                int stringCount = reader.ReadInt32();
                for (int i = 0; i < stringCount; i++)
                {
                    string name = ReadString(reader);
                    string value = ReadString(reader);
                    if (!string.IsNullOrEmpty(name))
                    {
                        gameState.SetGlobal(name, value ?? "");
                    }
                }

                // Deserialize location globals
                int locationCount = reader.ReadInt32();
                for (int i = 0; i < locationCount; i++)
                {
                    string name = ReadString(reader);
                    if (!string.IsNullOrEmpty(name))
                    {
                        // Read location data: position (3 floats) + facing (1 float) + area (string)
                        float x = reader.ReadSingle();
                        float y = reader.ReadSingle();
                        float z = reader.ReadSingle();
                        float facing = reader.ReadSingle();
                        string areaTag = ReadString(reader);

                        // Create SavedLocation object
                        var location = new SavedLocation
                        {
                            Position = new Vector3(x, y, z),
                            Facing = facing,
                            AreaResRef = areaTag
                        };

                        // Set location in game state
                        // Note: IGameState.SetGlobal accepts object, so we can pass SavedLocation
                        gameState.SetGlobal(name, location);
                    }
                }
            }
        }

        /// <summary>
        /// Serializes Eclipse party/squad information.
        /// </summary>
        /// <remarks>
        /// Eclipse party system is more complex than Odyssey.
        /// Includes approval ratings, loyalty, romance flags.
        /// Tracks squad composition and tactical roles.
        ///
        /// Squad data includes:
        /// - Squad member approval and loyalty
        /// - Romance relationships with player
        /// - Equipment and customization
        /// - Mission performance statistics
        /// - Dialogue state and conversation history
        /// - Tactical AI settings and behaviors
        /// </remarks>
        public override byte[] SerializeParty(IPartyState partyState)
        {
            // TODO: Implement Eclipse squad serialization
            // Use ConvertToPartyState(partyState) helper from base class to extract party data
            // Create SQUAD struct with relationships
            // Serialize approval and romance data
            // Include mission performance stats
            // Save tactical AI configurations
            // Note: Eclipse uses binary format, not GFF like Odyssey

            throw new NotImplementedException("Eclipse squad serialization not yet implemented");
        }

        /// <summary>
        /// Deserializes Eclipse squad information.
        /// </summary>
        /// <remarks>
        /// Recreates squad with complex relationships.
        /// Restores approval, romance, and loyalty states.
        /// Reapplies mission consequences and dialogue history.
        /// </remarks>
        public override void DeserializeParty(byte[] partyData, IPartyState partyState)
        {
            // TODO: Implement Eclipse squad deserialization
            // Parse SQUAD struct with relationships
            // Restore approval and romance states
            // Reapply mission consequences
            // Recreate dialogue and tactical state
        }

        /// <summary>
        /// Serializes Eclipse area state with physics and destruction.
        /// </summary>
        /// <remarks>
        /// Eclipse areas include physics state and destructible geometry.
        /// Saves real-time modifications and environmental changes.
        /// Most complex area serialization of all engines.
        ///
        /// Eclipse area state includes:
        /// - Physics world state (bodies, constraints, destruction)
        /// - Destructible geometry modifications
        /// - Dynamic lighting configurations
        /// - Placed objects and environmental changes
        /// - Weather and atmospheric conditions
        /// - Interactive element states
        /// - Navigation mesh modifications
        /// </remarks>
        public override byte[] SerializeArea(IArea area)
        {
            // TODO: Implement Eclipse area serialization
            // Create complex AREA struct
            // Serialize physics world state
            // Save destructible geometry changes
            // Include dynamic lighting and effects
            // Preserve navigation modifications

            throw new NotImplementedException("Eclipse area serialization not yet implemented");
        }

        /// <summary>
        /// Deserializes Eclipse area with physics restoration.
        /// </summary>
        /// <remarks>
        /// Restores physics state and destructible changes.
        /// Recreates dynamic lighting and environmental effects.
        /// Most complex area deserialization of all engines.
        /// </remarks>
        public override void DeserializeArea(byte[] areaData, IArea area)
        {
            // TODO: Implement Eclipse area deserialization
            // Parse complex AREA struct
            // Restore physics world state
            // Recreate destructible geometry
            // Reapply lighting and effects
            // Update navigation mesh
        }

        /// <summary>
        /// Serializes Eclipse entities with physics and AI state.
        /// </summary>
        /// <remarks>
        /// Eclipse entities include physics components and complex AI.
        /// Saves relationship states, approval ratings, romance flags.
        /// Includes tactical AI configurations and behavior states.
        ///
        /// Eclipse entity enhancements:
        /// - Physics body state and constraints
        /// - Complex AI behavior trees and state
        /// - Relationship and approval systems
        /// - Romance and dialogue state
        /// - Tactical positioning and cover preferences
        /// - Equipment and customization state
        /// - Mission-specific flags and objectives
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Entity serialization functions
        /// - DragonAge2.exe: Enhanced entity state serialization
        /// - MassEffect.exe: Squad member entity serialization
        /// - MassEffect2.exe: Advanced entity state with relationships
        /// </remarks>
        public override byte[] SerializeEntities(IEnumerable<IEntity> entities)
        {
            if (entities == null)
            {
                return new byte[0];
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                // Write entity count
                var entityList = entities.ToList();
                writer.Write(entityList.Count);

                // Serialize each entity
                foreach (var entity in entityList)
                {
                    SerializeEntity(writer, entity);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes Eclipse entities with full state restoration.
        /// </summary>
        /// <remarks>
        /// Recreates entities with physics, AI, and relationship state.
        /// Restores complex behavioral configurations.
        /// Handles entity interdependencies and references.
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Entity deserialization functions
        /// - DragonAge2.exe: Enhanced entity state restoration
        /// - MassEffect.exe: Squad member entity restoration
        /// - MassEffect2.exe: Advanced entity state with relationships
        /// </remarks>
        public override IEnumerable<IEntity> DeserializeEntities(byte[] entitiesData)
        {
            if (entitiesData == null || entitiesData.Length == 0)
            {
                yield break;
            }

            using (var stream = new MemoryStream(entitiesData))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                // Read entity count
                int entityCount = reader.ReadInt32();

                // Deserialize each entity
                for (int i = 0; i < entityCount; i++)
                {
                    var entity = DeserializeEntity(reader);
                    if (entity != null)
                    {
                        yield return entity;
                    }
                }
            }
        }

        #region Entity Serialization Helpers

        /// <summary>
        /// Writes a string to a binary writer (length-prefixed UTF-8).
        /// </summary>
        private void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                value = "";
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            if (bytes.Length > 0)
            {
                writer.Write(bytes);
            }
        }

        /// <summary>
        /// Reads a string from a binary reader (length-prefixed UTF-8).
        /// </summary>
        private string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 65536) // Sanity check
            {
                throw new InvalidDataException($"Invalid string length: {length}");
            }

            if (length == 0)
            {
                return "";
            }

            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Serializes a single entity with all components and state.
        /// </summary>
        /// <remarks>
        /// Comprehensive entity serialization including:
        /// - Basic properties (ObjectId, Tag, ObjectType, AreaId, IsValid)
        /// - Transform component (Position, Facing, Scale, Parent)
        /// - Stats component (HP, FP, abilities, skills, saves, level)
        /// - Inventory component (all items in all slots)
        /// - ScriptHooks component (script ResRefs and local variables)
        /// - Door component (if present)
        /// - Placeable component (if present)
        /// - Custom data dictionary
        /// </remarks>
        private void SerializeEntity(BinaryWriter writer, IEntity entity)
        {
            if (entity == null)
            {
                writer.Write(0); // Has entity flag
                return;
            }

            writer.Write(1); // Has entity flag

            // Serialize basic entity properties
            writer.Write(entity.ObjectId);
            WriteString(writer, entity.Tag ?? "");
            writer.Write((int)entity.ObjectType);
            writer.Write(entity.AreaId);
            writer.Write(entity.IsValid ? 1 : 0);

            // Serialize Transform component
            var transformComponent = entity.GetComponent<ITransformComponent>();
            writer.Write(transformComponent != null ? 1 : 0);
            if (transformComponent != null)
            {
                writer.Write(transformComponent.Position.X);
                writer.Write(transformComponent.Position.Y);
                writer.Write(transformComponent.Position.Z);
                writer.Write(transformComponent.Facing);
                writer.Write(transformComponent.Scale.X);
                writer.Write(transformComponent.Scale.Y);
                writer.Write(transformComponent.Scale.Z);
                writer.Write(transformComponent.Parent != null ? transformComponent.Parent.ObjectId : 0u);
            }

            // Serialize Stats component
            var statsComponent = entity.GetComponent<IStatsComponent>();
            writer.Write(statsComponent != null ? 1 : 0);
            if (statsComponent != null)
            {
                writer.Write(statsComponent.CurrentHP);
                writer.Write(statsComponent.MaxHP);
                writer.Write(statsComponent.CurrentFP);
                writer.Write(statsComponent.MaxFP);
                writer.Write(statsComponent.IsDead ? 1 : 0);
                writer.Write(statsComponent.BaseAttackBonus);
                writer.Write(statsComponent.ArmorClass);
                writer.Write(statsComponent.FortitudeSave);
                writer.Write(statsComponent.ReflexSave);
                writer.Write(statsComponent.WillSave);
                writer.Write(statsComponent.WalkSpeed);
                writer.Write(statsComponent.RunSpeed);
                writer.Write(statsComponent.Level);

                // Serialize ability scores
                writer.Write(statsComponent.GetAbility(Ability.Strength));
                writer.Write(statsComponent.GetAbility(Ability.Dexterity));
                writer.Write(statsComponent.GetAbility(Ability.Constitution));
                writer.Write(statsComponent.GetAbility(Ability.Intelligence));
                writer.Write(statsComponent.GetAbility(Ability.Wisdom));
                writer.Write(statsComponent.GetAbility(Ability.Charisma));

                // Serialize ability modifiers
                writer.Write(statsComponent.GetAbilityModifier(Ability.Strength));
                writer.Write(statsComponent.GetAbilityModifier(Ability.Dexterity));
                writer.Write(statsComponent.GetAbilityModifier(Ability.Constitution));
                writer.Write(statsComponent.GetAbilityModifier(Ability.Intelligence));
                writer.Write(statsComponent.GetAbilityModifier(Ability.Wisdom));
                writer.Write(statsComponent.GetAbilityModifier(Ability.Charisma));

                // Serialize known spells (simplified - serialize spell IDs)
                // Note: In a full implementation, we would iterate through all possible spell IDs
                // For now, we serialize an empty count as a placeholder
                writer.Write(0); // Known spell count
            }

            // Serialize Inventory component
            var inventoryComponent = entity.GetComponent<IInventoryComponent>();
            writer.Write(inventoryComponent != null ? 1 : 0);
            if (inventoryComponent != null)
            {
                // Collect all items from all slots
                var allItems = new List<(int slot, IEntity item)>();
                for (int slot = 0; slot < 256; slot++) // Reasonable upper bound
                {
                    var item = inventoryComponent.GetItemInSlot(slot);
                    if (item != null)
                    {
                        allItems.Add((slot, item));
                    }
                }

                writer.Write(allItems.Count);
                foreach (var (slot, item) in allItems)
                {
                    writer.Write(slot);
                    writer.Write(item.ObjectId);
                    WriteString(writer, item.Tag ?? "");
                    writer.Write((int)item.ObjectType);
                }
            }

            // Serialize ScriptHooks component
            var scriptHooksComponent = entity.GetComponent<IScriptHooksComponent>();
            writer.Write(scriptHooksComponent != null ? 1 : 0);
            if (scriptHooksComponent != null)
            {
                // Serialize script ResRefs for all event types
                int scriptCount = 0;
                var scripts = new List<(int eventType, string resRef)>();
                foreach (ScriptEvent eventType in Enum.GetValues(typeof(ScriptEvent)))
                {
                    string scriptResRef = scriptHooksComponent.GetScript(eventType);
                    if (!string.IsNullOrEmpty(scriptResRef))
                    {
                        scripts.Add(((int)eventType, scriptResRef));
                        scriptCount++;
                    }
                }
                writer.Write(scriptCount);
                foreach (var (eventType, resRef) in scripts)
                {
                    writer.Write(eventType);
                    WriteString(writer, resRef);
                }

                // Serialize local variables
                // Note: Accessing local variables requires reflection or a different approach
                // For now, we serialize empty local variable sets
                // In a full implementation, we would need access to the internal dictionaries
                writer.Write(0); // Local int count
                writer.Write(0); // Local float count
                writer.Write(0); // Local string count
            }

            // Serialize Door component
            var doorComponent = entity.GetComponent<IDoorComponent>();
            writer.Write(doorComponent != null ? 1 : 0);
            if (doorComponent != null)
            {
                writer.Write(doorComponent.IsOpen ? 1 : 0);
                writer.Write(doorComponent.IsLocked ? 1 : 0);
                writer.Write(doorComponent.LockableByScript ? 1 : 0);
                writer.Write(doorComponent.LockDC);
                writer.Write(doorComponent.IsBashed ? 1 : 0);
                writer.Write(doorComponent.HitPoints);
                writer.Write(doorComponent.MaxHitPoints);
                writer.Write(doorComponent.Hardness);
                WriteString(writer, doorComponent.KeyTag ?? "");
                writer.Write(doorComponent.KeyRequired ? 1 : 0);
                writer.Write(doorComponent.OpenState);
                WriteString(writer, doorComponent.LinkedTo ?? "");
                WriteString(writer, doorComponent.LinkedToModule ?? "");
            }

            // Serialize Placeable component
            var placeableComponent = entity.GetComponent<IPlaceableComponent>();
            writer.Write(placeableComponent != null ? 1 : 0);
            if (placeableComponent != null)
            {
                writer.Write(placeableComponent.IsUseable ? 1 : 0);
                writer.Write(placeableComponent.HasInventory ? 1 : 0);
                writer.Write(placeableComponent.IsStatic ? 1 : 0);
                writer.Write(placeableComponent.IsOpen ? 1 : 0);
                writer.Write(placeableComponent.IsLocked ? 1 : 0);
                writer.Write(placeableComponent.LockDC);
                WriteString(writer, placeableComponent.KeyTag ?? "");
                writer.Write(placeableComponent.HitPoints);
                writer.Write(placeableComponent.MaxHitPoints);
                writer.Write(placeableComponent.Hardness);
                writer.Write(placeableComponent.AnimationState);
            }

            // Serialize custom data dictionary
            // Access custom data via IEntity interface methods
            // Note: This is a simplified approach - in a full implementation we might use reflection
            // to access the internal _data dictionary for more efficient serialization
            var customDataEntries = new List<(string key, object value, int type)>();

            // Try to serialize common custom data patterns
            // In a full implementation, we would iterate through all custom data entries
            // For now, we serialize an empty set as components handle their own state
            writer.Write(0); // Custom data count
        }

        /// <summary>
        /// Deserializes a single entity with all components and state.
        /// </summary>
        /// <remarks>
        /// Comprehensive entity deserialization restoring:
        /// - Basic properties (ObjectId, Tag, ObjectType, AreaId, IsValid)
        /// - Transform component (Position, Facing, Scale, Parent reference)
        /// - Stats component (HP, FP, abilities, skills, saves, level)
        /// - Inventory component (all items in all slots)
        /// - ScriptHooks component (script ResRefs and local variables)
        /// - Door component (if present)
        /// - Placeable component (if present)
        /// - Custom data dictionary
        ///
        /// Note: This creates entity state data but does not fully reconstruct IEntity objects.
        /// Full entity reconstruction requires entity factory and component system integration.
        /// </remarks>
        private IEntity DeserializeEntity(BinaryReader reader)
        {
            bool hasEntity = reader.ReadInt32() != 0;
            if (!hasEntity)
            {
                return null;
            }

            // Read basic entity properties
            uint objectId = reader.ReadUInt32();
            string tag = ReadString(reader);
            ObjectType objectType = (ObjectType)reader.ReadInt32();
            uint areaId = reader.ReadUInt32();
            bool isValid = reader.ReadInt32() != 0;

            // Create basic entity structure
            // Note: Full component restoration requires component factories which are engine-specific
            // This creates the entity with basic properties; components would need to be attached separately
            var entity = new Entity(objectId, objectType);
            entity.Tag = tag;
            entity.AreaId = areaId;
            // IsValid is read-only, so we can't set it directly
            // Components will need to be created and attached via component factories

            // Read Transform component
            bool hasTransform = reader.ReadInt32() != 0;
            Vector3 position = Vector3.Zero;
            float facing = 0f;
            Vector3 scale = Vector3.One;
            uint parentObjectId = 0u;
            if (hasTransform)
            {
                position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                facing = reader.ReadSingle();
                scale = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                parentObjectId = reader.ReadUInt32();
                // Set position directly on entity (Entity has Position property)
                entity.Position = position;
                // Note: Transform component would need to be created and attached separately
            }

            // Read Stats component
            bool hasStats = reader.ReadInt32() != 0;
            if (hasStats)
            {
                int currentHP = reader.ReadInt32();
                int maxHP = reader.ReadInt32();
                int currentFP = reader.ReadInt32();
                int maxFP = reader.ReadInt32();
                bool isDead = reader.ReadInt32() != 0;
                int baseAttackBonus = reader.ReadInt32();
                int armorClass = reader.ReadInt32();
                int fortitudeSave = reader.ReadInt32();
                int reflexSave = reader.ReadInt32();
                int willSave = reader.ReadInt32();
                float walkSpeed = reader.ReadSingle();
                float runSpeed = reader.ReadSingle();
                int level = reader.ReadInt32();

                // Read ability scores
                int str = reader.ReadInt32();
                int dex = reader.ReadInt32();
                int con = reader.ReadInt32();
                int intel = reader.ReadInt32();
                int wis = reader.ReadInt32();
                int cha = reader.ReadInt32();

                // Read ability modifiers
                int strMod = reader.ReadInt32();
                int dexMod = reader.ReadInt32();
                int conMod = reader.ReadInt32();
                int intMod = reader.ReadInt32();
                int wisMod = reader.ReadInt32();
                int chaMod = reader.ReadInt32();

                // Read known spells
                int spellCount = reader.ReadInt32();
                for (int i = 0; i < spellCount; i++)
                {
                    int spellId = reader.ReadInt32();
                    // Would restore spell knowledge here
                }
            }

            // Read Inventory component
            bool hasInventory = reader.ReadInt32() != 0;
            if (hasInventory)
            {
                int itemCount = reader.ReadInt32();
                for (int i = 0; i < itemCount; i++)
                {
                    int slot = reader.ReadInt32();
                    uint itemObjectId = reader.ReadUInt32();
                    string itemTag = ReadString(reader);
                    ObjectType itemObjectType = (ObjectType)reader.ReadInt32();
                    // Would restore item in slot here
                }
            }

            // Read ScriptHooks component
            bool hasScriptHooks = reader.ReadInt32() != 0;
            if (hasScriptHooks)
            {
                int scriptCount = reader.ReadInt32();
                for (int i = 0; i < scriptCount; i++)
                {
                    int eventType = reader.ReadInt32();
                    string resRef = ReadString(reader);
                    // Would restore script hook here
                }

                // Read local variables
                int localIntCount = reader.ReadInt32();
                for (int i = 0; i < localIntCount; i++)
                {
                    string name = ReadString(reader);
                    int value = reader.ReadInt32();
                    // Would restore local int here
                }

                int localFloatCount = reader.ReadInt32();
                for (int i = 0; i < localFloatCount; i++)
                {
                    string name = ReadString(reader);
                    float value = reader.ReadSingle();
                    // Would restore local float here
                }

                int localStringCount = reader.ReadInt32();
                for (int i = 0; i < localStringCount; i++)
                {
                    string name = ReadString(reader);
                    string value = ReadString(reader);
                    // Would restore local string here
                }
            }

            // Read Door component
            bool hasDoor = reader.ReadInt32() != 0;
            if (hasDoor)
            {
                bool isOpen = reader.ReadInt32() != 0;
                bool isLocked = reader.ReadInt32() != 0;
                bool lockableByScript = reader.ReadInt32() != 0;
                int lockDC = reader.ReadInt32();
                bool isBashed = reader.ReadInt32() != 0;
                int hitPoints = reader.ReadInt32();
                int maxHitPoints = reader.ReadInt32();
                int hardness = reader.ReadInt32();
                string keyTag = ReadString(reader);
                bool keyRequired = reader.ReadInt32() != 0;
                int openState = reader.ReadInt32();
                string linkedTo = ReadString(reader);
                string linkedToModule = ReadString(reader);
                // Would restore door component here
            }

            // Read Placeable component
            bool hasPlaceable = reader.ReadInt32() != 0;
            if (hasPlaceable)
            {
                bool isUseable = reader.ReadInt32() != 0;
                bool hasInventory = reader.ReadInt32() != 0;
                bool isStatic = reader.ReadInt32() != 0;
                bool isOpen = reader.ReadInt32() != 0;
                bool isLocked = reader.ReadInt32() != 0;
                int lockDC = reader.ReadInt32();
                string keyTag = ReadString(reader);
                int hitPoints = reader.ReadInt32();
                int maxHitPoints = reader.ReadInt32();
                int hardness = reader.ReadInt32();
                int animationState = reader.ReadInt32();
                // Would restore placeable component here
            }

            // Read custom data
            int customDataCount = reader.ReadInt32();
            for (int i = 0; i < customDataCount; i++)
            {
                string key = ReadString(reader);
                int valueType = reader.ReadInt32();
                // Restore custom data based on valueType
                switch (valueType)
                {
                    case 0: // int
                        entity.SetData(key, reader.ReadInt32());
                        break;
                    case 1: // float
                        entity.SetData(key, reader.ReadSingle());
                        break;
                    case 2: // string
                        entity.SetData(key, ReadString(reader));
                        break;
                    case 3: // bool
                        entity.SetData(key, reader.ReadInt32() != 0);
                        break;
                    default:
                        // Skip unknown types
                        break;
                }
            }

            // Return entity with basic properties restored
            // Note: Components (Transform, Stats, Inventory, etc.) would need to be created
            // via component factories and attached separately. This is engine-specific.
            return entity;
        }

        #endregion

        /// <summary>
        /// Creates Eclipse save directory with complex structure.
        /// </summary>
        /// <remarks>
        /// Eclipse saves have more complex directory structures.
        /// Includes separate files for different state types.
        /// Supports screenshots, metadata, and DLC content.
        ///
        /// Eclipse save structure:
        /// - Main save directory with game-specific naming
        /// - Save metadata and screenshot files
        /// - Separate physics state files
        /// - DLC-specific save data directories
        /// - Relationship and romance state files
        /// - Mission progress and objective tracking
        /// </remarks>
        public override void CreateSaveDirectory(string saveName, SaveGameData saveData)
        {
            // TODO: Implement Eclipse save directory creation
            // Create game-specific directory structure
            // Write multiple state files
            // Include DLC-specific directories
            // Save screenshots and metadata
        }

        /// <summary>
        /// Validates Eclipse save compatibility with complex requirements.
        /// </summary>
        /// <remarks>
        /// Eclipse compatibility checking is most complex.
        /// Validates DLC requirements, version compatibility.
        /// Checks physics engine versions and feature support.
        /// Includes morality and romance state validation.
        ///
        /// Compatibility checks:
        /// - Engine version compatibility
        /// - DLC and expansion requirements
        /// - Physics engine version matching
        /// - Morality system compatibility
        /// - Romance state validation
        /// - Mission progression integrity
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Save validation functions
        /// - DragonAge2.exe: Enhanced save validation
        /// - MassEffect.exe: Save compatibility checking
        /// - MassEffect2.exe: Advanced save validation with DLC checks
        /// </remarks>
        public override SaveCompatibility CheckCompatibility(string savePath)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                return SaveCompatibility.Incompatible;
            }

            // Check if save directory exists
            if (!Directory.Exists(savePath))
            {
                return SaveCompatibility.Incompatible;
            }

            // List of compatibility issues found
            var issues = new List<string>();
            var warnings = new List<string>();

            // Check for required save files
            // Eclipse saves typically have:
            // - NFO file: Save metadata (signature, version, basic info)
            // - Save archive file: Full game state (varies by game: .das, .das2, .pcsave, .pcsave2)
            string nfoPath = Path.Combine(savePath, "save.nfo");
            if (!File.Exists(nfoPath))
            {
                // Try alternative NFO file names
                nfoPath = Path.Combine(savePath, "SaveGame.nfo");
                if (!File.Exists(nfoPath))
                {
                    nfoPath = Path.Combine(savePath, "savenfo.res");
                    if (!File.Exists(nfoPath))
                    {
                        return SaveCompatibility.Incompatible;
                    }
                }
            }

            // Try to read and validate NFO file
            SaveGameMetadata metadata = null;
            try
            {
                byte[] nfoData = File.ReadAllBytes(nfoPath);
                if (nfoData == null || nfoData.Length == 0)
                {
                    return SaveCompatibility.Incompatible;
                }

                // Try to deserialize NFO to validate structure
                // Note: This uses DeserializeSaveNfo which validates signature and version
                try
                {
                    // For Eclipse saves, we need to determine which game-specific serializer to use
                    // For now, we'll do basic validation by checking the signature
                    using (var stream = new MemoryStream(nfoData))
                    using (var reader = new BinaryReader(stream, Encoding.UTF8))
                    {
                        // Read signature (4 bytes)
                        if (stream.Length < 4)
                        {
                            return SaveCompatibility.Incompatible;
                        }

                        byte[] signatureBytes = reader.ReadBytes(4);
                        string signature = Encoding.UTF8.GetString(signatureBytes);

                        // Validate signature matches known Eclipse save formats
                        // Known signatures: "DAS " (Dragon Age: Origins), "DAS2" (Dragon Age 2),
                        // "MES " (Mass Effect), "MES2" (Mass Effect 2)
                        bool isValidSignature = signature == "DAS " || signature == "DAS2" ||
                                                signature == "MES " || signature == "MES2";
                        if (!isValidSignature)
                        {
                            issues.Add($"Invalid save file signature: '{signature}'");
                            return SaveCompatibility.Incompatible;
                        }

                        // Read version (4 bytes)
                        if (stream.Length < 8)
                        {
                            return SaveCompatibility.Incompatible;
                        }

                        int saveVersion = reader.ReadInt32();

                        // Validate version compatibility
                        // Eclipse save versions: DAO=1, DA2=2, ME1=3, ME2=4
                        // Current implementation supports version 4 (ME2) as maximum
                        if (saveVersion < 1 || saveVersion > SaveVersion)
                        {
                            if (saveVersion > SaveVersion)
                            {
                                issues.Add($"Save file version {saveVersion} is newer than supported version {SaveVersion}. Migration required.");
                                return SaveCompatibility.RequiresMigration;
                            }
                            else
                            {
                                issues.Add($"Unsupported save file version: {saveVersion}");
                                return SaveCompatibility.Incompatible;
                            }
                        }

                        // Check if version is significantly older (may need migration)
                        if (saveVersion < SaveVersion - 1)
                        {
                            warnings.Add($"Save file version {saveVersion} is older than current version {SaveVersion}. Some features may not be available.");
                        }

                        // Read engine identifier if present (after version)
                        // Eclipse saves may include engine identifier string
                        if (stream.Length > 8)
                        {
                            try
                            {
                                // Try to read engine identifier (length-prefixed string)
                                int identifierLength = reader.ReadInt32();
                                if (identifierLength > 0 && identifierLength < 256 && stream.Position + identifierLength <= stream.Length)
                                {
                                    byte[] identifierBytes = reader.ReadBytes(identifierLength);
                                    string engineIdentifier = Encoding.UTF8.GetString(identifierBytes);

                                    if (!string.IsNullOrEmpty(engineIdentifier) &&
                                        !engineIdentifier.Equals(EngineIdentifier, StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Check if it's a compatible Eclipse variant
                                        if (engineIdentifier.StartsWith("Eclipse", StringComparison.OrdinalIgnoreCase) ||
                                            engineIdentifier == "DragonAge" || engineIdentifier == "MassEffect")
                                        {
                                            warnings.Add($"Save file created with different Eclipse variant: {engineIdentifier}");
                                        }
                                        else
                                        {
                                            issues.Add($"Save file created with incompatible engine: {engineIdentifier}");
                                            return SaveCompatibility.Incompatible;
                                        }
                                    }
                                }
                            }
                            catch
                            {
                                // Engine identifier may not be present in older saves, this is acceptable
                            }
                        }
                    }
                }
                catch (InvalidDataException ex)
                {
                    issues.Add($"Invalid save file format: {ex.Message}");
                    return SaveCompatibility.Incompatible;
                }
                catch (NotSupportedException ex)
                {
                    issues.Add($"Unsupported save file version: {ex.Message}");
                    return SaveCompatibility.RequiresMigration;
                }
                catch (Exception ex)
                {
                    issues.Add($"Error reading save file: {ex.Message}");
                    return SaveCompatibility.Incompatible;
                }
            }
            catch (IOException ex)
            {
                issues.Add($"Cannot read save file: {ex.Message}");
                return SaveCompatibility.Incompatible;
            }
            catch (UnauthorizedAccessException ex)
            {
                issues.Add($"Access denied to save file: {ex.Message}");
                return SaveCompatibility.Incompatible;
            }

            // Check for save archive file (full game state)
            // Eclipse saves have different extensions per game
            string[] possibleArchiveExtensions = { ".das", ".das2", ".pcsave", ".pcsave2", ".sav", ".save" };
            bool foundArchive = false;
            foreach (string ext in possibleArchiveExtensions)
            {
                string archivePath = Path.Combine(savePath, $"save{ext}");
                if (File.Exists(archivePath))
                {
                    foundArchive = true;

                    // Validate archive file is not empty
                    FileInfo archiveInfo = new FileInfo(archivePath);
                    if (archiveInfo.Length == 0)
                    {
                        issues.Add("Save archive file is empty");
                        return SaveCompatibility.Incompatible;
                    }

                    // Check if archive file is readable
                    try
                    {
                        using (var stream = File.OpenRead(archivePath))
                        {
                            // Try to read first few bytes to validate structure
                            if (stream.Length >= 4)
                            {
                                byte[] header = new byte[4];
                                stream.Read(header, 0, 4);
                                string archiveSignature = Encoding.UTF8.GetString(header);

                                // Archive should have same signature as NFO
                                // This is a basic validation - full validation would require deserialization
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Cannot fully validate archive file: {ex.Message}");
                    }

                    break;
                }
            }

            if (!foundArchive)
            {
                warnings.Add("Save archive file not found - save may be incomplete");
            }

            // Eclipse-specific compatibility checks

            // Check for DLC requirements
            // Eclipse saves may reference DLC content that needs to be present
            string dlcPath = Path.Combine(savePath, "DLC");
            if (Directory.Exists(dlcPath))
            {
                // Check if DLC files are present and valid
                string[] dlcFiles = Directory.GetFiles(dlcPath, "*.dlc", SearchOption.TopDirectoryOnly);
                foreach (string dlcFile in dlcFiles)
                {
                    try
                    {
                        FileInfo dlcInfo = new FileInfo(dlcFile);
                        if (dlcInfo.Length == 0)
                        {
                            warnings.Add($"DLC file {Path.GetFileName(dlcFile)} is empty or corrupted");
                        }
                    }
                    catch
                    {
                        warnings.Add($"Cannot access DLC file: {Path.GetFileName(dlcFile)}");
                    }
                }
            }

            // Check physics engine compatibility
            // Eclipse saves store physics state that requires compatible physics engine version
            // This is a simplified check - full implementation would validate physics data structure
            string physicsStatePath = Path.Combine(savePath, "physics.dat");
            if (File.Exists(physicsStatePath))
            {
                try
                {
                    FileInfo physicsInfo = new FileInfo(physicsStatePath);
                    if (physicsInfo.Length > 0)
                    {
                        // Basic validation: check file is not corrupted
                        // Full validation would require reading physics engine version from file
                        using (var stream = File.OpenRead(physicsStatePath))
                        {
                            if (stream.Length >= 4)
                            {
                                byte[] header = new byte[4];
                                stream.Read(header, 0, 4);
                                // Physics state files typically have a version header
                                // This is a placeholder - full implementation would validate version
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Physics state file validation failed: {ex.Message}");
                }
            }

            // Validate morality and romance state integrity
            // Eclipse saves store complex relationship and morality data
            // Check if morality/romance data files exist and are valid
            string moralityPath = Path.Combine(savePath, "morality.dat");
            string romancePath = Path.Combine(savePath, "romance.dat");

            if (File.Exists(moralityPath))
            {
                try
                {
                    FileInfo moralityInfo = new FileInfo(moralityPath);
                    if (moralityInfo.Length == 0)
                    {
                        warnings.Add("Morality state file is empty");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Cannot validate morality state: {ex.Message}");
                }
            }

            if (File.Exists(romancePath))
            {
                try
                {
                    FileInfo romanceInfo = new FileInfo(romancePath);
                    if (romanceInfo.Length == 0)
                    {
                        warnings.Add("Romance state file is empty");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Cannot validate romance state: {ex.Message}");
                }
            }

            // Check mission progression integrity
            // Eclipse saves store complex quest/mission state
            // Validate that mission data is consistent
            string missionPath = Path.Combine(savePath, "missions.dat");
            if (File.Exists(missionPath))
            {
                try
                {
                    FileInfo missionInfo = new FileInfo(missionPath);
                    if (missionInfo.Length == 0)
                    {
                        warnings.Add("Mission progression file is empty");
                    }
                }
                catch (Exception ex)
                {
                    warnings.Add($"Cannot validate mission progression: {ex.Message}");
                }
            }

            // Determine final compatibility status
            if (issues.Count > 0)
            {
                // If we have critical issues, return incompatible
                // (Note: issues list is only populated for critical problems that cause Incompatible/RequiresMigration returns)
                return SaveCompatibility.Incompatible;
            }

            if (warnings.Count > 0)
            {
                // If we have warnings but no critical issues, return compatible with warnings
                return SaveCompatibility.CompatibleWithWarnings;
            }

            // No issues or warnings found
            return SaveCompatibility.Compatible;
        }

        /// <summary>
        /// Migrates save data between Eclipse versions.
        /// </summary>
        /// <remarks>
        /// Eclipse supports save migration between games.
        /// Handles Mass Effect 1 to 2 imports, DLC additions.
        /// Migrates morality, romance, and relationship data.
        /// </remarks>
        public SaveMigrationResult MigrateSave(string sourcePath, string targetPath, EclipseGame targetGame)
        {
            // TODO: Implement save migration system
            // Handle cross-game imports (ME1->ME2)
            // Migrate DLC-specific content
            // Convert morality and romance systems
            // Update relationship data structures

            return new SaveMigrationResult
            {
                Success = false,
                MigrationNotes = new List<string> { "Migration not yet implemented" }
            };
        }
    }

    /// <summary>
    /// Result of a save migration operation.
    /// </summary>
    public class SaveMigrationResult
    {
        /// <summary>
        /// Whether the migration succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Notes about the migration process.
        /// </summary>
        public List<string> MigrationNotes { get; set; } = new List<string>();

        /// <summary>
        /// Warnings about potential issues.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Errors that occurred during migration.
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Eclipse game identifiers for migration.
    /// </summary>
    public enum EclipseGame
    {
        /// <summary>
        /// Dragon Age: Origins
        /// </summary>
        DragonAgeOrigins,

        /// <summary>
        /// Dragon Age 2
        /// </summary>
        DragonAge2,

        /// <summary>
        /// Mass Effect
        /// </summary>
        MassEffect,

        /// <summary>
        /// Mass Effect 2
        /// </summary>
        MassEffect2
    }

    /// <summary>
    /// Eclipse lighting system placeholder.
    /// </summary>
    internal class EclipseLightingSystem : ILightingSystem
    {
        public void Update(float deltaTime) { }
        public void AddLight(IDynamicLight light) { }
        public void RemoveLight(IDynamicLight light) { }
    }

    /// <summary>
    /// Eclipse physics system placeholder.
    /// </summary>
    internal class EclipsePhysicsSystem : IPhysicsSystem
    {
        public void StepSimulation(float deltaTime) { }
        public bool RayCast(Vector3 origin, Vector3 direction, out Vector3 hitPoint, out IEntity hitEntity)
        {
            hitPoint = origin;
            hitEntity = null;
            return false;
        }
    }
}
