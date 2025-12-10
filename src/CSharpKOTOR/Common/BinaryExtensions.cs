using System.IO;
using System.Text;

namespace CSharpKOTOR.Common
{

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
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
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
            writer.Write(value.X);
            writer.Write(value.Y);
            writer.Write(value.Z);
            writer.Write(value.W);
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
                var encoding = Encoding.GetEncoding(LanguageExtensions.GetEncoding(language));
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
            using (var ms = new MemoryStream())
            using (var tempWriter = new BinaryWriter(ms))
            {
                // Write StringRef
                uint stringref = value.StringRef == -1 ? 0xFFFFFFFF : (uint)value.StringRef;
                tempWriter.Write(stringref);

                // Write string count
                tempWriter.Write((uint)value.Count);

                // Write all substrings
                foreach ((Language language, Gender gender, string text) in value)
                {
                    int stringId = LocalizedString.SubstringId(language, gender);
                    tempWriter.Write((uint)stringId);

                    var encoding = Encoding.GetEncoding(LanguageExtensions.GetEncoding(language));
                    byte[] textBytes = encoding.GetBytes(text);
                    tempWriter.Write((uint)textBytes.Length);
                    tempWriter.Write(textBytes);
                }

                // Write total length + data
                byte[] locstringData = ms.ToArray();
                writer.Write((uint)locstringData.Length);
                writer.Write(locstringData);
            }
        }
    }
}

