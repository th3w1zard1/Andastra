using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Andastra.Parsing.Formats.SSF
{
    /// <summary>
    /// Writes SSF files to XML format.
    /// XML is a human-readable format for easier editing of sound set files.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/io_ssf_xml.py:62-86
    /// </summary>
    public class SSFXMLWriter
    {
        private readonly SSF _ssf;
        private readonly XElement _xmlRoot;

        /// <summary>
        /// Initializes a new instance of SSFXMLWriter.
        /// </summary>
        /// <param name="ssf">The SSF object to write.</param>
        public SSFXMLWriter(SSF ssf)
        {
            _ssf = ssf ?? throw new ArgumentNullException(nameof(ssf));
            _xmlRoot = new XElement("xml");
        }

        /// <summary>
        /// Writes the SSF to XML format and returns the XML as a byte array.
        /// </summary>
        /// <returns>XML data as byte array.</returns>
        public byte[] Write()
        {
            // Iterate through all SSFSound enum values
            foreach (SSFSound sound in Enum.GetValues(typeof(SSFSound)).Cast<SSFSound>())
            {
                // Get enum name (e.g., "BATTLE_CRY_1")
                string soundName = sound.ToString();

                // Get enum value (0-27)
                int soundId = (int)sound;

                // Get string reference from SSF (returns -1 if not set)
                int? strrefValue = _ssf.Get(sound);
                int strref = strrefValue ?? -1;

                // Create sound element with attributes
                var soundElement = new XElement("sound");
                soundElement.SetAttributeValue("id", soundId.ToString());
                soundElement.SetAttributeValue("label", soundName);
                soundElement.SetAttributeValue("strref", strref.ToString());

                _xmlRoot.Add(soundElement);
            }

            // Indent the XML for readability
            IndentXml(_xmlRoot);

            // Convert XML to string and then to bytes
            string xmlString = _xmlRoot.ToString(SaveOptions.None);
            return Encoding.UTF8.GetBytes(xmlString);
        }

        /// <summary>
        /// Writes the SSF to XML format and saves it to a file.
        /// </summary>
        /// <param name="filePath">Path to the output file.</param>
        public void Write(string filePath)
        {
            byte[] xmlBytes = Write();
            File.WriteAllBytes(filePath, xmlBytes);
        }

        /// <summary>
        /// Writes the SSF to XML format and writes it to a stream.
        /// </summary>
        /// <param name="stream">Stream to write to.</param>
        public void Write(Stream stream)
        {
            byte[] xmlBytes = Write();
            stream.Write(xmlBytes, 0, xmlBytes.Length);
        }

        /// <summary>
        /// Indents the XML element tree for readability.
        /// </summary>
        /// <param name="element">The XML element to indent.</param>
        /// <param name="level">The indentation level (default: 0).</param>
        private static void IndentXml(XElement element, int level = 0)
        {
            string indent = "\n" + new string(' ', level * 2);

            if (element.Elements().Any())
            {
                if (string.IsNullOrWhiteSpace(element.Value))
                {
                    element.Value = indent + "  ";
                }

                foreach (var child in element.Elements())
                {
                    IndentXml(child, level + 1);
                }
            }
        }
    }
}
