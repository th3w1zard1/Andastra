using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using BioWare.NET.Common;

namespace BioWare.NET.Common
{
    /// <summary>
    /// Extension methods for Vector3 and Vector4.
    /// </summary>
    public static class VectorExtensions
    {
        /// <summary>
        /// Checks if a Vector3 is within a list of vectors (within epsilon distance).
        /// </summary>
        public static bool Within(this Vector3 vector, List<Vector3> vectors, float epsilon = 1e-6f)
        {
            foreach (var v in vectors)
            {
                if (Vector3.Distance(vector, v) < epsilon)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Computes the dot product of two vectors.
        /// </summary>
        public static float Dot(this Vector3 vector, Vector3 other)
        {
            return Vector3.Dot(vector, other);
        }

        /// <summary>
        /// Gets the magnitude (length) of a vector.
        /// </summary>
        public static float Magnitude(this Vector3 vector)
        {
            return vector.Length();
        }
    }

    /// <summary>
    /// Extension methods for BinaryReader and BinaryWriter to handle complex game types.
    /// </summary>
    public static class BinaryExtensions
    {
        static BinaryExtensions()
        {
            // Register CodePages encoding provider for Windows encodings
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static Vector3 ReadVector3(this BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            return new Vector3(x, y, z);
        }

        public static void WriteVector3(this BinaryWriter writer, Vector3 value)
        {
            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
        }

        public static Vector4 ReadVector4(this BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float w = reader.ReadSingle();
            return new Vector4(x, y, z, w);
        }

        public static void WriteVector4(this BinaryWriter writer, Vector4 value)
        {
            writer.WriteSingle(value.X);
            writer.WriteSingle(value.Y);
            writer.WriteSingle(value.Z);
            writer.WriteSingle(value.W);
        }

        /// <summary>
        /// Reads a LocalizedString following the GFF format specification.
        /// </summary>
        public static LocalizedString ReadLocalizedString(this BinaryReader reader)
        {
            var locString = LocalizedString.FromInvalid();

            reader.ReadUInt32(); // Total length (not used during reading)
            uint stringref = reader.ReadUInt32();
            locString.StringRef = stringref == 0xFFFFFFFF ? -1 : (int)stringref;
            uint stringCount = reader.ReadUInt32();

            for (int i = 0; i < stringCount; i++)
            {
                uint stringId = reader.ReadUInt32();
                Language language;
                Gender gender;
                LocalizedString.SubstringPair((int)stringId, out language, out gender);
                uint length = reader.ReadUInt32();

                // Get encoding for the language
                string encodingName = LanguageExtensions.GetEncoding(language);
                Encoding encoding;
                try
                {
                    encoding = Encoding.GetEncoding(encodingName);
                }
                catch
                {
                    encoding = Encoding.UTF8;
                }
                byte[] textBytes = reader.ReadBytes((int)length);
                string text = encoding.GetString(textBytes).TrimEnd('\0');

                locString.SetData(language, gender, text);
            }

            return locString;
        }

        /// <summary>
        /// Writes a LocalizedString following the GFF format specification.
        /// </summary>
        public static void WriteLocalizedString(this BinaryWriter writer, LocalizedString value)
        {
            // Build the locstring data first to calculate total length
            using (var tempWriter = BinaryWriter.ToByteArray())
            {
                // Write StringRef
                uint stringref = value.StringRef == -1 ? 0xFFFFFFFF : (uint)value.StringRef;
                tempWriter.WriteUInt32(stringref);

                // Write string count
                tempWriter.WriteUInt32((uint)value.Count);

                // Write all substrings
                foreach ((Language language, Gender gender, string text) in value)
                {
                    int stringId = LocalizedString.SubstringId(language, gender);
                    tempWriter.WriteUInt32((uint)stringId);

                    string encodingName = LanguageExtensions.GetEncoding(language);
                    Encoding encoding;
                    try
                    {
                        encoding = Encoding.GetEncoding(encodingName);
                    }
                    catch
                    {
                        encoding = Encoding.UTF8;
                    }
                    byte[] textBytes = encoding.GetBytes(text);
                    tempWriter.WriteUInt32((uint)textBytes.Length);
                    tempWriter.WriteBytes(textBytes);
                }

                // Write total length + data
                byte[] locstringData = tempWriter.Data();
                writer.WriteUInt32((uint)locstringData.Length);
                writer.WriteBytes(locstringData);
            }
        }
    }
}
