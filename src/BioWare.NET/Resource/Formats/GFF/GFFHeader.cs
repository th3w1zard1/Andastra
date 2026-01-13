using System;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF
{
    /// <summary>
    /// Represents the header of a GFF file.
    /// Contains file type, version, and offsets/counts for all sections.
    /// 
    /// Based on BioWare Aurora GFF format specification:
    /// - vendor/PyKotor/wiki/Bioware-Aurora-GFF.md:320-354
    /// - vendor/PyKotor/wiki/GFF-File-Format.md:113-127
    /// 
    /// Header structure (56 bytes total):
    /// - FileType: 4 bytes (char[4])
    /// - FileVersion: 4 bytes (char[4])
    /// - StructArrayOffset: 4 bytes (uint32)
    /// - StructCount: 4 bytes (uint32)
    /// - FieldArrayOffset: 4 bytes (uint32)
    /// - FieldCount: 4 bytes (uint32)
    /// - LabelArrayOffset: 4 bytes (uint32)
    /// - LabelCount: 4 bytes (uint32)
    /// - FieldDataOffset: 4 bytes (uint32)
    /// - FieldDataCount: 4 bytes (uint32)
    /// - FieldIndicesOffset: 4 bytes (uint32)
    /// - FieldIndicesCount: 4 bytes (uint32)
    /// - ListIndicesOffset: 4 bytes (uint32)
    /// - ListIndicesCount: 4 bytes (uint32)
    /// </summary>
    public class GFFHeader
    {
        /// <summary>
        /// 4-character file type string (e.g., "UTI ", "ARE ", "DLG ").
        /// </summary>
        public string FileType { get; set; } = "GFF ";

        /// <summary>
        /// 4-character GFF version string (typically "V3.2").
        /// </summary>
        public string FileVersion { get; set; } = "V3.2";

        /// <summary>
        /// Offset of Struct array as bytes from the beginning of the file.
        /// </summary>
        public uint StructArrayOffset { get; set; }

        /// <summary>
        /// Number of elements in Struct array.
        /// </summary>
        public uint StructCount { get; set; }

        /// <summary>
        /// Offset of Field array as bytes from the beginning of the file.
        /// </summary>
        public uint FieldArrayOffset { get; set; }

        /// <summary>
        /// Number of elements in Field array.
        /// </summary>
        public uint FieldCount { get; set; }

        /// <summary>
        /// Offset of Label array as bytes from the beginning of the file.
        /// </summary>
        public uint LabelArrayOffset { get; set; }

        /// <summary>
        /// Number of elements in Label array.
        /// </summary>
        public uint LabelCount { get; set; }

        /// <summary>
        /// Offset of Field Data section as bytes from the beginning of the file.
        /// </summary>
        public uint FieldDataOffset { get; set; }

        /// <summary>
        /// Number of bytes in Field Data block.
        /// </summary>
        public uint FieldDataCount { get; set; }

        /// <summary>
        /// Offset of Field Indices array as bytes from the beginning of the file.
        /// </summary>
        public uint FieldIndicesOffset { get; set; }

        /// <summary>
        /// Number of bytes in Field Indices array.
        /// </summary>
        public uint FieldIndicesCount { get; set; }

        /// <summary>
        /// Offset of List Indices array as bytes from the beginning of the file.
        /// </summary>
        public uint ListIndicesOffset { get; set; }

        /// <summary>
        /// Number of bytes in List Indices array.
        /// </summary>
        public uint ListIndicesCount { get; set; }
    }
}

