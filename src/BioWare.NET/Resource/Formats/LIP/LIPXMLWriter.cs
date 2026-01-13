using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.LIP
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_xml.py:75-99
    // Original: class LIPXMLWriter(ResourceWriter)
    /// <summary>
    /// Writes LIP files to XML format.
    /// </summary>
    /// <remarks>
    /// XML is a human-readable format for easier editing of lip-sync animation data.
    /// Format: &lt;lip duration="1.5"&gt;&lt;keyframe time="0.0" shape="0" /&gt;...&lt;/lip&gt;
    /// References:
    /// - vendor/xoreos-tools/src/xml/lipdumper.cpp (LIP to XML conversion)
    /// - vendor/xoreos-tools/src/xml/lipcreator.cpp (XML to LIP conversion)
    /// </remarks>
    public class LIPXMLWriter : IDisposable
    {
        private readonly LIP _lip;
        private readonly RawBinaryWriter _writer;

        public LIPXMLWriter(LIP lip, string filepath)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public LIPXMLWriter(LIP lip, Stream target)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public LIPXMLWriter(LIP lip)
        {
            _lip = lip ?? throw new ArgumentNullException(nameof(lip));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/lip/io_lip_xml.py:85-99
        // Original: @autoclose def write(self, *, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                // Create root element
                XElement xmlRoot = new XElement("lip");
                xmlRoot.SetAttributeValue("duration", _lip.Length.ToString("F6"));

                // Add keyframe elements
                foreach (LIPKeyFrame keyframe in _lip.Frames)
                {
                    XElement keyframeElement = new XElement("keyframe");
                    keyframeElement.SetAttributeValue("time", keyframe.Time.ToString("F6"));
                    keyframeElement.SetAttributeValue("shape", ((int)keyframe.Shape).ToString());
                    xmlRoot.Add(keyframeElement);
                }

                // Create document with declaration
                XDocument doc = new XDocument(new XDeclaration("1.0", "utf-8", null), xmlRoot);

                // Convert to formatted XML bytes using standard .NET XML formatting
                // Matching PyKotor's indent behavior with proper indentation
                byte[] xmlBytes;
                using (var memoryStream = new MemoryStream())
                {
                    XmlWriterSettings settings = new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "  ",
                        Encoding = Encoding.UTF8,
                        OmitXmlDeclaration = false
                    };
                    using (XmlWriter xmlWriter = XmlWriter.Create(memoryStream, settings))
                    {
                        doc.Save(xmlWriter);
                    }
                    xmlBytes = memoryStream.ToArray();
                }

                // Write XML bytes directly
                _writer.WriteBytes(xmlBytes);
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }


        public void Dispose()
        {
            _writer?.Dispose();
        }

        // Matching LIPBinaryWriter pattern for BytesLip
        // Get the data from the underlying RawBinaryWriter
        public byte[] Data()
        {
            return _writer?.Data() ?? new byte[0];
        }
    }
}

