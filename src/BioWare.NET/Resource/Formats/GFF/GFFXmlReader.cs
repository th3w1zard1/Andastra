using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml;
using System.Xml.Linq;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF
{
    /// <summary>
    /// Reads GFF data from XML format.
    /// Parses GFF3 XML format as used in PyKotor test files.
    /// </summary>
    public class GFFXmlReader
    {
        /// <summary>
        /// Loads a GFF from XML text.
        /// </summary>
        public GFF Load(string xmlText)
        {
            var doc = XDocument.Parse(xmlText);
            return LoadFromXmlDocument(doc);
        }

        /// <summary>
        /// Loads a GFF from XML stream.
        /// </summary>
        public GFF Load(Stream xmlStream)
        {
            var doc = XDocument.Load(xmlStream);
            return LoadFromXmlDocument(doc);
        }

        /// <summary>
        /// Loads a GFF from XML bytes.
        /// </summary>
        public GFF Load(byte[] xmlBytes)
        {
            using (var ms = new MemoryStream(xmlBytes))
            {
                return Load(ms);
            }
        }

        private GFF LoadFromXmlDocument(XDocument doc)
        {
            var gff3Element = doc.Element("gff3");
            if (gff3Element == null)
            {
                throw new XmlException("Root element must be 'gff3'");
            }

            var structElement = gff3Element.Element("struct");
            if (structElement == null)
            {
                throw new XmlException("gff3 element must contain a 'struct' element");
            }

            // Parse struct id from attributes
            int structId = 0;
            var idAttr = structElement.Attribute("id");
            if (idAttr != null && !string.IsNullOrEmpty(idAttr.Value))
            {
                if (!int.TryParse(idAttr.Value, out structId))
                {
                    structId = 0;
                }
            }

            // Create GFF with default DLG content type
            var gff = new GFF(GFFContent.DLG);
            gff.Header.FileType = "DLG ";
            gff.Header.FileVersion = "V3.2";

            // Parse the root struct
            gff.Root = ParseStruct(structElement, structId);

            return gff;
        }

        private GFFStruct ParseStruct(XElement structElement, int structId)
        {
            var gffStruct = new GFFStruct(structId);

            foreach (var element in structElement.Elements())
            {
                string fieldName = element.Attribute("label")?.Value;
                if (string.IsNullOrEmpty(fieldName))
                {
                    continue;
                }

                string fieldType = element.Name.LocalName;
                object value = ParseFieldValue(element, fieldType);
                GFFFieldType gffFieldType = GetGFFFieldType(fieldType);

                gffStruct.SetField(fieldName, gffFieldType, value);
            }

            return gffStruct;
        }

        private object ParseFieldValue(XElement element, string fieldType)
        {
            switch (fieldType)
            {
                case "uint8":
                case "byte":
                    return byte.Parse(element.Value);
                case "sint32":
                case "int32":
                    return int.Parse(element.Value);
                case "uint32":
                    return uint.Parse(element.Value);
                case "sint16":
                case "int16":
                    return short.Parse(element.Value);
                case "uint16":
                    return ushort.Parse(element.Value);
                case "float":
                case "single":
                    return float.Parse(element.Value, CultureInfo.InvariantCulture);
                case "exostring":
                    return element.Value;
                case "resref":
                    return new ResRef(element.Value ?? string.Empty);
                case "locstring":
                    return ParseLocString(element);
                case "list":
                    return ParseList(element);
                case "struct":
                    return ParseStruct(element, int.Parse(element.Attribute("id")?.Value ?? "0"));
                default:
                    throw new XmlException($"Unknown field type: {fieldType}");
            }
        }

        private LocalizedString ParseLocString(XElement locStringElement)
        {
            // Check if there's a strref attribute
            int strref = -1;
            var strrefAttr = locStringElement.Attribute("strref");
            if (strrefAttr != null)
            {
                if (!int.TryParse(strrefAttr.Value, out strref))
                {
                    strref = -1;
                }
            }

            var substrings = new Dictionary<int, string>();
            var locString = new LocalizedString(strref, substrings);

            // Parse string elements
            foreach (var stringElement in locStringElement.Elements("string"))
            {
                var languageAttr = stringElement.Attribute("language");
                var genderAttr = stringElement.Attribute("gender");

                if (languageAttr != null && genderAttr != null)
                {
                    if (Enum.TryParse<Language>(languageAttr.Value, out var language) &&
                        Enum.TryParse<Gender>(genderAttr.Value, out var gender))
                    {
                        locString.SetData(language, gender, stringElement.Value);
                    }
                }
            }

            return locString;
        }

        private GFFList ParseList(XElement listElement)
        {
            var gffList = new GFFList();

            foreach (var structElement in listElement.Elements("struct"))
            {
                int structId = int.Parse(structElement.Attribute("id")?.Value ?? "0");
                var gffStruct = ParseStruct(structElement, structId);
                gffList.Add(gffStruct);
            }

            return gffList;
        }

        private GFFFieldType GetGFFFieldType(string xmlType)
        {
            switch (xmlType)
            {
                case "uint8":
                case "byte":
                    return GFFFieldType.UInt8;
                case "sint32":
                case "int32":
                    return GFFFieldType.Int32;
                case "uint32":
                    return GFFFieldType.UInt32;
                case "sint16":
                case "int16":
                    return GFFFieldType.Int16;
                case "uint16":
                    return GFFFieldType.UInt16;
                case "float":
                case "single":
                    return GFFFieldType.Single;
                case "exostring":
                    return GFFFieldType.String;
                case "resref":
                    return GFFFieldType.ResRef;
                case "locstring":
                    return GFFFieldType.LocalizedString;
                case "list":
                    return GFFFieldType.List;
                case "struct":
                    return GFFFieldType.Struct;
                default:
                    throw new XmlException($"Unknown XML field type: {xmlType}");
            }
        }
    }
}
