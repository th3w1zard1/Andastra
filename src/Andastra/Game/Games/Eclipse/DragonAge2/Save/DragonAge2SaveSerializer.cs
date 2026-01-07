using System;
using System.IO;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Engines.Eclipse.Save;

namespace Andastra.Runtime.Engines.Eclipse.DragonAge2.Save
{
    /// <summary>
    /// Save serializer for Dragon Age 2 (.das save files).
    /// </summary>
    /// <remarks>
    /// Dragon Age 2 Save Format:
    /// - Based on DragonAge2.exe: SaveGameMessage @ 0x00be37a8, DeleteSaveGameMessage @ 0x00be389c
    /// - Located via string references: "SaveGameMessage" @ 0x00be37a8, "GameModeController::HandleMessage(SaveGameMessage)" @ 0x00d2b330
    /// - Save file format: Binary format with signature "DA2S" (Dragon Age 2 Save)
    /// - Version: 1 (int32)
    /// - Structure: Signature (4 bytes) -> Version (4 bytes) -> Metadata -> Game State
    /// - Inheritance: Base class EclipseSaveSerializer (Runtime.Engines.Eclipse.Save) - abstract save serializer, DragonAge2 override - DA2 save format
    /// - Original implementation: UnrealScript message-based save system, binary serialization
    /// - Note: DA2 save format may differ from DA:O format (different game engine version)
    /// </remarks>
    public class DragonAge2SaveSerializer : EclipseSaveSerializer
    {
        private const string SaveSignature = "DA2S";
        private const int SaveVersion = 1;

        /// <summary>
        /// Serializes save metadata to NFO format (Dragon Age 2-specific).
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

                // Dragon Age 2-specific metadata fields
                // Based on DragonAge2.exe: SaveGameMessage @ 0x00be37a8, GameModeController::HandleMessage(SaveGameMessage) @ 0x00d2b330
                // Player character name
                WriteString(writer, saveData.PlayerName ?? "");

                // Current area name
                WriteString(writer, saveData.CurrentAreaName ?? "");

                // Party member count (from PartyState)
                int partyMemberCount = saveData.PartyState?.SelectedParty?.Count ?? 0;
                writer.Write(partyMemberCount);

                // Player level (from PartyState.PlayerCharacter)
                int playerLevel = saveData.PartyState?.PlayerCharacter?.Level ?? 0;
                writer.Write(playerLevel);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes save metadata from NFO format (Dragon Age 2-specific).
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
                ValidateVersion(reader, SaveVersion, "Dragon Age 2");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // Dragon Age 2-specific metadata fields
                // Based on DragonAge2.exe: SaveGameMessage structure
                saveData.PlayerName = ReadString(reader);
                saveData.CurrentAreaName = ReadString(reader);
                int partyMemberCount = reader.ReadInt32();
                int playerLevel = reader.ReadInt32();

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
            }

            return saveData;
        }

        /// <summary>
        /// Serializes full save archive (Dragon Age 2-specific).
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

                // Dragon Age 2-specific metadata
                WriteString(writer, saveData.PlayerName ?? "");
                WriteString(writer, saveData.CurrentAreaName ?? "");
                int partyMemberCount = saveData.PartyState?.SelectedParty?.Count ?? 0;
                writer.Write(partyMemberCount);
                int playerLevel = saveData.PartyState?.PlayerCharacter?.Level ?? 0;
                writer.Write(playerLevel);

                // Serialize full game state
                // Based on DragonAge2.exe: SaveGameMessage @ 0x00be37a8 serialization

                // Party state: Serialize party member data (using base class method)
                SerializeSelectedParty(writer, saveData.PartyState);

                // Inventory state: Serialize inventory data from PlayerCharacter (using base class method)
                SerializeBasicInventory(writer, saveData.PartyState?.PlayerCharacter?.Inventory);

                // Quest state: Serialize journal entries (using base class method)
                SerializeJournalEntries(writer, saveData.JournalEntries);

                // World state: Serialize global variables (using base class method)
                SerializeGlobalVariables(writer, saveData.GlobalVariables);

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes full save archive (Dragon Age 2-specific).
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
                ValidateVersion(reader, SaveVersion, "Dragon Age 2");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // Dragon Age 2-specific metadata
                saveData.PlayerName = ReadString(reader);
                saveData.CurrentAreaName = ReadString(reader);
                int partyMemberCount = reader.ReadInt32();
                int playerLevel = reader.ReadInt32();

                // Initialize PartyState if needed
                if (saveData.PartyState == null)
                {
                    saveData.PartyState = new Core.Save.PartyState();
                }
                if (saveData.PartyState.PlayerCharacter == null)
                {
                    saveData.PartyState.PlayerCharacter = new Core.Save.CreatureState();
                }
                saveData.PartyState.PlayerCharacter.Level = playerLevel;

                // Deserialize full game state
                // Based on DragonAge2.exe: SaveGameMessage deserialization

                // Party state: Deserialize party member data (using base class method)
                if (saveData.PartyState == null)
                {
                    saveData.PartyState = new Core.Save.PartyState();
                }
                DeserializeSelectedParty(reader, saveData.PartyState);

                // Inventory state: Deserialize inventory data (using base class method)
                if (saveData.PartyState.PlayerCharacter == null)
                {
                    saveData.PartyState.PlayerCharacter = new Core.Save.CreatureState();
                }
                saveData.PartyState.PlayerCharacter.Inventory = DeserializeBasicInventory(reader);

                // Quest state: Deserialize journal entries (using base class method)
                saveData.JournalEntries = DeserializeJournalEntries(reader);

                // World state: Deserialize global variables (using base class method)
                saveData.GlobalVariables = DeserializeGlobalVariables(reader);
            }
        }
    }
}

