using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.PCC
{
    /// <summary>
    /// Writes PCC/UPK (Unreal Engine 3 Package) files.
    /// </summary>
    /// <remarks>
    /// PCC/UPK Binary Writer:
    /// - Based on Unreal Engine 3 package format specification
    /// - Writes package header, name table, import table, export table
    /// - Creates package structure from PCC resources
    /// - Supports both PCC (cooked) and UPK (package) formats
    /// - Used by Eclipse Engine games (Dragon Age, )
    /// - Note: Full package writing is complex and may not preserve all Unreal Engine metadata
    /// </remarks>
    public class PCCBinaryWriter
    {
        private readonly PCC _pcc;

        public PCCBinaryWriter(PCC pcc)
        {
            _pcc = pcc ?? throw new ArgumentNullException(nameof(pcc));
        }

        public byte[] Write()
        {
            using (var ms = new MemoryStream())
            {
                Write(ms);
                return ms.ToArray();
            }
        }

        public void Write(Stream stream)
        {
            using (var writer = new System.IO.BinaryWriter(stream, Encoding.ASCII, true))
            {
                var resources = _pcc.GetResources();

                // Write package signature
                uint signature = _pcc.PackageType == PCCType.UPK ? 0x9E2A83C4 : 0x9E2A83C1;
                writer.Write(signature);

                // Write package versions
                writer.Write(_pcc.PackageVersion);
                writer.Write(_pcc.LicenseeVersion);
                writer.Write(_pcc.EngineVersion);
                writer.Write(_pcc.CookerVersion);

                // Build name table
                var nameTable = new List<string>();
                var nameMap = new Dictionary<string, int>();
                foreach (var resource in resources)
                {
                    string resName = resource.ResRef.ToString();
                    if (!nameMap.ContainsKey(resName))
                    {
                        nameMap[resName] = nameTable.Count;
                        nameTable.Add(resName);
                    }
                }

                // Calculate offsets
                int headerSize = 64; // Approximate header size
                int nameTableOffset = headerSize;
                int nameTableSize = CalculateNameTableSize(nameTable);
                int exportTableOffset = nameTableOffset + nameTableSize;
                int exportTableSize = resources.Count * 32; // Approximate export entry size
                int dataOffset = exportTableOffset + exportTableSize;

                // Write package header offsets
                writer.Write(nameTable.Count);
                writer.Write(nameTableOffset);
                writer.Write(resources.Count);
                writer.Write(exportTableOffset);
                writer.Write(0); // Import count
                writer.Write(0); // Import offset (no imports for now)
                writer.Write(0); // Depends offset
                writer.Write(0); // Depends count

                // Padding to header size
                int currentPos = (int)stream.Position;
                if (currentPos < headerSize)
                {
                    writer.Write(new byte[headerSize - currentPos]);
                }

                // Write name table
                stream.Position = nameTableOffset;
                foreach (string name in nameTable)
                {
                    byte[] nameBytes = Encoding.ASCII.GetBytes(name);
                    writer.Write(nameBytes.Length);
                    writer.Write(nameBytes);
                    writer.Write(0); // Name hash (simplified)
                }

                // Write export table and data
                stream.Position = exportTableOffset;
                int currentDataOffset = dataOffset;
                foreach (var resource in resources)
                {
                    int nameIndex = nameMap[resource.ResRef.ToString()];

                    // Write export entry
                    writer.Write(0); // ClassIndex (simplified)
                    writer.Write(0); // SuperIndex
                    writer.Write(0); // OuterIndex
                    writer.Write(nameIndex);
                    writer.Write(0); // ArchetypeIndex
                    writer.Write(0L); // ObjectFlags
                    writer.Write(resource.Data.Length);
                    writer.Write(currentDataOffset);

                    // Write resource data
                    long savePos = stream.Position;
                    stream.Position = currentDataOffset;
                    writer.Write(resource.Data);
                    currentDataOffset = (int)stream.Position;
                    stream.Position = savePos;
                }
            }
        }

        private int CalculateNameTableSize(List<string> names)
        {
            int size = 0;
            foreach (string name in names)
            {
                size += 4; // Length
                size += Encoding.ASCII.GetByteCount(name);
                size += 4; // Hash
            }
            return size;
        }
    }
}

