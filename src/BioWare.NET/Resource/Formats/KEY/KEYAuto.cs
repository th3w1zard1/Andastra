using System;
using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.KEY
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/key/key_auto.py
    // Original: detect_key, read_key, write_key, bytes_key
    public static class KEYAuto
    {
        public static ResourceType DetectKey(object source, int offset = 0)
        {
            try
            {
                using (BioWare.NET.Common.RawBinaryReader reader = CreateReader(source, offset, 0))
                {
                    reader.Seek(offset);
                    string fileType = reader.ReadString(4);
                    string fileVersion = reader.ReadString(4);
                    if (fileType == KEY.FileTypeConst && (fileVersion == KEY.FileVersionConst || fileVersion == "V1.1"))
                    {
                        return ResourceType.KEY;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore and fall through to invalid
            }
            return ResourceType.INVALID;
        }

        public static KEY ReadKey(object source, int offset = 0, int? size = null)
        {
            ResourceType format = DetectKey(source, offset);
            if (format != ResourceType.KEY)
            {
                throw new ArgumentException("Invalid KEY file format");
            }

            int sizeValue = size ?? 0;
            if (source is string filepath)
            {
                return new KEYBinaryReader(filepath, offset, sizeValue).Load();
            }
            if (source is byte[] bytes)
            {
                return new KEYBinaryReader(bytes, offset, sizeValue).Load();
            }
            if (source is Stream stream)
            {
                return new KEYBinaryReader(stream, offset, sizeValue).Load();
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for KEY");
        }

        public static void WriteKey(KEY key, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.KEY;
            if (format != ResourceType.KEY)
            {
                throw new ArgumentException("Unsupported format specified; use KEY.");
            }

            if (target is string filepath)
            {
                new KEYBinaryWriter(key, filepath).Write();
                return;
            }
            if (target is Stream stream)
            {
                new KEYBinaryWriter(key, stream).Write();
                return;
            }
            throw new ArgumentException("Target must be string or Stream for KEY");
        }

        public static byte[] BytesKey(KEY key, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.KEY;
            using (var ms = new MemoryStream())
            {
                WriteKey(key, ms, format);
                return ms.ToArray();
            }
        }

        private static BioWare.NET.Common.RawBinaryReader CreateReader(object source, int offset, int size)
        {
            if (source is string filepath)
            {
                return BioWare.NET.Common.RawBinaryReader.FromFile(filepath, offset, size > 0 ? (int?)size : null);
            }
            if (source is byte[] bytes)
            {
                return BioWare.NET.Common.RawBinaryReader.FromBytes(bytes, offset, size > 0 ? (int?)size : null);
            }
            if (source is Stream stream)
            {
                return BioWare.NET.Common.RawBinaryReader.FromStream(stream, offset, size > 0 ? (int?)size : null);
            }
            throw new ArgumentException("Source must be string, byte[], or Stream for KEY");
        }
    }
}

