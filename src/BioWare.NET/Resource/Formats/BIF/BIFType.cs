using System;
using System.IO;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.BIF
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:72-99
    // Original: class BIFType(Enum):
    /// <summary>
    /// The type of BIF file based on file header signature.
    /// 
    /// BIF files can be either uncompressed (BIFF) or LZMA-compressed (BZF).
    /// The file type is determined by the first 4 bytes of the file header.
    /// </summary>
    public enum BIFType
    {
        BIF,  // Regular uncompressed BIF file
        BZF   // LZMA-compressed BIF file (used in some distributions)
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bif/bif_data.py:88-99
    // Original: @classmethod def from_extension(cls, ext_or_filepath: os.PathLike | str) -> BIFType:
    public static class BIFTypeExtensions
    {
        public static string ToFourCC(this BIFType type)
        {
            return type == BIFType.BIF ? "BIFF" : "BZF ";
        }

        public static BIFType FromExtension([CanBeNull] string extOrFilepath)
        {
            if (string.IsNullOrEmpty(extOrFilepath))
            {
                throw new ArgumentException("Extension or filepath cannot be null or empty", nameof(extOrFilepath));
            }

            if (FileHelpers.IsBifFile(extOrFilepath))
            {
                return BIFType.BIF;
            }
            if (FileHelpers.IsBzfFile(extOrFilepath))
            {
                return BIFType.BZF;
            }

            throw new ArgumentException($"'{extOrFilepath}' is not a valid BZF/BIF file extension.", nameof(extOrFilepath));
        }
    }
}


