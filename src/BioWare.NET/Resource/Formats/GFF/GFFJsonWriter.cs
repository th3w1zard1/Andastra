using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.GFF
{
    /// <summary>
    /// Writes GFF data to JSON format.
    /// 1:1 port of Python gff_json_writer.py from pykotor/resource/formats/gff/
    /// </summary>
    public class GFFJsonWriter
    {
        private readonly JsonWriterOptions _options = new JsonWriterOptions
        {
            Indented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Writes a GFF to JSON string.
        /// </summary>
        public string Write(GFF gff)
        {
            using (var stream = new MemoryStream())
            {
                Write(gff, stream);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Writes a GFF to JSON stream.
        /// </summary>
        public void Write(GFF gff, Stream stream)
        {
            using (var writer = new Utf8JsonWriter(stream, _options))
            {
                WriteGff(writer, gff);
            }
        }

        /// <summary>
        /// Writes a GFF to JSON bytes.
        /// </summary>
        public byte[] WriteBytes(GFF gff)
        {
            using (var stream = new MemoryStream())
            {
                Write(gff, stream);
                return stream.ToArray();
            }
        }

        private void WriteGff(Utf8JsonWriter writer, GFF gff)
        {
            writer.WriteStartObject();

            // Write GFF metadata
            writer.WriteString("__gff_type__", gff.Header.FileType);
            writer.WriteString("__gff_version__", gff.Header.FileVersion);
            writer.WriteNumber("__struct_id__", gff.Root.StructId);

            // Write the root struct fields
            WriteStructFields(writer, gff.Root);

            writer.WriteEndObject();
        }

        private void WriteStructFields(Utf8JsonWriter writer, GFFStruct gffStruct)
        {
            foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
            {
                writer.WritePropertyName(label);
                WriteField(writer, fieldType, value);
            }
        }

        private void WriteField(Utf8JsonWriter writer, GFFFieldType fieldType, object value)
        {
            writer.WriteStartObject();
            writer.WriteString("__data_type__", fieldType.ToString());

            writer.WritePropertyName("__value__");

            switch (fieldType)
            {
                case GFFFieldType.UInt8:
                    writer.WriteNumberValue((byte)value);
                    break;
                case GFFFieldType.Int8:
                    writer.WriteNumberValue((sbyte)value);
                    break;
                case GFFFieldType.UInt16:
                    writer.WriteNumberValue((ushort)value);
                    break;
                case GFFFieldType.Int16:
                    writer.WriteNumberValue((short)value);
                    break;
                case GFFFieldType.UInt32:
                    writer.WriteNumberValue((uint)value);
                    break;
                case GFFFieldType.Int32:
                    writer.WriteNumberValue((int)value);
                    break;
                case GFFFieldType.UInt64:
                    writer.WriteNumberValue((ulong)value);
                    break;
                case GFFFieldType.Int64:
                    writer.WriteNumberValue((long)value);
                    break;
                case GFFFieldType.Single:
                    writer.WriteNumberValue((float)value);
                    break;
                case GFFFieldType.Double:
                    writer.WriteNumberValue((double)value);
                    break;
                case GFFFieldType.String:
                    writer.WriteStringValue((string)value);
                    break;
                case GFFFieldType.ResRef:
                    writer.WriteStringValue(((ResRef)value).ToString());
                    break;
                case GFFFieldType.LocalizedString:
                    WriteLocalizedString(writer, (LocalizedString)value);
                    break;
                case GFFFieldType.Binary:
                    WriteBinary(writer, (byte[])value);
                    break;
                case GFFFieldType.Struct:
                    WriteStruct(writer, (GFFStruct)value);
                    break;
                case GFFFieldType.List:
                    WriteList(writer, (GFFList)value);
                    break;
                case GFFFieldType.Vector3:
                    WriteVector3(writer, (Vector3)value);
                    break;
                case GFFFieldType.Vector4:
                    WriteVector4(writer, (Vector4)value);
                    break;
                default:
                    throw new ArgumentException($"Unsupported field type: {fieldType}");
            }

            writer.WriteEndObject();
        }

        private void WriteLocalizedString(Utf8JsonWriter writer, LocalizedString locString)
        {
            writer.WriteStartObject();
            writer.WriteNumber("string_ref", locString.StringRef);

            var dict = locString.ToDictionary();
            if (dict.ContainsKey("substrings") && dict["substrings"] is Dictionary<int, string> substrings && substrings.Count > 0)
            {
                writer.WritePropertyName("substrings");
                writer.WriteStartObject();
                foreach (var kvp in substrings)
                {
                    writer.WriteString(kvp.Key.ToString(), kvp.Value);
                }
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private void WriteBinary(Utf8JsonWriter writer, byte[] data)
        {
            // Binary data is base64 encoded
            string base64String = Convert.ToBase64String(data);
            writer.WriteStringValue(base64String);
        }

        private void WriteStruct(Utf8JsonWriter writer, GFFStruct gffStruct)
        {
            writer.WriteStartObject();
            writer.WriteNumber("__struct_id__", gffStruct.StructId);
            WriteStructFields(writer, gffStruct);
            writer.WriteEndObject();
        }

        private void WriteList(Utf8JsonWriter writer, GFFList list)
        {
            writer.WriteStartArray();

            foreach (GFFStruct item in list)
            {
                writer.WriteStartObject();
                writer.WriteNumber("__struct_id__", item.StructId);
                WriteStructFields(writer, item);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        private void WriteVector3(Utf8JsonWriter writer, Vector3 vector)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", vector.X);
            writer.WriteNumber("y", vector.Y);
            writer.WriteNumber("z", vector.Z);
            writer.WriteEndObject();
        }

        private void WriteVector4(Utf8JsonWriter writer, Vector4 vector)
        {
            writer.WriteStartObject();
            writer.WriteNumber("x", vector.X);
            writer.WriteNumber("y", vector.Y);
            writer.WriteNumber("z", vector.Z);
            writer.WriteNumber("w", vector.W);
            writer.WriteEndObject();
        }
    }
}
