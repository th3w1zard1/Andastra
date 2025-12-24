using System;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using JetBrains.Annotations;

namespace Andastra.Parsing.Resource.Formats.BIF
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:102-199
    // Original: class BIFResource(ArchiveResource):
    /// <summary>
    /// A single resource entry stored in a BIF/BZF file.
    /// 
    /// BIF resources contain only the resource data, type, and ID. The actual filename (ResRef)
    /// is stored in the KEY file and matched via the resource ID. Each resource has a unique ID
    /// within the BIF that corresponds to entries in the KEY file's resource table.
    /// </summary>
    public class BIFResource
    {
        // vendor/reone/include/reone/resource/format/bifreader.h:30
        // vendor/Kotor.NET/Kotor.NET/Formats/KotorBIF/BIFBinaryStructure.cs:53
        // vendor/KotOR_IO/KotOR_IO/File Formats/BIF.cs:203
        // Resource ID (matches KEY file, unique within BIF)
        public int ResnameKeyIndex { get; set; }

        // vendor/reone/include/reone/resource/format/bifreader.h:31
        // vendor/Kotor.NET/Kotor.NET/Formats/KotorBIF/BIFBinaryStructure.cs:54
        // vendor/KotOR_IO/KotOR_IO/File Formats/BIF.cs:204
        // Byte offset to resource data in file
        private int _offset; // Offset in BIF file

        // BZF-specific: Size of compressed data
        private int _packedSize; // Size of compressed data (BZF only)

        public int Offset
        {
            get => _offset;
            set => _offset = value;
        }

        public int PackedSize
        {
            get => _packedSize;
            set => _packedSize = value;
        }

        public ResRef ResRef { get; set; }
        public ResourceType ResType { get; set; }
        public byte[] Data { get; set; }
        public int Size => Data?.Length ?? 0;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:138-161
        // Original: def __init__(self, resref: ResRef, restype: ResourceType, data: bytes, resname_key_index: int = 0, size: int | None = None):
        public BIFResource(ResRef resref, ResourceType restype, byte[] data, int resnameKeyIndex = 0, int? size = null)
        {
            ResRef = resref ?? throw new ArgumentNullException(nameof(resref));
            ResType = restype ?? throw new ArgumentNullException(nameof(restype));
            Data = data ?? new byte[0];
            ResnameKeyIndex = resnameKeyIndex;
            _offset = 0;
            _packedSize = 0;
        }

        public ResourceIdentifier Identifier()
        {
            return new ResourceIdentifier(ResRef.ToString(), ResType);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:183-190
        // Original: def __eq__(self, other: object) -> bool:
        public override bool Equals([CanBeNull] object obj)
        {
            if (!(obj is BIFResource other))
            {
                return false;
            }
            return ResnameKeyIndex == other.ResnameKeyIndex && ResType == other.ResType && Size == other.Size;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:192-194
        // Original: def __hash__(self) -> int:
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + ResnameKeyIndex.GetHashCode();
                hash = hash * 23 + ResType.GetHashCode();
                hash = hash * 23 + Size.GetHashCode();
                return hash;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:196-198
        // Original: def __str__(self) -> str:
        public override string ToString()
        {
            return $"{ResRef}:{ResType.Name}[{Size}b]";
        }
    }
}
