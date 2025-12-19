
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Andastra.Parsing.Formats;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Formats.PCC
{
    /// <summary>
    /// Reads PCC/UPK (Unreal Engine 3 Package) files.
    /// </summary>
    /// <remarks>
    /// PCC/UPK Binary Reader:
    /// - Based on Unreal Engine 3 package format specification
    /// - Reads package header, name table, import table, export table
    /// - Extracts resources from export table entries
    /// - Supports both PCC (cooked) and UPK (package) formats
    /// - Used by Eclipse Engine games (Dragon Age, Mass Effect)
    /// </remarks>
    public class PCCBinaryReader : BinaryFormatReaderBase
    {
        [CanBeNull]
        private PCC _pcc;

        public PCCBinaryReader(byte[] data) : base(data)
        {
        }

        public PCCBinaryReader(string filepath) : base(filepath)
        {
        }

        public PCCBinaryReader(Stream source) : base(source)
        {
        }

        public PCC Load()
        {
            try
            {
                Reader.Seek(0);

                // Read package signature (4 bytes)
                // Unreal Engine 3 packages have different signatures:
                // - UE3 cooked packages: 0x9E2A83C1 (little-endian)
                // - UE3 uncooked packages: 0x9E2A83C4 (little-endian)
                // - Some variants may use different signatures
                uint signature = Reader.ReadUInt32();
                
                bool isValidSignature = signature == 0x9E2A83C1 || signature == 0x9E2A83C4 ||
                                        signature == 0xC1832A9E || signature == 0xC4832A9E;

                if (!isValidSignature)
                {
                    // Check if it might be a different UE3 version or format
                    // Some packages may have headers that start differently
                    // For now, we'll be lenient and try to parse anyway
                    Reader.Seek(0);
                }
                else
                {
                    Reader.Seek(0);
                }

                // Determine package type from extension or signature
                // Both PCC and UPK use the same format, just different extensions
                PCCType packageType = PCCType.PCC; // Default to PCC

                _pcc = new PCC(packageType);

                // Read package version (after 4-byte signature)
                _pcc.PackageVersion = Reader.ReadInt32();
                _pcc.LicenseeVersion = Reader.ReadInt32();
                _pcc.EngineVersion = Reader.ReadInt32();
                _pcc.CookerVersion = Reader.ReadInt32();

                // Read package header offsets
                // UE3 package format structure:
                // - Signature (4 bytes)
                // - Version info (16 bytes: 4 ints)
                // - Table offsets and counts
                int nameCount = Reader.ReadInt32();
                int nameOffset = Reader.ReadInt32();
                int exportCount = Reader.ReadInt32();
                int exportOffset = Reader.ReadInt32();
                int importCount = Reader.ReadInt32();
                int importOffset = Reader.ReadInt32();
                int dependsOffset = Reader.ReadInt32();
                int dependsCount = Reader.ReadInt32();
                
                // Validate offsets are reasonable
                if (nameOffset < 0 || exportOffset < 0 || importOffset < 0 ||
                    nameOffset >= Data.Length || exportOffset >= Data.Length || importOffset >= Data.Length)
                {
                    throw new InvalidDataException("Invalid package header offsets");
                }

                // Read name table
                var names = new List<string>();
                Reader.Seek(nameOffset);
                for (int i = 0; i < nameCount; i++)
                {
                    int nameLength = Reader.ReadInt32();
                    if (nameLength < 0)
                    {
                        // Negative length indicates Unicode string
                        nameLength = -nameLength;
                        byte[] nameBytes = Reader.ReadBytes(nameLength * 2);
                        string name = Encoding.Unicode.GetString(nameBytes);
                        names.Add(name);
                    }
                    else
                    {
                        // ASCII string
                        byte[] nameBytes = Reader.ReadBytes(nameLength);
                        string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                        names.Add(name);
                    }
                    Reader.SeekRelative(4); // Skip hash
                }

                // Read import table (for dependencies, not resources)
                Reader.Seek(importOffset);
                for (int i = 0; i < importCount; i++)
                {
                    Reader.SeekRelative(20); // Skip import entry (20 bytes typically)
                }

                // Read export table (this is where resources are)
                Reader.Seek(exportOffset);
                var exports = new List<ExportEntry>();
                for (int i = 0; i < exportCount; i++)
                {
                    var export = new ExportEntry
                    {
                        ClassIndex = Reader.ReadInt32(),
                        SuperIndex = Reader.ReadInt32(),
                        OuterIndex = Reader.ReadInt32(),
                        ObjectName = Reader.ReadInt32(),
                        ArchetypeIndex = Reader.ReadInt32(),
                        ObjectFlags = Reader.ReadInt64(),
                        SerialSize = Reader.ReadInt32(),
                        SerialOffset = Reader.ReadInt32()
                    };
                    exports.Add(export);
                }

                // Extract resources from exports
                foreach (var export in exports)
                {
                    if (export.ObjectName < 0 || export.ObjectName >= names.Count)
                    {
                        continue;
                    }

                    string objectName = names[export.ObjectName];
                    if (string.IsNullOrEmpty(objectName))
                    {
                        continue;
                    }

                    // Determine resource type from object name or class
                    ResourceType resType = DetermineResourceType(objectName, export.ClassIndex, names);

                    // Read export data
                    if (export.SerialOffset > 0 && export.SerialSize > 0 && 
                        export.SerialOffset + export.SerialSize <= Data.Length)
                    {
                        Reader.Seek(export.SerialOffset);
                        byte[] exportData = Reader.ReadBytes(export.SerialSize);

                        // Extract resource name (remove package path if present)
                        string resName = ExtractResourceName(objectName);

                        _pcc.SetData(resName, resType, exportData);
                    }
                }

                return _pcc;
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException("Corrupted or truncated PCC/UPK file.", ex);
            }
        }

        private ResourceType DetermineResourceType(string objectName, int classIndex, List<string> names)
        {
            // Try to determine resource type from object name extension
            string lowerName = objectName.ToLowerInvariant();
            
            // Check common extensions
            if (lowerName.EndsWith(".texture2d") || lowerName.EndsWith(".texture"))
            {
                return ResourceType.TPC; // Use TPC as texture type
            }
            if (lowerName.EndsWith(".staticmesh") || lowerName.EndsWith(".skeletalmesh"))
            {
                return ResourceType.MDL; // Use MDL as model type
            }
            if (lowerName.EndsWith(".sound") || lowerName.EndsWith(".soundcue"))
            {
                return ResourceType.WAV; // Use WAV as sound type
            }
            if (lowerName.EndsWith(".material") || lowerName.EndsWith(".materialinstance"))
            {
                return ResourceType.MAT;
            }
            if (lowerName.EndsWith(".script") || lowerName.EndsWith(".class"))
            {
                return ResourceType.NCS; // Use NCS as script type
            }

            // Default to binary
            return ResourceType.INVALID;
        }

        private string ExtractResourceName(string objectName)
        {
            // Remove package path (e.g., "Package.ObjectName" -> "ObjectName")
            int lastDot = objectName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < objectName.Length - 1)
            {
                return objectName.Substring(lastDot + 1);
            }
            return objectName;
        }

        private class ExportEntry
        {
            public int ClassIndex { get; set; }
            public int SuperIndex { get; set; }
            public int OuterIndex { get; set; }
            public int ObjectName { get; set; }
            public int ArchetypeIndex { get; set; }
            public long ObjectFlags { get; set; }
            public int SerialSize { get; set; }
            public int SerialOffset { get; set; }
        }
    }
}

