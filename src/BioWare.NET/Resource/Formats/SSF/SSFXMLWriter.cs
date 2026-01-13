using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace BioWare.NET.Resource.Formats.SSF
{
    /// <summary>
    /// Writes SSF files to XML format.
    /// XML is a human-readable format for easier editing of sound set files.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/io_ssf_xml.py:62-86
    /// 
    /// References:
    /// ----------
    ///     vendor/xoreos-tools/src/xml/ssfdumper.cpp (SSF to XML conversion)
    ///     vendor/xoreos-tools/src/xml/ssfcreator.cpp (XML to SSF conversion)
    ///     Note: XML format structure may vary between tools
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
        /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/ssf/io_ssf_xml.py:73-86
        /// </summary>
        /// <returns>XML data as byte array.</returns>
        public byte[] Write()
        {
            // Iterate through all SSFSound enum values
            // Matching Python: for sound_name, sound in SSFSound.__members__.items()
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
                // Matching Python: ElementTree.SubElement(self.xml_root, "sound", {...})
                var soundElement = new XElement("sound");
                soundElement.SetAttributeValue("id", soundId.ToString());
                soundElement.SetAttributeValue("label", soundName);
                soundElement.SetAttributeValue("strref", strref.ToString());
                
                _xmlRoot.Add(soundElement);
            }

            // Indent the XML for readability
            // Matching Python: indent(self.xml_root)
            IndentXml(_xmlRoot);

            // Convert XML to string and then to bytes
            // Matching Python: ElementTree.tostring(self.xml_root)
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
        /// Matching PyKotor implementation at Libraries/PyKotor/src/utility/misc.py:312-345
        /// </summary>
        /// <param name="element">The XML element to indent.</param>
        /// <param name="level">The indentation level (default: 0).</param>
        private static void IndentXml(XElement element, int level = 0)
        {
            // Calculate indentation string (2 spaces per level)
            // Matching Python: i: str = "\n" + level * "  "
            string indent = "\n" + new string(' ', level * 2);

            if (element.Elements().Any())
            {
                // Element has children
                // Matching Python: if len(elem):
                if (string.IsNullOrWhiteSpace(element.Value))
                {
                    // Set text to indentation + 2 spaces for children
                    // Matching Python: elem.text = f"{i}  "
                    element.Value = indent + "  ";
                }

                // Recursively indent child elements
                // Matching Python: for e in elem: indent(e, level + 1)
                foreach (var child in element.Elements())
                {
                    IndentXml(child, level + 1);
                }

                // Set tail to indentation
                // Matching Python: elem.tail = i
                // Note: XElement doesn't have a direct tail property, but we can handle this
                // by ensuring proper formatting in the final output
            }
            else if (level > 0)
            {
                // Empty element at non-root level
                // Matching Python: elif level and (not elem.tail or not elem.tail.strip()): elem.tail = i
                // Note: XElement handles this automatically in ToString()
            }
        }
    }
}

