using System;
using System.IO;
using Andastra.Runtime.Core.Save;
using Andastra.Runtime.Engines.Eclipse.Save;

namespace Andastra.Runtime.Engines.Eclipse.DragonAgeOrigins.Save
{
    /// <summary>
    /// Save serializer for Dragon Age: Origins (.das save files).
    /// </summary>
    /// <remarks>
    /// Dragon Age: Origins Save Format:
    /// - Based on daorigins.exe: SaveGameMessage @ 0x00ae6276, COMMAND_SAVEGAME @ 0x00af15d4
    /// - Located via string references: "SaveGameMessage" @ 0x00ae6276, "COMMAND_SAVEGAME" @ 0x00af15d4
    /// - Save file format: Binary format with signature "DAS " (Dragon Age Save)
    /// - Version: 1 (int32)
    /// - Structure: Signature (4 bytes) -> Version (4 bytes) -> Metadata -> Game State
    /// - Inheritance: Base class EclipseSaveSerializer (Runtime.Engines.Eclipse.Save) - abstract save serializer, DragonAgeOrigins override - .das format
    /// - Original implementation: UnrealScript message-based save system, binary serialization
    /// </remarks>
    public class DragonAgeOriginsSaveSerializer : EclipseSaveSerializer
    {
        private const string SaveSignature = "DAS ";
        private const int SaveVersion = 1;

        /// <summary>
        /// Serializes save metadata to NFO format (Dragon Age: Origins-specific).
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

                // Dragon Age: Origins-specific metadata fields
                // Based on daorigins.exe: SaveGameMessage @ 0x00ae6276, COMMAND_SAVEGAME @ 0x00af15d4
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
        /// Deserializes save metadata from NFO format (Dragon Age: Origins-specific).
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
                ValidateVersion(reader, SaveVersion, "Dragon Age: Origins");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // Dragon Age: Origins-specific metadata fields
                // Based on daorigins.exe: SaveGameMessage structure
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
        /// Serializes full save archive (Dragon Age: Origins-specific).
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

                // Dragon Age: Origins-specific metadata
                WriteString(writer, saveData.PlayerName ?? "");
                WriteString(writer, saveData.CurrentAreaName ?? "");
                int partyMemberCount = saveData.PartyState?.SelectedParty?.Count ?? 0;
                writer.Write(partyMemberCount);
                int playerLevel = saveData.PartyState?.PlayerCharacter?.Level ?? 0;
                writer.Write(playerLevel);

                // Serialize full game state
                // Based on daorigins.exe: SaveGameMessage @ 0x00ae6276 serialization
                
                // Party state: Serialize party member data
                if (saveData.PartyState != null && saveData.PartyState.SelectedParty != null)
                {
                    writer.Write(saveData.PartyState.SelectedParty.Count);
                    foreach (var partyMemberResRef in saveData.PartyState.SelectedParty)
                    {
                        WriteString(writer, partyMemberResRef ?? "");
                    }
                }
                else
                {
                    writer.Write(0);
                }
                
                // Inventory state: Serialize inventory data from PlayerCharacter
                int inventoryItemCount = 0;
                if (saveData.PartyState?.PlayerCharacter?.Inventory != null)
                {
                    inventoryItemCount = saveData.PartyState.PlayerCharacter.Inventory.Count;
                }
                writer.Write(inventoryItemCount);
                if (saveData.PartyState?.PlayerCharacter?.Inventory != null)
                {
                    foreach (var item in saveData.PartyState.PlayerCharacter.Inventory)
                    {
                        WriteString(writer, item?.TemplateResRef ?? "");
                        writer.Write(item?.StackSize ?? 1);
                    }
                }
                
                // Quest state: Serialize journal entries
                int journalEntryCount = saveData.JournalEntries != null ? saveData.JournalEntries.Count : 0;
                writer.Write(journalEntryCount);
                if (saveData.JournalEntries != null)
                {
                    foreach (var entry in saveData.JournalEntries)
                    {
                        WriteString(writer, entry?.QuestTag ?? "");
                        writer.Write(entry?.State ?? 0);
                    }
                }
                
                // World state: Serialize global variables
                if (saveData.GlobalVariables != null)
                {
                    // Serialize boolean globals
                    writer.Write(saveData.GlobalVariables.Booleans != null ? saveData.GlobalVariables.Booleans.Count : 0);
                    if (saveData.GlobalVariables.Booleans != null)
                    {
                        foreach (var kvp in saveData.GlobalVariables.Booleans)
                        {
                            WriteString(writer, kvp.Key ?? "");
                            writer.Write(kvp.Value);
                        }
                    }
                    
                    // Serialize numeric globals
                    writer.Write(saveData.GlobalVariables.Numbers != null ? saveData.GlobalVariables.Numbers.Count : 0);
                    if (saveData.GlobalVariables.Numbers != null)
                    {
                        foreach (var kvp in saveData.GlobalVariables.Numbers)
                        {
                            WriteString(writer, kvp.Key ?? "");
                            writer.Write(kvp.Value);
                        }
                    }
                    
                    // Serialize string globals
                    writer.Write(saveData.GlobalVariables.Strings != null ? saveData.GlobalVariables.Strings.Count : 0);
                    if (saveData.GlobalVariables.Strings != null)
                    {
                        foreach (var kvp in saveData.GlobalVariables.Strings)
                        {
                            WriteString(writer, kvp.Key ?? "");
                            WriteString(writer, kvp.Value ?? "");
                        }
                    }
                }
                else
                {
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(0);
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes full save archive (Dragon Age: Origins-specific).
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
                ValidateVersion(reader, SaveVersion, "Dragon Age: Origins");

                // Read common metadata
                ReadCommonMetadata(reader, saveData);

                // Dragon Age: Origins-specific metadata
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
                // Based on daorigins.exe: SaveGameMessage deserialization
                
                // Party state: Deserialize party member data
                int selectedPartyCount = reader.ReadInt32();
                if (selectedPartyCount > 0)
                {
                    if (saveData.PartyState.SelectedParty == null)
                    {
                        saveData.PartyState.SelectedParty = new System.Collections.Generic.List<string>();
                    }
                    for (int i = 0; i < selectedPartyCount; i++)
                    {
                        saveData.PartyState.SelectedParty.Add(ReadString(reader));
                    }
                }
                
                // Inventory state: Deserialize inventory data
                int inventoryItemCount = reader.ReadInt32();
                if (inventoryItemCount > 0)
                {
                    if (saveData.PartyState.PlayerCharacter.Inventory == null)
                    {
                        saveData.PartyState.PlayerCharacter.Inventory = new System.Collections.Generic.List<Core.Save.ItemState>();
                    }
                    for (int i = 0; i < inventoryItemCount; i++)
                    {
                        var item = new Core.Save.ItemState();
                        item.TemplateResRef = ReadString(reader);
                        item.StackSize = reader.ReadInt32();
                        saveData.PartyState.PlayerCharacter.Inventory.Add(item);
                    }
                }
                
                // Quest state: Deserialize journal entries
                int journalEntryCount = reader.ReadInt32();
                if (journalEntryCount > 0)
                {
                    if (saveData.JournalEntries == null)
                    {
                        saveData.JournalEntries = new System.Collections.Generic.List<Core.Save.JournalEntry>();
                    }
                    for (int i = 0; i < journalEntryCount; i++)
                    {
                        var entry = new Core.Save.JournalEntry();
                        entry.QuestTag = ReadString(reader);
                        entry.State = reader.ReadInt32();
                        saveData.JournalEntries.Add(entry);
                    }
                }
                
                // World state: Deserialize global variables
                if (saveData.GlobalVariables == null)
                {
                    saveData.GlobalVariables = new Core.Save.GlobalVariableState();
                }
                
                // Deserialize boolean globals
                int boolGlobalCount = reader.ReadInt32();
                for (int i = 0; i < boolGlobalCount; i++)
                {
                    string key = ReadString(reader);
                    bool value = reader.ReadBoolean();
                    saveData.GlobalVariables.Booleans[key] = value;
                }
                
                // Deserialize numeric globals
                int numGlobalCount = reader.ReadInt32();
                for (int i = 0; i < numGlobalCount; i++)
                {
                    string key = ReadString(reader);
                    int value = reader.ReadInt32();
                    saveData.GlobalVariables.Numbers[key] = value;
                }
                
                // Deserialize string globals
                int strGlobalCount = reader.ReadInt32();
                for (int i = 0; i < strGlobalCount; i++)
                {
                    string key = ReadString(reader);
                    string value = ReadString(reader);
                    saveData.GlobalVariables.Strings[key] = value;
                }
            }
        }
    }
}

