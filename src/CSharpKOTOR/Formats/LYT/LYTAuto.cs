using System;
using System.IO;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Formats.LYT
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py
    // Original: read_lyt, write_lyt, bytes_lyt functions
    public static class LYTAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py:13-39
        // Original: def read_lyt(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> LYT
        public static LYT ReadLyt(object source, int offset = 0, int? size = null)
        {
            int sizeValue = size ?? 0;
            if (source is string filepath)
            {
                return new LYTAsciiReader(filepath, offset, sizeValue).Load();
            }
            if (source is byte[] bytes)
            {
                return new LYTAsciiReader(bytes, offset, sizeValue).Load();
            }
            if (source is Stream stream)
            {
                return new LYTAsciiReader(stream, offset, sizeValue).Load();
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for LYT");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py:42-65
        // Original: def write_lyt(lyt: LYT, target: TARGET_TYPES, file_format: ResourceType = ResourceType.LYT)
        public static void WriteLyt(LYT lyt, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LYT;
            if (format != ResourceType.LYT)
            {
                throw new ArgumentException("Unsupported format specified; use LYT.");
            }

            if (target is string filepath)
            {
                new LYTAsciiWriter(lyt, filepath).Write();
            }
            else if (target is Stream stream)
            {
                new LYTAsciiWriter(lyt, stream).Write();
            }
            else
            {
                throw new ArgumentException("Target must be string or Stream for LYT");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lyt/lyt_auto.py:68-91
        // Original: def bytes_lyt(lyt: LYT, file_format: ResourceType = ResourceType.LYT) -> bytes
        public static byte[] BytesLyt(LYT lyt, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LYT;
            using (var ms = new MemoryStream())
            {
                WriteLyt(lyt, ms, format);
                return ms.ToArray();
            }
        }
    }
}

