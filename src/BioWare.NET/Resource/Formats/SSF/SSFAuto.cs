using System;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.SSF
{

    /// <summary>
    /// Auto-detection and convenience functions for SSF files.
    /// 1:1 port of Python ssf_auto.py from pykotor/resource/formats/ssf/ssf_auto.py
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/ssf_auto.py
    /// </summary>
    public static class SSFAuto
    {
        /// <summary>
        /// Returns what format the SSF data is believed to be in.
        /// This function performs a basic check and does not guarantee accuracy of the result or integrity of the data.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/ssf_auto.py:15-59
        /// </summary>
        public static ResourceType DetectSsf(object source, int offset = 0)
        {
            try
            {
                byte[] firstBytes = null;
                if (source is string filepath)
                {
                    using (var fs = new FileStream(filepath, FileMode.Open, FileAccess.Read))
                    {
                        fs.Seek(offset, SeekOrigin.Begin);
                        firstBytes = new byte[4];
                        int bytesRead = fs.Read(firstBytes, 0, 4);
                        if (bytesRead < 4)
                        {
                            return ResourceType.INVALID;
                        }
                    }
                }
                else if (source is byte[] data)
                {
                    if (data.Length < offset + 4)
                    {
                        return ResourceType.INVALID;
                    }
                    firstBytes = new byte[4];
                    Array.Copy(data, offset, firstBytes, 0, 4);
                }
                else if (source is Stream stream)
                {
                    // Seek to offset and read 4 bytes for format detection
                    stream.Seek(offset, SeekOrigin.Begin);
                    firstBytes = new byte[4];
                    int bytesRead = stream.Read(firstBytes, 0, 4);
                    if (bytesRead < 4)
                    {
                        return ResourceType.INVALID;
                    }

                    // Restore stream position to offset so ReadSsf can read from the beginning
                    // This is critical: ReadSsf will create SSFBinaryReader which uses CopyTo
                    // from the current position, so we must restore to the start of the data
                    if (stream.CanSeek)
                    {
                        stream.Position = offset;
                    }
                }
                else
                {
                    return ResourceType.INVALID;
                }

                string first4 = Encoding.ASCII.GetString(firstBytes);
                if (first4 == "SSF ")
                {
                    return ResourceType.SSF;
                }
                if (first4.Contains("<"))
                {
                    return ResourceType.SSF_XML;
                }
                return ResourceType.INVALID;
            }
            catch
            {
                return ResourceType.INVALID;
            }
        }

        /// <summary>
        /// Reads the SSF data from the source location with the specified format (SSF or SSF_XML).
        /// The file format is automatically determined before parsing the data if not specified.
        /// 1:1 port of Python read_ssf function.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/ssf_auto.py:62-102
        /// </summary>
        public static SSF ReadSsf(object source, int offset = 0, int? size = null, [CanBeNull] ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? DetectSsf(source, offset);

            if (format == ResourceType.INVALID)
            {
                throw new ArgumentException("Failed to determine the format of the SSF file.");
            }

            if (format == ResourceType.SSF)
            {
                int sizeValue = size ?? 0;
                if (source is string filepath)
                {
                    var reader = new SSFBinaryReader(filepath, offset, sizeValue);
                    return reader.Load();
                }
                if (source is byte[] data)
                {
                    var reader = new SSFBinaryReader(data, offset, sizeValue);
                    return reader.Load();
                }
                if (source is Stream stream)
                {
                    var reader = new SSFBinaryReader(stream, offset, sizeValue);
                    return reader.Load();
                }
                throw new ArgumentException("Source must be string, byte[], or Stream");
            }

            if (format == ResourceType.SSF_XML)
            {
                int sizeValue = size ?? 0;
                var reader = new SSFXMLReader(source, offset, sizeValue);
                if (source is string filepath)
                {
                    // Check if the string is XML content (starts with '<') or a file path
                    if (!string.IsNullOrWhiteSpace(filepath) && filepath.TrimStart().StartsWith("<"))
                    {
                        // Raw XML content - use Load() method which handles offset/size
                        return reader.Load();
                    }
                    else
                    {
                        // File path - use Load() method which handles offset/size
                        return reader.Load();
                    }
                }
                if (source is byte[] bytes)
                {
                    // Use Load() method which handles offset/size
                    return reader.Load();
                }
                if (source is Stream stream)
                {
                    // Use Load() method which handles offset/size
                    return reader.Load();
                }
                throw new ArgumentException("Source must be XML content, file path, byte array, or stream.");
            }

            throw new ArgumentException("Unsupported format specified; use SSF or SSF_XML.");
        }

        /// <summary>
        /// Compatibility overload for helpers that call ReadSsf(source) without format detection.
        /// </summary>
        public static SSF ReadSsf(object source)
        {
            return ReadSsf(source, 0, null, null);
        }

        /// <summary>
        /// Writes the SSF data to the target location with the specified format (SSF or SSF_XML).
        /// 1:1 port of Python write_ssf function.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/ssf_auto.py:105-130
        /// </summary>
        public static void WriteSsf(SSF ssf, string target, ResourceType fileFormat)
        {
            if (fileFormat == ResourceType.SSF)
            {
                var writer = new SSFBinaryWriter(ssf);
                byte[] data = writer.Write();
                File.WriteAllBytes(target, data);
            }
            else if (fileFormat == ResourceType.SSF_XML)
            {
                var writer = new SSFXMLWriter(ssf);
                writer.Write(target);
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use SSF or SSF_XML.");
            }
        }

        /// <summary>
        /// Returns the SSF data as a byte array.
        /// 1:1 port of Python bytes_ssf function.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/ssf_auto.py:133-156
        /// </summary>
        public static byte[] BytesSsf(SSF ssf, [CanBeNull] ResourceType fileFormat = null)
        {
            ResourceType format = fileFormat ?? ResourceType.SSF;
            if (format == ResourceType.SSF)
            {
                var writer = new SSFBinaryWriter(ssf);
                return writer.Write();
            }
            else if (format == ResourceType.SSF_XML)
            {
                var writer = new SSFXMLWriter(ssf);
                return writer.Write();
            }
            else
            {
                throw new ArgumentException("Unsupported format specified; use SSF or SSF_XML.");
            }
        }
    }
}

