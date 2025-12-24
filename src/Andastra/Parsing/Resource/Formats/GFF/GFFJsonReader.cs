using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Formats.GFF
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
            using var document = JsonDocument.Parse(jsonText);
            return LoadFromJsonElement(document.RootElement);
        }

        /// <summary>
        /// Loads a GFF from JSON data.
        /// </summary>
        public GFF Load(Stream jsonStream)
        {
            using var document = JsonDocument.Parse(jsonStream);
            return LoadFromJsonElement(document.RootElement);
        }

        /// <summary>
        /// Loads a GFF from JSON data.
        /// </summary>
        public GFF Load(byte[] jsonBytes)
        {
            using var document = JsonDocument.Parse(jsonBytes);
            return LoadFromJsonElement(document.RootElement);
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
                gffStruct.Set(property.Name, fieldType, value);
            }

            return gffStruct;
        }

        private GFFFieldType ParseFieldType(string dataType)
        {
            return dataType switch
            {
                "UInt8" => GFFFieldType.UInt8,
                "Int8" => GFFFieldType.Int8,
                "UInt16" => GFFFieldType.UInt16,
                "Int16" => GFFFieldType.Int16,
                "UInt32" => GFFFieldType.UInt32,
                "Int32" => GFFFieldType.Int32,
                "UInt64" => GFFFieldType.UInt64,
                "Int64" => GFFFieldType.Int64,
                "Single" => GFFFieldType.Single,
                "Double" => GFFFieldType.Double,
                "String" => GFFFieldType.String,
                "ResRef" => GFFFieldType.ResRef,
                "LocalizedString" => GFFFieldType.LocalizedString,
                "Binary" => GFFFieldType.Binary,
                "Struct" => GFFFieldType.Struct,
                "List" => GFFFieldType.List,
                "Vector3" => GFFFieldType.Vector3,
                "Vector4" => GFFFieldType.Vector4,
                _ => throw new ArgumentException($"Unknown GFF field type: {dataType}")
            };
        }

        private object ParseFieldValue(JsonElement fieldElement, GFFFieldType fieldType)
        {
            if (!fieldElement.TryGetProperty("__value__", out JsonElement valueElement))
            {
                throw new JsonException("Field is missing __value__ property");
            }

            return fieldType switch
            {
                GFFFieldType.UInt8 => valueElement.GetByte(),
                GFFFieldType.Int8 => valueElement.GetSByte(),
                GFFFieldType.UInt16 => valueElement.GetUInt16(),
                GFFFieldType.Int16 => valueElement.GetInt16(),
                GFFFieldType.UInt32 => (uint)valueElement.GetInt64(),
                GFFFieldType.Int32 => valueElement.GetInt32(),
                GFFFieldType.UInt64 => valueElement.GetUInt64(),
                GFFFieldType.Int64 => valueElement.GetInt64(),
                GFFFieldType.Single => valueElement.GetSingle(),
                GFFFieldType.Double => valueElement.GetDouble(),
                GFFFieldType.String => valueElement.GetString(),
                GFFFieldType.ResRef => ResRef.From(valueElement.GetString()),
                GFFFieldType.LocalizedString => ParseLocalizedString(valueElement),
                GFFFieldType.Binary => ParseBinary(valueElement),
                GFFFieldType.Struct => ParseStruct(valueElement),
                GFFFieldType.List => ParseList(valueElement),
                GFFFieldType.Vector3 => ParseVector3(valueElement),
                GFFFieldType.Vector4 => ParseVector4(valueElement),
                _ => throw new ArgumentException($"Unsupported field type: {fieldType}")
            };
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
