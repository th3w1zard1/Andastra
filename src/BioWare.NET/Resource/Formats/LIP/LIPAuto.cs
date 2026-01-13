using System;
using System.IO;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_auto.py
    // Original: detect_lip, read_lip, write_lip, bytes_lip functions
    public static class LIPAuto
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_auto.py:16-60
        // Original: def detect_lip(source: SOURCE_TYPES, offset: int = 0) -> ResourceType
        public static ResourceType DetectLip(object source, int offset = 0)
        {
            ResourceType Check(string first4)
            {
                if (first4 == "LIP ")
                {
                    return ResourceType.LIP;
                }
                if (first4.Contains("<"))
                {
                    return ResourceType.LIP_XML;
                }
                if (first4.Contains("{"))
                {
                    return ResourceType.LIP_JSON;
                }
                return ResourceType.INVALID;
            }

            ResourceType fileFormat;
            try
            {
                using (var reader = RawBinaryReader.FromAuto(source, offset))
                {
                    fileFormat = Check(reader.ReadString(4));
                }
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (DirectoryNotFoundException)
            {
                throw;
            }
            catch (IOException)
            {
                fileFormat = ResourceType.INVALID;
            }

            return fileFormat;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_auto.py:63-99
        // Original: def read_lip(source: SOURCE_TYPES, offset: int = 0, size: int | None = None) -> LIP
        public static LIP ReadLip(object source, int offset = 0, int? size = null)
        {
            ResourceType fileFormat = DetectLip(source, offset);
            int sizeValue = size ?? 0;

            if (fileFormat == ResourceType.LIP)
            {
                if (source is string filepath)
                {
                    return new LIPBinaryReader(filepath, offset, sizeValue).Load();
                }
                if (source is byte[] bytes)
                {
                    return new LIPBinaryReader(bytes, offset, sizeValue).Load();
                }
                if (source is Stream stream)
                {
                    return new LIPBinaryReader(stream, offset, sizeValue).Load();
                }
                throw new ArgumentException("Source must be string, byte[], or Stream for binary LIP");
            }
            if (fileFormat == ResourceType.LIP_XML)
            {
                if (source is string filepath)
                {
                    return new LIPXMLReader(filepath, offset, sizeValue).Load();
                }
                if (source is byte[] bytes)
                {
                    return new LIPXMLReader(bytes, offset, sizeValue).Load();
                }
                if (source is Stream stream)
                {
                    return new LIPXMLReader(stream, offset, sizeValue).Load();
                }
                throw new ArgumentException("Source must be string, byte[], or Stream for XML LIP");
            }
            if (fileFormat == ResourceType.LIP_JSON)
            {
                if (source is string filepath)
                {
                    return new LIPJSONReader(filepath, offset, sizeValue).Load();
                }
                if (source is byte[] bytes)
                {
                    return new LIPJSONReader(bytes, offset, sizeValue).Load();
                }
                if (source is Stream stream)
                {
                    return new LIPJSONReader(stream, offset, sizeValue).Load();
                }
                throw new ArgumentException("Source must be string, byte[], or Stream for JSON LIP");
            }
            throw new ArgumentException("Failed to determine the format of the LIP file.");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_auto.py:102-129
        // Original: def write_lip(lip: LIP, target: TARGET_TYPES, file_format: ResourceType = ResourceType.LIP)
        public static void WriteLip(LIP lip, object target, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LIP;
            if (format == ResourceType.LIP)
            {
                if (target is string filepath)
                {
                    new LIPBinaryWriter(lip, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new LIPBinaryWriter(lip, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for binary LIP");
                }
            }
            else if (format == ResourceType.LIP_XML)
            {
                if (target is string filepath)
                {
                    new LIPXMLWriter(lip, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new LIPXMLWriter(lip, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for XML LIP");
                }
            }
            else if (format == ResourceType.LIP_JSON)
            {
                if (target is string filepath)
                {
                    new LIPJSONWriter(lip, filepath).Write();
                }
                else if (target is Stream stream)
                {
                    new LIPJSONWriter(lip, stream).Write();
                }
                else
                {
                    throw new ArgumentException("Target must be string or Stream for JSON LIP");
                }
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use LIP or LIP_XML or LIP_JSON.");
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/lip_auto.py:132-155
        // Original: def bytes_lip(lip: LIP, file_format: ResourceType = ResourceType.LIP) -> bytes
        // Matching BWMAuto.BytesBwm pattern - use constructor with no target and call Data()
        public static byte[] BytesLip(LIP lip, ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.LIP;
            if (format == ResourceType.LIP)
            {
                using (var writer = new LIPBinaryWriter(lip))
                {
                    writer.Write();
                    return writer.Data();
                }
            }
            else if (format == ResourceType.LIP_XML)
            {
                using (var writer = new LIPXMLWriter(lip))
                {
                    writer.Write();
                    return writer.Data();
                }
            }
            else if (format == ResourceType.LIP_JSON)
            {
                using (var writer = new LIPJSONWriter(lip))
                {
                    writer.Write();
                    return writer.Data();
                }
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use LIP or LIP_XML or LIP_JSON.");
            }
        }
    }
}

