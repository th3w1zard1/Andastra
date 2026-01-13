
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.PCC
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
    /// - Used by Eclipse Engine games (Dragon Age, )
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
                // Validate minimum file size (must have at least signature + version info)
                if (Data.Length < 20)
                {
                    throw new InvalidDataException(
                        $"PCC/UPK file is too small. Expected at least 20 bytes (signature + version info), got {Data.Length} bytes.");
                }

                Reader.Seek(0);

                // Read package signature (4 bytes)
                // Unreal Engine 3 packages have different signatures:
                // - UE3 cooked packages: 0x9E2A83C1 (little-endian) - PCC format
                // - UE3 uncooked packages: 0x9E2A83C4 (little-endian) - UPK format
                // - Big-endian variants: 0xC1832A9E (cooked), 0xC4832A9E (uncooked)
                // Based on Unreal Engine 3 package format specification
                uint signature = Reader.ReadUInt32();

                // Validate signature and determine package type
                PCCType packageType;
                bool isValidSignature = ValidateSignature(signature, out packageType);

                if (!isValidSignature)
                {
                    // Perform additional validation checks to provide better diagnostics
                    string diagnosticInfo = GenerateSignatureDiagnostics(signature);

                    throw new InvalidDataException(
                        $"Invalid PCC/UPK package signature. Expected one of: 0x9E2A83C1 (PCC cooked), " +
                        $"0x9E2A83C4 (UPK uncooked), 0xC1832A9E (PCC big-endian), or 0xC4832A9E (UPK big-endian). " +
                        $"Got: 0x{signature:X8}. {diagnosticInfo}");
                }

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

        /// <summary>
        /// Validates the package signature and determines the package type.
        /// </summary>
        /// <param name="signature">The 4-byte signature read from the file.</param>
        /// <param name="packageType">Output parameter for the detected package type.</param>
        /// <returns>True if the signature is valid, false otherwise.</returns>
        /// <remarks>
        /// Valid Unreal Engine 3 package signatures:
        /// - 0x9E2A83C1: UE3 cooked package (little-endian) - PCC format
        /// - 0x9E2A83C4: UE3 uncooked package (little-endian) - UPK format
        /// - 0xC1832A9E: UE3 cooked package (big-endian) - PCC format
        /// - 0xC4832A9E: UE3 uncooked package (big-endian) - UPK format
        /// Based on Unreal Engine 3 package format specification and Eclipse Engine implementation.
        /// </remarks>
        private bool ValidateSignature(uint signature, out PCCType packageType)
        {
            // Check for little-endian signatures (most common)
            if (signature == 0x9E2A83C1)
            {
                packageType = PCCType.PCC; // Cooked package
                return true;
            }
            if (signature == 0x9E2A83C4)
            {
                packageType = PCCType.UPK; // Uncooked package
                return true;
            }

            // Check for big-endian signatures (less common, but valid)
            if (signature == 0xC1832A9E)
            {
                packageType = PCCType.PCC; // Cooked package (big-endian)
                return true;
            }
            if (signature == 0xC4832A9E)
            {
                packageType = PCCType.UPK; // Uncooked package (big-endian)
                return true;
            }

            // Invalid signature
            packageType = PCCType.PCC; // Default value (won't be used)
            return false;
        }

        /// <summary>
        /// Generates diagnostic information for invalid signatures to help with debugging.
        /// </summary>
        /// <param name="signature">The invalid signature value.</param>
        /// <returns>Diagnostic string with additional information.</returns>
        private string GenerateSignatureDiagnostics(uint signature)
        {
            var diagnostics = new StringBuilder();
            diagnostics.Append("Diagnostics: ");

            // Check if file might be too small
            if (Data.Length < 4)
            {
                diagnostics.Append($"File is too small ({Data.Length} bytes). ");
            }

            // Check if signature might be zero (common corruption pattern)
            if (signature == 0x00000000)
            {
                diagnostics.Append("Signature is all zeros (likely corrupted or empty file). ");
            }
            else if (signature == 0xFFFFFFFF)
            {
                diagnostics.Append("Signature is all ones (likely corrupted or uninitialized data). ");
            }

            // Check if signature looks like it might be in wrong byte order
            // If we swap bytes and it matches a known signature, suggest endianness issue
            uint swapped = ((signature & 0x000000FF) << 24) |
                          ((signature & 0x0000FF00) << 8) |
                          ((signature & 0x00FF0000) >> 8) |
                          ((signature & 0xFF000000) >> 24);

            if (swapped == 0x9E2A83C1 || swapped == 0x9E2A83C4 ||
                swapped == 0xC1832A9E || swapped == 0xC4832A9E)
            {
                diagnostics.Append($"Byte-swapped signature (0x{swapped:X8}) matches known format - possible endianness issue. ");
            }

            // Check if signature might be ASCII text (common mistake)
            byte[] sigBytes = BitConverter.GetBytes(signature);
            bool isAscii = true;
            for (int i = 0; i < 4; i++)
            {
                if (sigBytes[i] < 0x20 || sigBytes[i] > 0x7E)
                {
                    isAscii = false;
                    break;
                }
            }
            if (isAscii)
            {
                string asciiText = Encoding.ASCII.GetString(sigBytes);
                diagnostics.Append($"Signature appears to be ASCII text: \"{asciiText}\" (file might not be a PCC/UPK package). ");
            }

            // Check first few bytes after signature to see if structure looks valid
            // Read directly from Data array to avoid side effects on Reader position
            if (Data.Length >= 8)
            {
                int version = BitConverter.ToInt32(Data, 4);
                if (version < 0 || version > 10000)
                {
                    diagnostics.Append($"Version field ({version}) appears invalid. ");
                }
            }

            return diagnostics.ToString();
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

