using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Resource.Formats.GFF
{
    /// <summary>
    /// Reads GFF data from JSON format.
    /// 1:1 port of Python gff_json_reader.py from pykotor/resource/formats/gff/
    /// </summary>
    public class GFFJsonReader
    {
        /// <summary>
        /// Loads a GFF from JSON data.
        /// </summary>
        public GFF Load(string jsonText)
        {
            using (var document = JsonDocument.Parse(jsonText))
            {
                return LoadFromJsonElement(document.RootElement);
            }
        }

        /// <summary>
        /// Loads a GFF from JSON data.
        /// </summary>
        public GFF Load(Stream jsonStream)
        {
            using (var document = JsonDocument.Parse(jsonStream))
            {
                return LoadFromJsonElement(document.RootElement);
            }
        }

        /// <summary>
        /// Loads a GFF from JSON data.
        /// </summary>
        public GFF Load(byte[] jsonBytes)
        {
            using (var document = JsonDocument.Parse(jsonBytes))
            {
                return LoadFromJsonElement(document.RootElement);
            }
        }

        private GFF LoadFromJsonElement(JsonElement rootElement)
        {
            // Extract GFF metadata
            string gffType = rootElement.GetProperty("__gff_type__").GetString();
            string gffVersion = rootElement.GetProperty("__gff_version__").GetString();
            int structId = rootElement.GetProperty("__struct_id__").GetInt32();

            // Create GFF object
            var gff = new GFF(GFFContentExtensions.FromFourCC(gffType));
            gff.Header.FileType = gffType;
            gff.Header.FileVersion = gffVersion;

            // Parse the root struct
            gff.Root = ParseStruct(rootElement, structId);

            return gff;
        }

        private GFFStruct ParseStruct(JsonElement element, int structId)
        {
            var gffStruct = new GFFStruct(structId);

            foreach (JsonProperty property in element.EnumerateObject())
            {
                // Skip metadata properties
                if (property.Name.StartsWith("__") && property.Name.EndsWith("__"))
                {
                    continue;
                }

                var fieldElement = property.Value;
                if (!fieldElement.TryGetProperty("__data_type__", out JsonElement dataTypeElement))
                {
                    throw new JsonException($"Field '{property.Name}' is missing __data_type__ property");
                }

                string dataType = dataTypeElement.GetString();
                GFFFieldType fieldType = ParseFieldType(dataType);

                object value = ParseFieldValue(fieldElement, fieldType);
                gffStruct.SetField(property.Name, fieldType, value);
            }

            return gffStruct;
        }

        private GFFFieldType ParseFieldType(string dataType)
        {
            switch (dataType)
            {
                case "UInt8":
                    return GFFFieldType.UInt8;
                case "Int8":
                    return GFFFieldType.Int8;
                case "UInt16":
                    return GFFFieldType.UInt16;
                case "Int16":
                    return GFFFieldType.Int16;
                case "UInt32":
                    return GFFFieldType.UInt32;
                case "Int32":
                    return GFFFieldType.Int32;
                case "UInt64":
                    return GFFFieldType.UInt64;
                case "Int64":
                    return GFFFieldType.Int64;
                case "Single":
                    return GFFFieldType.Single;
                case "Double":
                    return GFFFieldType.Double;
                case "String":
                    return GFFFieldType.String;
                case "ResRef":
                    return GFFFieldType.ResRef;
                case "LocalizedString":
                    return GFFFieldType.LocalizedString;
                case "Binary":
                    return GFFFieldType.Binary;
                case "Struct":
                    return GFFFieldType.Struct;
                case "List":
                    return GFFFieldType.List;
                case "Vector3":
                    return GFFFieldType.Vector3;
                case "Vector4":
                    return GFFFieldType.Vector4;
                default:
                    throw new ArgumentException($"Unknown GFF field type: {dataType}");
            }
        }

        private object ParseFieldValue(JsonElement fieldElement, GFFFieldType fieldType)
        {
            if (!fieldElement.TryGetProperty("__value__", out JsonElement valueElement))
            {
                throw new JsonException("Field is missing __value__ property");
            }

            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                    return valueElement.GetByte();
                case GFFFieldType.Int8:
                    return valueElement.GetSByte();
                case GFFFieldType.UInt16:
                    return valueElement.GetUInt16();
                case GFFFieldType.Int16:
                    return valueElement.GetInt16();
                case GFFFieldType.UInt32:
                    return (uint)valueElement.GetInt64();
                case GFFFieldType.Int32:
                    return valueElement.GetInt32();
                case GFFFieldType.UInt64:
                    return valueElement.GetUInt64();
                case GFFFieldType.Int64:
                    return valueElement.GetInt64();
                case GFFFieldType.Single:
                    return valueElement.GetSingle();
                case GFFFieldType.Double:
                    return valueElement.GetDouble();
                case GFFFieldType.String:
                    return valueElement.GetString();
                case GFFFieldType.ResRef:
                    return new ResRef(valueElement.GetString());
                case GFFFieldType.LocalizedString:
                    return ParseLocalizedString(valueElement);
                case GFFFieldType.Binary:
                    return ParseBinary(valueElement);
                case GFFFieldType.Struct:
                    return ParseStruct(valueElement);
                case GFFFieldType.List:
                    return ParseList(valueElement);
                case GFFFieldType.Vector3:
                    return ParseVector3(valueElement);
                case GFFFieldType.Vector4:
                    return ParseVector4(valueElement);
                default:
                    throw new ArgumentException($"Unsupported field type: {fieldType}");
            }
        }

        private LocalizedString ParseLocalizedString(JsonElement element)
        {
            int stringRef = element.GetProperty("string_ref").GetInt32();

            var substrings = new Dictionary<int, string>();
            if (element.TryGetProperty("substrings", out JsonElement substringsElement))
            {
                foreach (JsonProperty substringProperty in substringsElement.EnumerateObject())
                {
                    int languageId = int.Parse(substringProperty.Name);
                    substrings[languageId] = substringProperty.Value.GetString();
                }
            }

            return new LocalizedString(stringRef, substrings);
        }

        private byte[] ParseBinary(JsonElement element)
        {
            // Binary data is base64 encoded
            string base64String = element.GetString();
            return Convert.FromBase64String(base64String);
        }

        private GFFStruct ParseStruct(JsonElement element)
        {
            int structId = element.GetProperty("__struct_id__").GetInt32();
            return ParseStruct(element, structId);
        }

        private GFFList ParseList(JsonElement element)
        {
            var list = new GFFList();

            foreach (JsonElement itemElement in element.EnumerateArray())
            {
                int structId = itemElement.GetProperty("__struct_id__").GetInt32();
                GFFStruct structItem = ParseStruct(itemElement, structId);
                list.Add(structItem);
            }

            return list;
        }

        private Vector3 ParseVector3(JsonElement element)
        {
            float x = element.GetProperty("x").GetSingle();
            float y = element.GetProperty("y").GetSingle();
            float z = element.GetProperty("z").GetSingle();
            return new Vector3(x, y, z);
        }

        private Vector4 ParseVector4(JsonElement element)
        {
            float x = element.GetProperty("x").GetSingle();
            float y = element.GetProperty("y").GetSingle();
            float z = element.GetProperty("z").GetSingle();
            float w = element.GetProperty("w").GetSingle();
            return new Vector4(x, y, z, w);
        }
    }
}
