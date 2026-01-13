using System;
using System.IO;
using System.Text;
using System.Xml.Linq;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.SSF
{
    /// <summary>
    /// Reads SSF files from XML format.
    /// XML is a human-readable format for easier editing of sound set files.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/io_ssf_xml.py:26-59
    /// 
    /// References:
    /// ----------
    ///     vendor/xoreos-tools/src/xml/ssfdumper.cpp (SSF to XML conversion)
    ///     vendor/xoreos-tools/src/xml/ssfcreator.cpp (XML to SSF conversion)
    ///     Note: XML format structure may vary between tools
    /// </summary>
    public class SSFXMLReader
    {
        private readonly int _offset;
        private readonly int? _size;
        private readonly object _source;

        public SSFXMLReader()
        {
            _offset = 0;
            _size = null;
            _source = null;
        }

        public SSFXMLReader(object source, int offset = 0, int size = 0)
        {
            _source = source;
            _offset = offset;
            _size = size > 0 ? size : (int?)null;
        }

        /// <summary>
        /// Loads an SSF from XML text.
        /// </summary>
        public SSF Load(string xmlText)
        {
            var doc = XDocument.Parse(xmlText);
            return LoadFromXmlDocument(doc);
        }

        /// <summary>
        /// Loads an SSF from XML stream.
        /// </summary>
        public SSF Load(Stream xmlStream)
        {
            var doc = XDocument.Load(xmlStream);
            return LoadFromXmlDocument(doc);
        }

        /// <summary>
        /// Loads an SSF from XML bytes.
        /// </summary>
        public SSF Load(byte[] xmlBytes)
        {
            // Try to decode with UTF-8 first, fallback to other encodings if needed
            string xmlText = null;
            try
            {
                xmlText = Encoding.UTF8.GetString(xmlBytes);
            }
            catch
            {
                try
                {
                    xmlText = Encoding.ASCII.GetString(xmlBytes);
                }
                catch
                {
                    xmlText = Encoding.Default.GetString(xmlBytes);
                }
            }
            return Load(xmlText);
        }

        /// <summary>
        /// Loads an SSF from the source with offset and size constraints.
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/io_ssf_xml.py:47-59
        /// </summary>
        public SSF Load()
        {
            if (_source == null)
            {
                throw new InvalidOperationException("Source must be set via constructor");
            }

            byte[] xmlBytes = null;
            if (_source is string filepath)
            {
                // Check if the string is XML content (starts with '<') or a file path
                if (!string.IsNullOrWhiteSpace(filepath) && filepath.TrimStart().StartsWith("<"))
                {
                    // Raw XML content - extract substring if offset/size specified
                    if (_offset > 0 || _size.HasValue)
                    {
                        int start = _offset;
                        int length = _size ?? (filepath.Length - start);
                        if (start + length > filepath.Length)
                        {
                            length = filepath.Length - start;
                        }
                        string xmlText = filepath.Substring(start, length);
                        return Load(xmlText);
                    }
                    return Load(filepath);
                }
                else
                {
                    // File path - read with offset/size
                    using (var reader = BioWare.NET.Common.RawBinaryReader.FromFile(filepath, _offset, _size))
                    {
                        xmlBytes = reader.ReadBytes(reader.Size);
                    }
                }
            }
            else if (_source is byte[] data)
            {
                using (var reader = BioWare.NET.Common.RawBinaryReader.FromBytes(data, _offset, _size))
                {
                    xmlBytes = reader.ReadBytes(reader.Size);
                }
            }
            else if (_source is Stream stream)
            {
                using (var reader = BioWare.NET.Common.RawBinaryReader.FromStream(stream, _offset, _size))
                {
                    xmlBytes = reader.ReadBytes(reader.Size);
                }
            }
            else
            {
                throw new ArgumentException("Source must be string, byte[], or Stream");
            }

            return Load(xmlBytes);
        }

        private SSF LoadFromXmlDocument(XDocument doc)
        {
            var ssf = new SSF();

            // Find root element (can be "xml" or any root)
            XElement rootElement = doc.Root;
            if (rootElement == null)
            {
                throw new InvalidDataException("XML data is not valid XML");
            }

            // Iterate through all child elements looking for "sound" elements
            foreach (var child in rootElement.Elements())
            {
                if (child.Name.LocalName == "sound")
                {
                    try
                    {
                        // Get id attribute (SSFSound enum value)
                        var idAttr = child.Attribute("id");
                        if (idAttr == null || string.IsNullOrEmpty(idAttr.Value))
                        {
                            continue;
                        }

                        if (!int.TryParse(idAttr.Value, out int soundId))
                        {
                            continue;
                        }

                        // Validate sound ID is in range (0-27)
                        if (soundId < 0 || soundId > 27)
                        {
                            continue;
                        }

                        // Get strref attribute (string reference)
                        var strrefAttr = child.Attribute("strref");
                        if (strrefAttr == null || string.IsNullOrEmpty(strrefAttr.Value))
                        {
                            continue;
                        }

                        if (!int.TryParse(strrefAttr.Value, out int stringref))
                        {
                            continue;
                        }

                        // Convert sound ID to SSFSound enum
                        SSFSound sound = (SSFSound)soundId;
                        ssf.SetData(sound, stringref);
                    }
                    catch (ArgumentException)
                    {
                        // Invalid SSFSound enum value, skip this element
                        continue;
                    }
                    catch (OverflowException)
                    {
                        // Invalid integer value, skip this element
                        continue;
                    }
                }
            }

            return ssf;
        }
    }
}

