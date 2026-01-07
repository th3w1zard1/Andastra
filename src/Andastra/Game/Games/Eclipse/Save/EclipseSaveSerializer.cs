using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using Andastra.Runtime.Content.Save;
using Andastra.Runtime.Core.Save;

namespace Andastra.Runtime.Engines.Eclipse.Save
{
    /// <summary>
    /// Abstract base class for Eclipse Engine save serializer implementations.
    /// </summary>
    /// <remarks>
    /// Eclipse Save System Base:
    /// - Based on Eclipse/Unreal Engine save system
    /// - Eclipse uses UnrealScript message passing system - save format is different from Odyssey GFF/ERF
    /// - Architecture: Message-based (SaveGameMessage) vs Odyssey direct file I/O
    /// - Game-specific implementations: DragonAgeOriginsSaveSerializer, DragonAge2SaveSerializer, SaveSerializer, 2SaveSerializer
    /// - Common functionality: Binary serialization helpers, signature validation, version checking
    /// </remarks>
    public abstract class EclipseSaveSerializer : ISaveSerializer
    {
        public abstract byte[] SerializeSaveNfo(SaveGameData saveData);
        public abstract SaveGameData DeserializeSaveNfo(byte[] data);
        public abstract byte[] SerializeSaveArchive(SaveGameData saveData);
        public abstract void DeserializeSaveArchive(byte[] data, SaveGameData saveData);

        #region Common Binary Serialization Helpers

        /// <summary>
        /// Writes a string to a binary writer (length-prefixed UTF-8).
        /// Common across all Eclipse save formats.
        /// </summary>
        protected void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                value = "";
            }

            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// Reads a string from a binary reader (length-prefixed UTF-8).
        /// Common across all Eclipse save formats.
        /// </summary>
        protected string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > 65536) // Sanity check
            {
                throw new InvalidDataException($"Invalid string length: {length}");
            }

            byte[] bytes = reader.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Validates a save file signature.
        /// Common across all Eclipse save formats.
        /// </summary>
        protected void ValidateSignature(BinaryReader reader, string expectedSignature)
        {
            byte[] signature = reader.ReadBytes(4);
            string actualSignature = Encoding.UTF8.GetString(signature);
            if (actualSignature != expectedSignature)
            {
                throw new InvalidDataException($"Invalid save file signature. Expected '{expectedSignature}', got '{actualSignature}'");
            }
        }

        /// <summary>
        /// Validates a save file version.
        /// Common across all Eclipse save formats.
        /// </summary>
        protected void ValidateVersion(BinaryReader reader, int expectedVersion, string formatName)
        {
            int version = reader.ReadInt32();
            if (version != expectedVersion)
            {
                throw new NotSupportedException($"Unsupported {formatName} save version: {version} (expected {expectedVersion})");
            }
        }

        /// <summary>
        /// Writes save metadata fields common to all Eclipse games.
        /// </summary>
        protected void WriteCommonMetadata(BinaryWriter writer, SaveGameData saveData)
        {
            // Save name
            WriteString(writer, saveData.Name ?? "");

            // Module name
            WriteString(writer, saveData.CurrentModule ?? "");

            // Time played (seconds)
            writer.Write((int)saveData.PlayTime.TotalSeconds);

            // Timestamp (FileTime)
            writer.Write(saveData.SaveTime.ToFileTime());
        }

        /// <summary>
        /// Reads save metadata fields common to all Eclipse games.
        /// </summary>
        protected void ReadCommonMetadata(BinaryReader reader, SaveGameData saveData)
        {
            // Save name
            saveData.Name = ReadString(reader);

            // Module name
            saveData.CurrentModule = ReadString(reader);

            // Time played (seconds)
            int timePlayed = reader.ReadInt32();
            saveData.PlayTime = TimeSpan.FromSeconds(timePlayed);

            // Timestamp (FileTime)
            long fileTime = reader.ReadInt64();
            saveData.SaveTime = DateTime.FromFileTime(fileTime);
        }

        #endregion

        #region Common Game State Serialization Helpers

        /// <summary>
        /// Serializes global variable state (booleans, numbers, strings).
        /// Common across all Eclipse save formats.
        /// </summary>
        /// <remarks>
        /// Based on common global variable serialization patterns across:
        /// - Dragon Age: Origins (daorigins.exe)
        /// - Dragon Age 2 (DragonAge2.exe)
        /// -  ()
        /// -  2 ()
        /// </remarks>
        protected void SerializeGlobalVariables(BinaryWriter writer, GlobalVariableState globals)
        {
            if (globals == null)
            {
                // Write empty state
                writer.Write(0); // Boolean count
                writer.Write(0); // Number count
                writer.Write(0); // String count
                return;
            }

            // Serialize boolean globals
            writer.Write(globals.Booleans != null ? globals.Booleans.Count : 0);
            if (globals.Booleans != null)
            {
                foreach (var kvp in globals.Booleans)
                {
                    WriteString(writer, kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            // Serialize numeric globals
            writer.Write(globals.Numbers != null ? globals.Numbers.Count : 0);
            if (globals.Numbers != null)
            {
                foreach (var kvp in globals.Numbers)
                {
                    WriteString(writer, kvp.Key);
                    writer.Write(kvp.Value);
                }
            }

            // Serialize string globals
            writer.Write(globals.Strings != null ? globals.Strings.Count : 0);
            if (globals.Strings != null)
            {
                foreach (var kvp in globals.Strings)
                {
                    WriteString(writer, kvp.Key);
                    WriteString(writer, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Deserializes global variable state (booleans, numbers, strings).
        /// Common across all Eclipse save formats.
        /// </summary>
        protected GlobalVariableState DeserializeGlobalVariables(BinaryReader reader)
        {
            var globals = new GlobalVariableState();

            // Deserialize boolean globals
            int boolCount = reader.ReadInt32();
            for (int i = 0; i < boolCount; i++)
            {
                string name = ReadString(reader);
                bool value = reader.ReadBoolean();
                globals.Booleans[name] = value;
            }

            // Deserialize numeric globals
            int numberCount = reader.ReadInt32();
            for (int i = 0; i < numberCount; i++)
            {
                string name = ReadString(reader);
                int value = reader.ReadInt32();
                globals.Numbers[name] = value;
            }

            // Deserialize string globals
            int stringCount = reader.ReadInt32();
            for (int i = 0; i < stringCount; i++)
            {
                string name = ReadString(reader);
                string value = ReadString(reader);
                globals.Strings[name] = value;
            }

            return globals;
        }

        /// <summary>
        /// Serializes journal entries.
        /// Common across all Eclipse save formats.
        /// </summary>
        /// <remarks>
        /// Based on common journal/quest state serialization patterns across all Eclipse games.
        /// </remarks>
        protected void SerializeJournalEntries(BinaryWriter writer, List<JournalEntry> journalEntries)
        {
            int count = journalEntries != null ? journalEntries.Count : 0;
            writer.Write(count);
            if (journalEntries != null)
            {
                foreach (JournalEntry entry in journalEntries)
                {
                    WriteString(writer, entry.QuestTag ?? "");
                    writer.Write(entry.State);
                }
            }
        }

        /// <summary>
        /// Deserializes journal entries.
        /// Common across all Eclipse save formats.
        /// </summary>
        protected List<JournalEntry> DeserializeJournalEntries(BinaryReader reader)
        {
            var journalEntries = new List<JournalEntry>();

            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                journalEntries.Add(new JournalEntry
                {
                    QuestTag = ReadString(reader),
                    State = reader.ReadInt32()
                });
            }

            return journalEntries;
        }

        /// <summary>
        /// Serializes basic party state (selected party members list).
        /// Common helper used by Dragon Age games.
        /// </summary>
        /// <remarks>
        /// Based on Dragon Age: Origins and Dragon Age 2 party serialization.
        ///  uses a more comprehensive party state serialization.
        /// </remarks>
        protected void SerializeSelectedParty(BinaryWriter writer, PartyState partyState)
        {
            if (partyState != null && partyState.SelectedParty != null)
            {
                writer.Write(partyState.SelectedParty.Count);
                foreach (string partyMemberResRef in partyState.SelectedParty)
                {
                    WriteString(writer, partyMemberResRef ?? "");
                }
            }
            else
            {
                writer.Write(0);
            }
        }

        /// <summary>
        /// Deserializes basic party state (selected party members list).
        /// Common helper used by Dragon Age games.
        /// </summary>
        protected void DeserializeSelectedParty(BinaryReader reader, PartyState partyState)
        {
            if (partyState == null)
            {
                partyState = new PartyState();
            }

            int selectedPartyCount = reader.ReadInt32();
            if (selectedPartyCount > 0)
            {
                if (partyState.SelectedParty == null)
                {
                    partyState.SelectedParty = new List<string>();
                }
                for (int i = 0; i < selectedPartyCount; i++)
                {
                    partyState.SelectedParty.Add(ReadString(reader));
                }
            }
        }

        /// <summary>
        /// Serializes basic inventory items (template ResRef and stack size).
        /// Common helper used by Dragon Age games.
        /// </summary>
        /// <remarks>
        /// Based on Dragon Age: Origins and Dragon Age 2 inventory serialization.
        ///  uses a more comprehensive item state serialization.
        /// </remarks>
        protected void SerializeBasicInventory(BinaryWriter writer, List<ItemState> inventory)
        {
            int inventoryItemCount = inventory != null ? inventory.Count : 0;
            writer.Write(inventoryItemCount);
            if (inventory != null)
            {
                foreach (ItemState item in inventory)
                {
                    WriteString(writer, item?.TemplateResRef ?? "");
                    writer.Write(item?.StackSize ?? 1);
                }
            }
        }

        /// <summary>
        /// Deserializes basic inventory items (template ResRef and stack size).
        /// Common helper used by Dragon Age games.
        /// </summary>
        protected List<ItemState> DeserializeBasicInventory(BinaryReader reader)
        {
            var inventory = new List<ItemState>();

            int inventoryItemCount = reader.ReadInt32();
            for (int i = 0; i < inventoryItemCount; i++)
            {
                var item = new ItemState
                {
                    TemplateResRef = ReadString(reader),
                    StackSize = reader.ReadInt32()
                };
                inventory.Add(item);
            }

            return inventory;
        }

        #endregion
    }
}
