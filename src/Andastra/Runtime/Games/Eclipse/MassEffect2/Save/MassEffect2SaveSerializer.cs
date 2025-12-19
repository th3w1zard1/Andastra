using System;
using System.IO;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Engines.Eclipse.Save;

namespace Andastra.Runtime.Engines.Eclipse.MassEffect2.Save
{
    /// <summary>
    /// Save serializer for Mass Effect 2 (.pcsave save files).
    /// </summary>
    /// <remarks>
    /// Mass Effect 2 Save Format:
    /// - Based on MassEffect2.exe: Save system (similar to ME1 but with differences)
    /// - Located via string references: Save system functions
    /// - Save file format: Binary format with signature "MES2" (Mass Effect Save 2)
    /// - Version: 1 (int32)
    /// - Structure: Signature (4 bytes) -> Version (4 bytes) -> Metadata -> Game State
    /// - Inheritance: Base class EclipseSaveSerializer (Runtime.Engines.Eclipse.Save) - abstract save serializer, MassEffect2 override - ME2 save format
    /// - Original implementation: UnrealScript message-based save system, binary serialization
    /// - Note: Mass Effect 2 uses .pcsave file extension, format may differ from ME1
    /// </remarks>
    public class MassEffect2SaveSerializer : EclipseSaveSerializer
    {
        private const string SaveSignature = "MES2";
        private const int SaveVersion = 1;

        /// <summary>
        /// Serializes save metadata to NFO format (Mass Effect 2-specific).
        /// </summary>
        public override byte[] SerializeSaveNfo(SaveGameData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Write signature
                writer.Write(System.Text.Encoding.UTF8.GetBytes(SaveSignature));

                // Write version
                writer.Write(SaveVersion);

                // Write common metadata
                WriteCommonMetadata(writer, saveData);

                // Mass Effect 2-specific metadata fields
                // Based on MassEffect2.exe: Save system structure
                // Serializes metadata for save list display and quick loading
                
                // Player character name
                WriteString(writer, saveData.PlayerName ?? "");
                
                // Current area name
                WriteString(writer, saveData.CurrentAreaName ?? "");
                
                // Player level (from PartyState.PlayerCharacter)
                int playerLevel = saveData.PartyState?.PlayerCharacter?.Level ?? 0;
                writer.Write(playerLevel);
                
                // Squad member count (Mass Effect 2 uses "squad" instead of "party")
                int squadMemberCount = saveData.PartyState?.SelectedParty?.Count ?? 0;
                writer.Write(squadMemberCount);
                
                // Character class (Mass Effect 2 classes: Soldier, Adept, Engineer, Sentinel, Infiltrator, Vanguard)
                // Extract from PartyState.PlayerCharacter.ClassLevels - get the primary class (highest level)
                string characterClass = GetCharacterClassName(saveData.PartyState?.PlayerCharacter);
                WriteString(writer, characterClass);
                
                // Mission count (number of active missions/quests)
                int missionCount = saveData.JournalEntries != null ? saveData.JournalEntries.Count : 0;
                writer.Write(missionCount);
                
                // Difficulty level
                // Mass Effect 2 has difficulty settings: 0=Casual, 1=Normal, 2=Veteran, 3=Hardcore, 4=Insanity
                // Note: Difficulty is not currently stored in SaveGameData structure, so we write 0 as default
                // If difficulty tracking is needed, add DifficultyLevel field to SaveGameData
                int difficultyLevel = 0;
                writer.Write(difficultyLevel);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes save metadata from NFO format (Mass Effect 2-specific).
        /// </summary>
        public override SaveGameData DeserializeSaveNfo(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Save data cannot be null or empty", nameof(data));
            }

            var saveData = new SaveGameData();

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                // Validate signature
                ValidateSignature(reader, SaveSignature);

                // Validate version
                ValidateVersion(reader, SaveVersion, "Mass Effect 2");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // Mass Effect 2-specific metadata fields
                // Based on MassEffect2.exe: Save system structure
                // Deserializes metadata for save list display and quick loading
                
                // Player character name
                saveData.PlayerName = ReadString(reader);
                
                // Current area name
                saveData.CurrentAreaName = ReadString(reader);
                
                // Player level
                int playerLevel = reader.ReadInt32();
                
                // Squad member count
                int squadMemberCount = reader.ReadInt32();
                
                // Character class
                string characterClass = ReadString(reader);
                
                // Mission count
                int missionCount = reader.ReadInt32();
                
                // Difficulty level
                int difficultyLevel = reader.ReadInt32();
                
                // Initialize PartyState if needed
                if (saveData.PartyState == null)
                {
                    saveData.PartyState = new Core.Save.PartyState();
                }
                
                // Set player level if PlayerCharacter exists
                if (saveData.PartyState.PlayerCharacter == null)
                {
                    saveData.PartyState.PlayerCharacter = new Core.Save.CreatureState();
                }
                saveData.PartyState.PlayerCharacter.Level = playerLevel;
                
                // Initialize SelectedParty if needed and set count (actual members loaded in full deserialization)
                if (saveData.PartyState.SelectedParty == null)
                {
                    saveData.PartyState.SelectedParty = new System.Collections.Generic.List<string>();
                }
                
                // Initialize JournalEntries if needed (count is known from metadata)
                if (saveData.JournalEntries == null)
                {
                    saveData.JournalEntries = new System.Collections.Generic.List<Core.Save.JournalEntry>();
                }
            }

            return saveData;
        }

        /// <summary>
        /// Serializes full save archive (Mass Effect 2-specific).
        /// </summary>
        public override byte[] SerializeSaveArchive(SaveGameData saveData)
        {
            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // Write signature
                writer.Write(System.Text.Encoding.UTF8.GetBytes(SaveSignature));

                // Write version
                writer.Write(SaveVersion);

                // Write common metadata
                WriteCommonMetadata(writer, saveData);

                // TODO: Serialize full game state
                // Based on MassEffect2.exe: Save system serialization
                // Includes: Squad state, inventory, missions, world state, etc.

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes full save archive (Mass Effect 2-specific).
        /// </summary>
        public override void DeserializeSaveArchive(byte[] data, SaveGameData saveData)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Save data cannot be null or empty", nameof(data));
            }

            if (saveData == null)
            {
                throw new ArgumentNullException(nameof(saveData));
            }

            using (var stream = new MemoryStream(data))
            using (var reader = new BinaryReader(stream))
            {
                // Validate signature
                ValidateSignature(reader, SaveSignature);

                // Validate version
                ValidateVersion(reader, SaveVersion, "Mass Effect 2");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // TODO: Deserialize full game state
            }
        }

        #region Helper Methods

        /// <summary>
        /// Gets the character class name from CreatureState.ClassLevels.
        /// Based on Mass Effect 2 class system: extracts primary class (highest level) from ClassLevels.
        /// </summary>
        /// <param name="creature">The creature state to extract class from.</param>
        /// <returns>Class name string, or empty string if no class information available.</returns>
        /// <remarks>
        /// Mass Effect 2 Class IDs:
        /// - 0 or 1: Soldier
        /// - 2: Adept
        /// - 3: Engineer
        /// - 4: Sentinel
        /// - 5: Infiltrator
        /// - 6: Vanguard
        /// </remarks>
        private string GetCharacterClassName(Core.Save.CreatureState creature)
        {
            if (creature == null || creature.ClassLevels == null || creature.ClassLevels.Count == 0)
            {
                return "";
            }

            // Find the class with the highest level (primary class)
            Core.Save.ClassLevel primaryClass = null;
            int maxLevel = -1;
            foreach (var classLevel in creature.ClassLevels)
            {
                if (classLevel.Level > maxLevel)
                {
                    maxLevel = classLevel.Level;
                    primaryClass = classLevel;
                }
            }

            if (primaryClass == null)
            {
                return "";
            }

            // Convert ClassId to class name
            return GetClassNameFromId(primaryClass.ClassId);
        }

        /// <summary>
        /// Converts a Mass Effect 2 class ID to class name string.
        /// Based on MassEffect2.exe: Class ID to name mapping.
        /// </summary>
        /// <param name="classId">The class ID to convert.</param>
        /// <returns>Class name string corresponding to the ID.</returns>
        /// <remarks>
        /// Mass Effect 2 Class ID mapping:
        /// - 0, 1: Soldier
        /// - 2: Adept
        /// - 3: Engineer
        /// - 4: Sentinel
        /// - 5: Infiltrator
        /// - 6: Vanguard
        /// </remarks>
        private string GetClassNameFromId(int classId)
        {
            switch (classId)
            {
                case 0:
                case 1:
                    return "Soldier";
                case 2:
                    return "Adept";
                case 3:
                    return "Engineer";
                case 4:
                    return "Sentinel";
                case 5:
                    return "Infiltrator";
                case 6:
                    return "Vanguard";
                default:
                    // Unknown class ID - return as string for extensibility
                    return $"Class_{classId}";
            }
        }

        #endregion
    }
}

