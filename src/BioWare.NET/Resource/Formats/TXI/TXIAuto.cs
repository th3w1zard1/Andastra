using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.TXI
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_auto.py
    // Original: read_txi, write_txi, bytes_txi functions
    public static class TXIAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_auto.py:13-19
        // Original: def read_txi(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> TXI
        public static TXI ReadTxi(object source, int offset = 0, int? size = null)
        {
            int sizeValue = size ?? 0;
            if (source is string filepath)
            {
                return new TXIBinaryReader(filepath, offset, sizeValue).Load();
            }
            if (source is byte[] bytes)
            {
                return new TXIBinaryReader(bytes, offset, sizeValue).Load();
            }
            if (source is Stream stream)
            {
                return new TXIBinaryReader(stream, offset, sizeValue).Load();
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for TXI");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_auto.py:22-32
        // Original: def write_txi(txi: TXI, target: TARGET_TYPES, file_format: ResourceType = ResourceType.TXI)
        public static void WriteTxi(TXI txi, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.TXI;
            if (format != ResourceType.TXI)
            {
                throw new ArgumentException("Unsupported format specified; use TXI.");
            }

            if (target is string filepath)
            {
                new TXIBinaryWriter(txi, filepath).Write();
            }
            else if (target is Stream stream)
            {
                new TXIBinaryWriter(txi, stream).Write();
            }
            else
            {
                throw new ArgumentException("Target must be string or Stream for TXI");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/txi/txi_auto.py:35-42
        // Original: def bytes_txi(txi: TXI, file_format: ResourceType = ResourceType.TXI) -> bytes
        public static byte[] BytesTxi(TXI txi, ResourceType fileFormat = null)
        {
            using (var ms = new MemoryStream())
            {
                WriteTxi(txi, ms, fileFormat);
                return ms.ToArray();
            }
        }
    }
}

