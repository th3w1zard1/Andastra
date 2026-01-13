using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.PCC
{
    /// <summary>
    /// Auto-detection and convenience functions for PCC/UPK files.
    /// </summary>
    /// <remarks>
    /// PCC/UPK Auto:
    /// - Provides convenience methods for reading and writing PCC/UPK files
    /// - Follows the same pattern as ERFAuto and RIMAuto
    /// - Supports both PCC and UPK formats (same structure, different extensions)
    /// - Used by Eclipse Engine games (Dragon Age, )
    /// </remarks>
    public static class PCCAuto
    {
        /// <summary>
        /// Returns a PCC instance from the source.
        /// </summary>
        public static PCC ReadPcc(string source, int offset = 0, int size = 0)
        {
            return new PCCBinaryReader(source).Load();
        }

        /// <summary>
        /// Returns a PCC instance from byte data.
        /// </summary>
        public static PCC ReadPcc(byte[] data, int offset = 0, int size = 0)
        {
            return new PCCBinaryReader(data).Load();
        }

        /// <summary>
        /// Writes the PCC data to the target location with the specified format (PCC or UPK).
        /// </summary>
        public static void WritePcc(PCC pcc, string target, ResourceType fileFormat)
        {
            if (fileFormat == ResourceType.PCC || fileFormat == ResourceType.UPK)
            {
                var writer = new PCCBinaryWriter(pcc);
                byte[] data = writer.Write();
                File.WriteAllBytes(target, data);
            }
            else
            {
                throw new ArgumentException($"Unsupported format specified: '{fileFormat}'; expected one of ResourceType.PCC, ResourceType.UPK.");
            }
        }

        /// <summary>
        /// Returns the PCC data as a byte array.
        /// </summary>
        public static byte[] BytesPcc(PCC pcc, [CanBeNull] ResourceType fileFormat = null)
        {
            var writer = new PCCBinaryWriter(pcc);
            return writer.Write();
        }
    }
}

