using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// TODO: Integrate with CSharpKOTOR GFF/ERF readers/writers
    /// </remarks>
    public class SaveSerializer : ISaveSerializer
    {
        // GFF field labels for save NFO
        private const string FIELD_SAVE_NAME = "SAVENAME";
        private const string FIELD_MODULE_NAME = "MODULENAME";
        private const string FIELD_SAVE_DATE = "SAVEDATE";
        private const string FIELD_SAVE_TIME = "SAVETIME";
        private const string FIELD_TIME_PLAYED = "TIMEPLAYED";
        private const string FIELD_PLAYER_NAME = "PLAYERNAME";
        private const string FIELD_PORTRAIT = "PORTRAIT";
        private const string FIELD_CHEAT_USED = "CHEATUSED";

        // ERF types
        private const string ERF_TYPE_SAV = "SAV ";

        #region ISaveSerializer Implementation

        public byte[] SerializeSaveNfo(SaveGameData saveData)
        {
            // TODO: Use CSharpKOTOR GFF writer
            // For now, create a placeholder implementation
            
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // GFF header signature
                writer.Write(Encoding.ASCII.GetBytes("GFF "));
                writer.Write(Encoding.ASCII.GetBytes("V3.2"));
                writer.Write((uint)0); // Type
                
                // Write field data as simple format
                // In real implementation, this would construct proper GFF structure
                WriteGffString(writer, FIELD_SAVE_NAME, saveData.Name ?? "");
                WriteGffString(writer, FIELD_MODULE_NAME, saveData.CurrentModule ?? "");
                WriteGffString(writer, FIELD_SAVE_DATE, saveData.SaveTime.ToString("yyyy-MM-dd"));
                WriteGffString(writer, FIELD_SAVE_TIME, saveData.SaveTime.ToString("HH:mm:ss"));
                WriteGffInt(writer, FIELD_TIME_PLAYED, (int)saveData.PlayTime.TotalSeconds);
                
                return ms.ToArray();
            }
        }

        public SaveGameData DeserializeSaveNfo(byte[] data)
        {
            // TODO: Use CSharpKOTOR GFF reader
            // For now, create a placeholder implementation

            var saveData = new SaveGameData();

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Verify GFF header
                string signature = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (signature != "GFF ")
                {
                    return null;
                }

                string version = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (!version.StartsWith("V3."))
                {
                    return null;
                }

                // Skip type
                reader.ReadUInt32();

                // Read fields
                // In real implementation, this would parse proper GFF structure
                saveData.Name = ReadGffString(reader, FIELD_SAVE_NAME);
                saveData.CurrentModule = ReadGffString(reader, FIELD_MODULE_NAME);
                
                string dateStr = ReadGffString(reader, FIELD_SAVE_DATE);
                string timeStr = ReadGffString(reader, FIELD_SAVE_TIME);
                
                DateTime saveTime;
                if (DateTime.TryParse(dateStr + " " + timeStr, out saveTime))
                {
                    saveData.SaveTime = saveTime;
                }
                
                int seconds = ReadGffInt(reader, FIELD_TIME_PLAYED);
                saveData.PlayTime = TimeSpan.FromSeconds(seconds);
            }

            return saveData;
        }

        public byte[] SerializeSaveArchive(SaveGameData saveData)
        {
            // TODO: Use CSharpKOTOR ERF writer
            // For now, create a placeholder ERF structure

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // ERF header
                writer.Write(Encoding.ASCII.GetBytes(ERF_TYPE_SAV));
                writer.Write(Encoding.ASCII.GetBytes("V1.0"));

                var resources = new List<KeyValuePair<string, byte[]>>();

                // Add GLOBALVARS.res
                byte[] globalVarsData = SerializeGlobalVariables(saveData.GlobalVariables);
                resources.Add(new KeyValuePair<string, byte[]>("GLOBALVARS", globalVarsData));

                // Add PARTYTABLE.res
                byte[] partyTableData = SerializePartyTable(saveData.PartyState);
                resources.Add(new KeyValuePair<string, byte[]>("PARTYTABLE", partyTableData));

                // Add module state files
                if (saveData.AreaStates != null)
                {
                    foreach (var kvp in saveData.AreaStates)
                    {
                        string areaResRef = kvp.Key;
                        AreaState areaState = kvp.Value;

                        string stateFileName = areaResRef + "_s";
                        byte[] stateData = SerializeAreaState(areaState);
                        resources.Add(new KeyValuePair<string, byte[]>(stateFileName, stateData));
                    }
                }

                // Write resource count
                writer.Write((uint)resources.Count);

                // Calculate offsets
                long keyListOffset = ms.Position + 12; // After header
                long resourceListOffset = keyListOffset + (resources.Count * 24); // 24 bytes per key
                long dataOffset = resourceListOffset + (resources.Count * 8); // 8 bytes per resource entry

                // Write offsets
                writer.Write((uint)keyListOffset);
                writer.Write((uint)resourceListOffset);

                // Write key list
                long currentDataOffset = dataOffset;
                foreach (var resource in resources)
                {
                    // ResRef (16 bytes, null-padded)
                    byte[] resRefBytes = new byte[16];
                    byte[] nameBytes = Encoding.ASCII.GetBytes(resource.Key);
                    Array.Copy(nameBytes, resRefBytes, Math.Min(nameBytes.Length, 16));
                    writer.Write(resRefBytes);

                    // Resource ID (4 bytes)
                    writer.Write((uint)0);

                    // Resource type (2 bytes) - using 0xFFFF for generic
                    writer.Write((ushort)0xFFFF);

                    // Unused (2 bytes)
                    writer.Write((ushort)0);
                }

                // Write resource entries
                currentDataOffset = dataOffset;
                foreach (var resource in resources)
                {
                    // Offset
                    writer.Write((uint)currentDataOffset);

                    // Size
                    writer.Write((uint)resource.Value.Length);

                    currentDataOffset += resource.Value.Length;
                }

                // Write resource data
                foreach (var resource in resources)
                {
                    writer.Write(resource.Value);
                }

                return ms.ToArray();
            }
        }

        public void DeserializeSaveArchive(byte[] data, SaveGameData saveData)
        {
            // TODO: Use CSharpKOTOR ERF reader
            // For now, create a placeholder implementation

            using (var ms = new MemoryStream(data))
            using (var reader = new BinaryReader(ms))
            {
                // Read ERF header
                string type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                string version = Encoding.ASCII.GetString(reader.ReadBytes(4));

                if (!version.StartsWith("V1."))
                {
                    return;
                }

                uint resourceCount = reader.ReadUInt32();
                uint keyListOffset = reader.ReadUInt32();
                uint resourceListOffset = reader.ReadUInt32();

                // Read key list
                ms.Seek(keyListOffset, SeekOrigin.Begin);
                var keys = new List<string>();
                for (int i = 0; i < resourceCount; i++)
                {
                    byte[] resRefBytes = reader.ReadBytes(16);
                    string resRef = Encoding.ASCII.GetString(resRefBytes).TrimEnd('\0');
                    keys.Add(resRef);
                    reader.ReadUInt32(); // Resource ID
                    reader.ReadUInt16(); // Type
                    reader.ReadUInt16(); // Unused
                }

                // Read resource entries
                ms.Seek(resourceListOffset, SeekOrigin.Begin);
                var entries = new List<Tuple<uint, uint>>();
                for (int i = 0; i < resourceCount; i++)
                {
                    uint offset = reader.ReadUInt32();
                    uint size = reader.ReadUInt32();
                    entries.Add(Tuple.Create(offset, size));
                }

                // Read resources
                for (int i = 0; i < resourceCount; i++)
                {
                    string resRef = keys[i];
                    uint offset = entries[i].Item1;
                    uint size = entries[i].Item2;

                    ms.Seek(offset, SeekOrigin.Begin);
                    byte[] resourceData = reader.ReadBytes((int)size);

                    if (resRef.Equals("GLOBALVARS", StringComparison.OrdinalIgnoreCase))
                    {
                        saveData.GlobalVariables = DeserializeGlobalVariables(resourceData);
                    }
                    else if (resRef.Equals("PARTYTABLE", StringComparison.OrdinalIgnoreCase))
                    {
                        saveData.PartyState = DeserializePartyTable(resourceData);
                    }
                    else if (resRef.EndsWith("_s", StringComparison.OrdinalIgnoreCase))
                    {
                        string areaResRef = resRef.Substring(0, resRef.Length - 2);
                        var areaState = DeserializeAreaState(resourceData);
                        if (areaState != null)
                        {
                            saveData.AreaStates[areaResRef] = areaState;
                        }
                    }
                }
            }
        }

        #endregion

        #region Global Variables

        private byte[] SerializeGlobalVariables(GlobalVariableState state)
        {
            if (state == null)
            {
                return new byte[0];
            }

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // GFF header
                writer.Write(Encoding.ASCII.GetBytes("GFF "));
                writer.Write(Encoding.ASCII.GetBytes("V3.2"));
                writer.Write((uint)0); // Type

                // Write boolean count and values
                writer.Write((uint)state.Booleans.Count);
                foreach (var kvp in state.Booleans)
                {
                    WriteGffString(writer, kvp.Key, kvp.Value.ToString());
                }

                // Write number count and values
                writer.Write((uint)state.Numbers.Count);
                foreach (var kvp in state.Numbers)
                {
                    WriteGffString(writer, kvp.Key, "");
                    writer.Write(kvp.Value);
                }

                // Write string count and values
                writer.Write((uint)state.Strings.Count);
                foreach (var kvp in state.Strings)
                {
                    WriteGffString(writer, kvp.Key, kvp.Value ?? "");
                }

                return ms.ToArray();
            }
        }

        private GlobalVariableState DeserializeGlobalVariables(byte[] data)
        {
            var state = new GlobalVariableState();

            if (data == null || data.Length < 12)
            {
                return state;
            }

            // TODO: Proper GFF parsing with CSharpKOTOR
            return state;
        }

        #endregion

        #region Party Table

        private byte[] SerializePartyTable(PartyState state)
        {
            if (state == null)
            {
                return new byte[0];
            }

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // GFF header
                writer.Write(Encoding.ASCII.GetBytes("GFF "));
                writer.Write(Encoding.ASCII.GetBytes("V3.2"));
                writer.Write((uint)0); // Type

                // Write gold
                writer.Write(state.Gold);

                // Write XP
                writer.Write(state.ExperiencePoints);

                // Write selected party count
                writer.Write((uint)state.SelectedParty.Count);
                foreach (string member in state.SelectedParty)
                {
                    WriteGffString(writer, "MEMBER", member);
                }

                // Write available members count
                writer.Write((uint)state.AvailableMembers.Count);
                foreach (var kvp in state.AvailableMembers)
                {
                    WriteGffString(writer, "RESREF", kvp.Key);
                    writer.Write(kvp.Value.IsAvailable ? (byte)1 : (byte)0);
                    writer.Write(kvp.Value.IsSelectable ? (byte)1 : (byte)0);
                }

                return ms.ToArray();
            }
        }

        private PartyState DeserializePartyTable(byte[] data)
        {
            var state = new PartyState();

            if (data == null || data.Length < 12)
            {
                return state;
            }

            // TODO: Proper GFF parsing with CSharpKOTOR
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
            using (var writer = new BinaryWriter(ms))
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

            // TODO: Proper GFF parsing with CSharpKOTOR
            return state;
        }

        private void SerializeEntityStates(BinaryWriter writer, string label, List<EntityState> states)
        {
            writer.Write((uint)states.Count);
            foreach (var entityState in states)
            {
                SerializeEntityState(writer, entityState);
            }
        }

        private void SerializeEntityState(BinaryWriter writer, EntityState state)
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

        private void WriteGffString(BinaryWriter writer, string label, string value)
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

        private void WriteGffInt(BinaryWriter writer, string label, int value)
        {
            byte[] labelBytes = Encoding.ASCII.GetBytes(label);
            writer.Write((byte)labelBytes.Length);
            writer.Write(labelBytes);
            writer.Write(value);
        }

        private string ReadGffString(BinaryReader reader, string expectedLabel)
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

        private int ReadGffInt(BinaryReader reader, string expectedLabel)
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
