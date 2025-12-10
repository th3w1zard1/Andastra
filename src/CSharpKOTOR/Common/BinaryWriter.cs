using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace CSharpKOTOR.Common
{

    /// <summary>
    /// Binary writer with enhanced functionality matching Python's RawBinaryWriter.
    /// Provides file and memory-based writing with encoding support.
    /// </summary>
    /// <remarks>
    /// Python Reference: g:/GitHub/PyKotor/Libraries/PyKotor/src/utility/common/stream.py
    /// </remarks>
    public abstract class RawBinaryWriter : IDisposable
    {
        public bool AutoClose { get; set; } = true;

        /// <summary>
        /// Creates a writer for a file.
        /// </summary>
        public static RawBinaryWriterFile ToFile(string path)
        {
            var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            return new RawBinaryWriterFile(stream);
        }

        /// <summary>
        /// Creates a writer for a stream.
        /// </summary>
        public static RawBinaryWriterFile ToStream(Stream stream, int offset = 0)
        {
            if (!stream.CanWrite)
            {
                throw new ArgumentException("Stream must be writable", nameof(stream));
            }
            return new RawBinaryWriterFile(stream, offset);
        }

        /// <summary>
        /// Creates a writer for a byte array (in-memory).
        /// </summary>
        public static RawBinaryWriterMemory ToByteArray([CanBeNull] byte[] data = null)
        {
            return new RawBinaryWriterMemory(data ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Creates a writer from a path, stream, byte array, or existing writer (auto-detect).
        /// </summary>
        public static RawBinaryWriter ToAuto(object source, int offset = 0)
        {
            if (source is string path)
            {
                return ToFile(path);
            }

            if (source is byte[] bytes)
            {
                return ToByteArray(bytes);
            }

            if (source is RawBinaryWriterFile fileWriter)
            {
                return new RawBinaryWriterFile(fileWriter.GetStream(), offset);
            }

            if (source is RawBinaryWriterMemory memoryWriter)
            {
                return new RawBinaryWriterMemory(memoryWriter.Data(), offset);
            }

            if (source is Stream stream && stream.CanWrite)
            {
                return ToStream(stream, offset);
            }

            throw new ArgumentException("Unsupported source type for ToAuto");
        }

        /// <summary>
        /// Convenience method to write raw data to a file.
        /// </summary>
        public static void Dump(string path, byte[] data)
        {
            File.WriteAllBytes(path, data);
        }

        public abstract void Close();
        public abstract int Size();
        public abstract byte[] Data();
        public abstract void Clear();
        public abstract void Seek(int position);
        public abstract void End();
        public abstract int Position();

        // Primitive type writers
        public abstract void WriteUInt8(byte value);
        public abstract void WriteInt8(sbyte value);
        public abstract void WriteUInt16(ushort value, bool bigEndian = false);
        public abstract void WriteInt16(short value, bool bigEndian = false);
        public abstract void WriteUInt32(uint value, bool bigEndian = false, bool maxNeg1 = false);
        public abstract void WriteInt32(int value, bool bigEndian = false);
        public abstract void WriteUInt64(ulong value, bool bigEndian = false);
        public abstract void WriteInt64(long value, bool bigEndian = false);
        public abstract void WriteSingle(float value, bool bigEndian = false);
        public abstract void WriteDouble(double value, bool bigEndian = false);
        public abstract void WriteVector3(Vector3 value, bool bigEndian = false);
        public abstract void WriteVector4(Vector4 value, bool bigEndian = false);
        public abstract void WriteBytes(byte[] value);
        public abstract void WriteString(string value, [CanBeNull] string encoding = "windows-1252", int prefixLength = 0, int stringLength = -1, char padding = '\0', bool bigEndian = false);
        public abstract void WriteLocalizedString(LocalizedString value, bool bigEndian = false);

        public abstract void Dispose();
    }

    /// <summary>
    /// File-based binary writer.
    /// </summary>
    public class RawBinaryWriterFile : RawBinaryWriter
    {
        private readonly Stream _stream;
        private readonly int _offset;

        public RawBinaryWriterFile(Stream stream, int offset = 0)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _offset = offset;
            _stream.Seek(offset, SeekOrigin.Begin);
        }

        public override void Close()
        {
            _stream.Close();
        }

        public override int Size()
        {
            long pos = _stream.Position;
            _stream.Seek(0, SeekOrigin.End);
            long size = _stream.Position;
            _stream.Seek(pos, SeekOrigin.Begin);
            return (int)size;
        }

        public override byte[] Data()
        {
            long pos = _stream.Position;
            _stream.Seek(0, SeekOrigin.Begin);

            byte[] data = new byte[_stream.Length];
            _stream.Read(data, 0, data.Length);

            _stream.Seek(pos, SeekOrigin.Begin);
            return data;
        }

        public override void Clear()
        {
            _stream.SetLength(0);
        }

        public override void Seek(int position)
        {
            _stream.Seek(position + _offset, SeekOrigin.Begin);
        }

        public override void End()
        {
            _stream.Seek(0, SeekOrigin.End);
        }

        public override int Position()
        {
            return (int)_stream.Position - _offset;
        }

        public override void WriteUInt8(byte value)
        {
            _stream.WriteByte(value);
        }

        public override void WriteInt8(sbyte value)
        {
            _stream.WriteByte((byte)value);
        }

        public override void WriteUInt16(ushort value, bool bigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteInt16(short value, bool bigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteUInt32(uint value, bool bigEndian = false, bool maxNeg1 = false)
        {
            if (maxNeg1 && (int)value == -1)
            {
                value = 0xFFFFFFFF;
            }

            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteInt32(int value, bool bigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteUInt64(ulong value, bool bigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteInt64(long value, bool bigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteSingle(float value, bool bigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteDouble(double value, bool bigEndian = false)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            _stream.Write(bytes, 0, bytes.Length);
        }

        public override void WriteVector3(Vector3 value, bool bigEndian = false)
        {
            WriteSingle(value.X, bigEndian);
            WriteSingle(value.Y, bigEndian);
            WriteSingle(value.Z, bigEndian);
        }

        public override void WriteVector4(Vector4 value, bool bigEndian = false)
        {
            WriteSingle(value.X, bigEndian);
            WriteSingle(value.Y, bigEndian);
            WriteSingle(value.Z, bigEndian);
            WriteSingle(value.W, bigEndian);
        }

        public override void WriteBytes(byte[] value)
        {
            _stream.Write(value, 0, value.Length);
        }

        public override void WriteString(string value, [CanBeNull] string encoding = "windows-1252", int prefixLength = 0, int stringLength = -1, char padding = '\0', bool bigEndian = false)
        {
            // Write length prefix if specified
            if (prefixLength == 1)
            {
                if (value.Length > 0xFF)
                {
                    throw new ArgumentException("String length too large for prefix length of 1");
                }

                WriteUInt8((byte)value.Length);
            }
            else if (prefixLength == 2)
            {
                if (value.Length > 0xFFFF)
                {
                    throw new ArgumentException("String length too large for prefix length of 2");
                }

                WriteUInt16((ushort)value.Length, bigEndian);
            }
            else if (prefixLength == 4)
            {
                if (value.Length > int.MaxValue)
                {
                    throw new ArgumentException("String length too large for prefix length of 4");
                }

                WriteUInt32((uint)value.Length, bigEndian);
            }

            // Pad or truncate if stringLength specified
            if (stringLength != -1)
            {
                if (value.Length < stringLength)
                {
                    value = value.PadRight(stringLength, padding);
                }
                else if (value.Length > stringLength)
                {
                    value = value.Substring(0, stringLength);
                }
            }

            // Write the string bytes
            Encoding enc = encoding != null
                ? Encoding.GetEncoding(encoding)
                : Encoding.UTF8;

            byte[] bytes = enc.GetBytes(value);
            _stream.Write(bytes, 0, bytes.Length);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:85-143
        // Original: def write_locstring(self, value: LocalizedString, *, big: bool = False):
        public override void WriteLocalizedString(LocalizedString value, bool bigEndian = false)
        {
            using (var ms = new MemoryStream())
            using (var tempWriter = new RawBinaryWriterFile(ms))
            {
                uint stringref = value.StringRef == -1 ? 0xFFFFFFFF : (uint)value.StringRef;
                tempWriter.WriteUInt32(stringref, bigEndian);
                tempWriter.WriteUInt32((uint)value.Count, bigEndian);

                foreach ((Language language, Gender gender, string text) in value)
                {
                    int stringId = LocalizedString.SubstringId(language, gender);
                    tempWriter.WriteUInt32((uint)stringId, bigEndian);

                    string encodingName = LanguageExtensions.GetEncoding(language);
                    Encoding encoding = encodingName != null
                        ? Encoding.GetEncoding(encodingName, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback)
                        : Encoding.GetEncoding("windows-1252", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);

                    byte[] textBytes = encoding.GetBytes(text);
                    tempWriter.WriteUInt32((uint)textBytes.Length, bigEndian);
                    tempWriter.WriteBytes(textBytes);
                }

                byte[] locstringData = ms.ToArray();
                WriteUInt32((uint)locstringData.Length);
                WriteBytes(locstringData);
            }
        }

        public override void Dispose()
        {
            if (AutoClose)
            {
                _stream?.Dispose();
            }
        }
    }

    /// <summary>
    /// Memory-based binary writer (writes to byte array).
    /// </summary>
    public class RawBinaryWriterMemory : RawBinaryWriter
    {
        private byte[] _data;
        private int _position;
        private readonly int _offset;

        public RawBinaryWriterMemory(byte[] data, int offset = 0)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _offset = offset;
            _position = 0;
        }

        public override void Close()
        {
            // Nothing to close for memory-based writer
        }

        public override int Size()
        {
            return _data.Length;
        }

        public override byte[] Data()
        {
            return _data;
        }

        public override void Clear()
        {
            _data = Array.Empty<byte>();
            _position = 0;
        }

        public override void Seek(int position)
        {
            _position = position;
        }

        public override void End()
        {
            _position = _data.Length;
        }

        public override int Position()
        {
            return _position - _offset;
        }

        private void EnsureCapacity(int length)
        {
            int required = _position + length;
            if (_data.Length < required)
            {
                Array.Resize(ref _data, required);
            }
        }

        public override void WriteUInt8(byte value)
        {
            EnsureCapacity(1);
            _data[_position++] = value;
        }

        public override void WriteInt8(sbyte value)
        {
            EnsureCapacity(1);
            _data[_position++] = (byte)value;
        }

        public override void WriteUInt16(ushort value, bool bigEndian = false)
        {
            EnsureCapacity(2);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 2);
            _position += 2;
        }

        public override void WriteInt16(short value, bool bigEndian = false)
        {
            EnsureCapacity(2);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 2);
            _position += 2;
        }

        public override void WriteUInt32(uint value, bool bigEndian = false, bool maxNeg1 = false)
        {
            if (maxNeg1 && (int)value == -1)
            {
                value = 0xFFFFFFFF;
            }

            EnsureCapacity(4);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 4);
            _position += 4;
        }

        public override void WriteInt32(int value, bool bigEndian = false)
        {
            EnsureCapacity(4);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 4);
            _position += 4;
        }

        public override void WriteUInt64(ulong value, bool bigEndian = false)
        {
            EnsureCapacity(8);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 8);
            _position += 8;
        }

        public override void WriteInt64(long value, bool bigEndian = false)
        {
            EnsureCapacity(8);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 8);
            _position += 8;
        }

        public override void WriteSingle(float value, bool bigEndian = false)
        {
            EnsureCapacity(4);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 4);
            _position += 4;
        }

        public override void WriteDouble(double value, bool bigEndian = false)
        {
            EnsureCapacity(8);
            byte[] bytes = BitConverter.GetBytes(value);
            if (bigEndian != BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            Array.Copy(bytes, 0, _data, _position, 8);
            _position += 8;
        }

        public override void WriteVector3(Vector3 value, bool bigEndian = false)
        {
            WriteSingle(value.X, bigEndian);
            WriteSingle(value.Y, bigEndian);
            WriteSingle(value.Z, bigEndian);
        }

        public override void WriteVector4(Vector4 value, bool bigEndian = false)
        {
            WriteSingle(value.X, bigEndian);
            WriteSingle(value.Y, bigEndian);
            WriteSingle(value.Z, bigEndian);
            WriteSingle(value.W, bigEndian);
        }

        public override void WriteBytes(byte[] value)
        {
            EnsureCapacity(value.Length);
            Array.Copy(value, 0, _data, _position, value.Length);
            _position += value.Length;
        }

        public override void WriteString(string value, [CanBeNull] string encoding = "windows-1252", int prefixLength = 0, int stringLength = -1, char padding = '\0', bool bigEndian = false)
        {
            // Write length prefix if specified
            if (prefixLength == 1)
            {
                if (value.Length > 0xFF)
                {
                    throw new ArgumentException("String length too large for prefix length of 1");
                }

                WriteUInt8((byte)value.Length);
            }
            else if (prefixLength == 2)
            {
                if (value.Length > 0xFFFF)
                {
                    throw new ArgumentException("String length too large for prefix length of 2");
                }

                WriteUInt16((ushort)value.Length, bigEndian);
            }
            else if (prefixLength == 4)
            {
                if (value.Length > int.MaxValue)
                {
                    throw new ArgumentException("String length too large for prefix length of 4");
                }

                WriteUInt32((uint)value.Length, bigEndian);
            }

            // Pad or truncate if stringLength specified
            if (stringLength != -1)
            {
                if (value.Length < stringLength)
                {
                    value = value.PadRight(stringLength, padding);
                }
                else if (value.Length > stringLength)
                {
                    value = value.Substring(0, stringLength);
                }
            }

            // Write the string bytes
            Encoding enc = encoding != null
                ? Encoding.GetEncoding(encoding)
                : Encoding.UTF8;

            byte[] bytes = enc.GetBytes(value);
            WriteBytes(bytes);
        }

        public override void WriteLocalizedString(LocalizedString value, bool bigEndian = false)
        {
            // Build the locstring data first to calculate total length
            var tempWriter = new RawBinaryWriterMemory(Array.Empty<byte>());

            // Write StringRef
            uint stringref = value.StringRef == -1 ? 0xFFFFFFFF : (uint)value.StringRef;
            tempWriter.WriteUInt32(stringref, bigEndian);

            // Write string count
            tempWriter.WriteUInt32((uint)value.Count, bigEndian);

            // Write all substrings
            foreach ((Language language, Gender gender, string text) in value)
            {
                int stringId = LocalizedString.SubstringId(language, gender);
                tempWriter.WriteUInt32((uint)stringId, bigEndian);

                Encoding encoding = Encoding.GetEncoding(LanguageExtensions.GetEncoding(language));
                byte[] textBytes = encoding.GetBytes(text);
                tempWriter.WriteUInt32((uint)textBytes.Length, bigEndian);
                tempWriter.WriteBytes(textBytes);
            }

            byte[] locstringData = tempWriter.Data();
            WriteUInt32((uint)locstringData.Length);
            WriteBytes(locstringData);
        }

        public override void Dispose()
        {
            // Nothing to dispose for memory-based writer
        }
    }
}

