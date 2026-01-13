using System;

namespace BioWare.NET.Resource.Formats.GFF
{
    /// <summary>
    /// Represents a single field entry in the GFF field array.
    /// 
    /// Based on BioWare Aurora GFF format specification:
    /// - vendor/PyKotor/wiki/Bioware-Aurora-GFF.md:404-420
    /// - vendor/PyKotor/wiki/GFF-File-Format.md:132-150
    /// 
    /// Field entry structure (12 bytes total):
    /// - FieldType: 4 bytes (uint32) - Type of the field
    /// - LabelIndex: 4 bytes (uint32) - Index into the label array
    /// - DataOrDataOffset: 4 bytes (uint32) - Either inline data value or offset to field data section
    /// </summary>
    public class GFFFieldEntry
    {
        /// <summary>
        /// Type of the field (e.g., UInt8, Int32, String, Struct, List).
        /// </summary>
        public GFFFieldType FieldType { get; set; }

        /// <summary>
        /// Index into the label array for this field's name.
        /// </summary>
        public uint LabelIndex { get; set; }

        /// <summary>
        /// For simple types: inline data value.
        /// For complex types: offset into field data section.
        /// For Struct type: index into struct array.
        /// For List type: offset into list indices array.
        /// </summary>
        public uint DataOrDataOffset { get; set; }
    }
}

