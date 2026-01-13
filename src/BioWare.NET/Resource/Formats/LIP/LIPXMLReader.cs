using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_xml.py:23-72
    // Original: class LIPXMLReader(ResourceReader)
    /// <summary>
    /// Reads LIP files from XML format.
    /// </summary>
    /// <remarks>
    /// XML is a human-readable format for easier editing of lip-sync animation data.
    /// Format: &lt;lip duration="1.5"&gt;&lt;keyframe time="0.0" shape="0" /&gt;...&lt;/lip&gt;
    /// References:
    /// - vendor/xoreos-tools/src/xml/lipdumper.cpp (LIP to XML conversion)
    /// - vendor/xoreos-tools/src/xml/lipcreator.cpp (XML to LIP conversion)
    /// </remarks>
    public class LIPXMLReader : IDisposable
    {
        private readonly RawBinaryReader _reader;
        private LIP _lip;

        public LIPXMLReader(byte[] data, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = RawBinaryReader.FromBytes(data, offset, sizeNullable);
        }

        public LIPXMLReader(string filepath, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = RawBinaryReader.FromFile(filepath, offset, sizeNullable);
        }

        public LIPXMLReader(Stream source, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = RawBinaryReader.FromStream(source, offset, sizeNullable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_xml.py:43-72
        // Original: @autoclose def load(self, *, auto_close: bool = True) -> LIP
        public LIP Load(bool autoClose = true)
        {
            try
            {
                _lip = new LIP();

                // Read all bytes from the reader
                byte[] xmlBytes = _reader.ReadBytes(_reader.Size);

                // Decode bytes to string with fallback encodings (matching PyKotor's decode_bytes_with_fallbacks)
                string xmlString = DecodeBytesWithFallbacks(xmlBytes);

                // Parse XML document
                XDocument doc;
                try
                {
                    doc = XDocument.Parse(xmlString);
                }
                catch (XmlException ex)
                {
                    throw new ArgumentException("The XML file that was loaded was not a valid LIP.", ex);
                }

                XElement xmlRoot = doc.Root;
                if (xmlRoot == null || xmlRoot.Name.LocalName != "lip")
                {
                    throw new ArgumentException("The XML file that was loaded was not a valid LIP.");
                }

                // Parse duration attribute
                XAttribute durationAttr = xmlRoot.Attribute("duration");
                if (durationAttr == null || string.IsNullOrEmpty(durationAttr.Value))
                {
                    throw new ArgumentException("Missing duration of the LIP.");
                }
                if (!float.TryParse(durationAttr.Value, out float duration))
                {
                    throw new ArgumentException($"Invalid duration value: '{durationAttr.Value}'.");
                }
                _lip.Length = duration;

                // Parse keyframe elements
                foreach (XElement keyframeElement in xmlRoot.Elements("keyframe"))
                {
                    // Parse time attribute
                    XAttribute timeAttr = keyframeElement.Attribute("time");
                    if (timeAttr == null || string.IsNullOrEmpty(timeAttr.Value))
                    {
                        throw new ArgumentException("Missing time for a keyframe.");
                    }
                    if (!float.TryParse(timeAttr.Value, out float time))
                    {
                        throw new ArgumentException($"Invalid time value: '{timeAttr.Value}'.");
                    }

                    // Parse shape attribute
                    XAttribute shapeAttr = keyframeElement.Attribute("shape");
                    if (shapeAttr == null || string.IsNullOrEmpty(shapeAttr.Value))
                    {
                        throw new ArgumentException("Missing shape for a keyframe.");
                    }
                    if (!int.TryParse(shapeAttr.Value, out int shapeValue))
                    {
                        throw new ArgumentException($"Invalid shape value: '{shapeAttr.Value}'.");
                    }
                    LIPShape shape = (LIPShape)shapeValue;

                    // Add keyframe to LIP
                    _lip.Add(time, shape);
                }

                return _lip;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        // Matching PyKotor's decode_bytes_with_fallbacks behavior
        // Try UTF-8 first, then fall back to other encodings
        private string DecodeBytesWithFallbacks(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            // Try UTF-8 first (most common)
            try
            {
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // Fall through to next encoding
            }

            // Try ASCII
            try
            {
                return Encoding.ASCII.GetString(bytes);
            }
            catch
            {
                // Fall through to next encoding
            }

            // Try Windows-1252 (common for legacy files)
            try
            {
                Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(1252).GetString(bytes);
            }
            catch
            {
                // Fall back to UTF-8 with error handling
                return Encoding.UTF8.GetString(bytes);
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

