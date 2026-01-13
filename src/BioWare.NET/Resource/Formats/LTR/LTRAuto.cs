using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.LTR
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_auto.py
    // Original: read_ltr, write_ltr, bytes_ltr functions
    public static class LTRAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_auto.py:13-38
        // Original: def read_ltr(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> LTR
        public static LTR ReadLtr(object source, int offset = 0, int? size = null)
        {
            try
            {
                int sizeValue = size ?? 0;
                if (source is string filepath)
                {
                    return new LTRBinaryReader(filepath, offset, sizeValue).Load();
                }
                if (source is byte[] bytes)
                {
                    return new LTRBinaryReader(bytes, offset, sizeValue).Load();
                }
                if (source is Stream stream)
                {
                    return new LTRBinaryReader(stream, offset, sizeValue).Load();
                }
                throw new ArgumentException("Source must be string, byte[], or Stream for LTR");
            }
            catch (IOException)
            {
                throw new ArgumentException("Tried to load an unsupported or corrupted LTR file.");
            }
            catch (ArgumentException)
            {
                throw;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_auto.py:41-62
        // Original: def write_ltr(ltr: LTR, target: TARGET_TYPES, file_format: ResourceType = ResourceType.LTR)
        public static void WriteLtr(LTR ltr, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LTR;
            if (format != ResourceType.LTR)
            {
                throw new ArgumentException("Unsupported format specified; use LTR.");
            }

            if (target is string filepath)
            {
                new LTRBinaryWriter(ltr, filepath).Write();
            }
            else if (target is Stream stream)
            {
                new LTRBinaryWriter(ltr, stream).Write();
            }
            else
            {
                throw new ArgumentException("Target must be string or Stream for LTR");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ltr/ltr_auto.py:65-88
        // Original: def bytes_ltr(ltr: LTR, file_format: ResourceType = ResourceType.LTR) -> bytes
        public static byte[] BytesLtr(LTR ltr, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LTR;
            using (var ms = new MemoryStream())
            {
                WriteLtr(ltr, ms, format);
                return ms.ToArray();
            }
        }
    }
}

