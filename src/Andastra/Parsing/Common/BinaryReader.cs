using System;
using System.IO;
using System.Numerics;
using System.Text;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Common
{
    /// <summary>
    /// Binary reader with enhanced functionality matching Python's Andastra.Parsing.Common.RawBinaryReader.
    /// Provides offset/size constrained reading, encoding support, and bounds checking.
    /// </summary>
    /// <remarks>
    /// Python Reference: g:/GitHub/PyKotor/Libraries/PyKotor/src/utility/common/stream.py
    /// </remarks>
    public class RawBinaryReader : IDisposable
    {
        // Static initializer to ensure CodePages encoding provider is registered
        static RawBinaryReader()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // Already registered, ignore
            }
        }
        private readonly Stream _stream;
        private readonly int _offset;
        private readonly int _size;
        private int _position;
        public bool AutoClose { get; set; } = true;

        public int Position => _position;
        public int Size => _size;
        public int Offset => _offset;

        public int TrueSize()
        {
            long current = _stream.Position;
            _stream.Seek(0, SeekOrigin.End);
            long size = _stream.Position;
            _stream.Seek(current, SeekOrigin.Begin);
            return (int)size;
        }
        public int Remaining => _size - _position;

        public RawBinaryReader(Stream stream, int offset = 0, int? size = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _offset = offset;
            _stream.Seek(offset, SeekOrigin.Begin);

            int totalSize = TrueSize();
            if (_offset > totalSize - (size ?? 0))
            {
                throw new IOException("Specified offset/size is greater than the number of available bytes.");
            }

            if (size.HasValue && size.Value < 0)
            {
                throw new ArgumentException($"Size must be greater than zero, got {size.Value}", nameof(size));
            }

            _size = size ?? (totalSize - _offset);
            _position = 0;
        }

        /// <summary>
        /// Creates a BinaryReader from a stream.
        /// </summary>
        public static RawBinaryReader FromStream(Stream stream, int offset = 0, int? size = null)
        {
            if (!stream.CanSeek)
            {
                throw new ArgumentException("Stream must be seekable", nameof(stream));
            }

            return new RawBinaryReader(stream, offset, size);
        }

        /// <summary>
        /// Creates a BinaryReader from a file path.
        /// </summary>
        public static RawBinaryReader FromFile(string path, int offset = 0, int? size = null)
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var reader = new RawBinaryReader(stream, offset, size);
            reader.AutoClose = true;
            return reader;
        }

        /// <summary>
        /// Creates a BinaryReader from a byte array.
        /// </summary>
        public static RawBinaryReader FromBytes(byte[] data, int offset = 0, int? size = null)
        {
            var stream = new MemoryStream(data);
            return new RawBinaryReader(stream, offset, size);
        }

        public static RawBinaryReader FromAuto(object source, int offset = 0, int? size = null)
        {
            if (source is string path)
            {
                return FromFile(path, offset, size);
            }

            if (source is byte[] bytes)
            {
                return FromBytes(bytes, offset, size);
            }

            if (source is MemoryStream ms)
            {
                return FromStream(ms, offset, size);
            }

            if (source is Stream stream && stream.CanSeek)
            {
                return FromStream(stream, offset, size);
            }

            var existing = source as RawBinaryReader;
            if (existing != null)
            {
                // share the same underlying stream by creating from stream at current offset
                return FromStream(existing._stream, existing._offset, existing._size);
            }

            throw new ArgumentException("Unsupported source type for FromAuto");
        }

        public static byte[] LoadFile(string path, int offset = 0, int size = -1)
        {
            using (FileStream reader = File.OpenRead(path))
            {
                reader.Seek(offset, SeekOrigin.Begin);
                if (size == -1)
                {
                    using (var ms = new MemoryStream())
                    {
                        reader.CopyTo(ms);
                        return ms.ToArray();
                    }
                }

                byte[] buffer = new byte[size];
                int read = reader.Read(buffer, 0, size);
                if (read < size)
                {
                    Array.Resize(ref buffer, read);
                }
                return buffer;
            }
        }

        /// <summary>
        /// Moves the stream pointer to the specified position (relative to offset).
        /// </summary>
        public void Seek(int position)
        {
            // Check if the absolute position is valid
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Position cannot be negative");
            }
            if (position > _size)
            {
                throw new ArgumentOutOfRangeException(nameof(position), $"Position {position} exceeds size {_size}");
            }

            _stream.Seek(_offset + position, SeekOrigin.Begin);
            _position = position;
        }

        public void SeekRelative(int offset)
        {
            Seek(_position + offset);
        }

        /// <summary>
        /// Skips ahead in the stream by the specified number of bytes.
        /// </summary>
        public void Skip(int length)
        {
            ExceedCheck(length);
            _stream.Seek(length, SeekOrigin.Current);
            _position += length;
        }

        /// <summary>
        /// Peeks at the next bytes without advancing the position.
        /// </summary>
        public byte[] Peek(int length = 1)
        {
            long current = _stream.Position;
            byte[] data = new byte[length];
            int read = _stream.Read(data, 0, length);
            _stream.Seek(current, SeekOrigin.Begin);

            if (read < length)
            {
                Array.Resize(ref data, read);
            }

            return data;
        }

        /// <summary>
        /// Reads all remaining bytes from the stream.
        /// </summary>
        public byte[] ReadAll()
        {
            int remainingBytes = _size - _position;
            byte[] data = new byte[remainingBytes];
            int read = _stream.Read(data, 0, remainingBytes);
            _position += read;

            if (read < remainingBytes)
            {
                Array.Resize(ref data, read);
            }

            return data;
        }

        /// <summary>
        /// Reads a specified number of bytes from the stream.
        /// </summary>
        public byte[] ReadBytes(int length)
        {
            ExceedCheck(length);
            byte[] data = new byte[length];
            int read = _stream.Read(data, 0, length);
            _position += read;

            if (read < length)
            {
                Array.Resize(ref data, read);
            }

            return data;
        }

        // Primitive type readers
        public byte ReadUInt8()
        {
            ExceedCheck(1);
            int value = _stream.ReadByte();
            if (value == -1)
            {
                throw new EndOfStreamException();
            }

            _position++;
            return (byte)value;
        }

        public sbyte ReadInt8()
        {
            ExceedCheck(1);
            int value = _stream.ReadByte();
            if (value == -1)
            {
                throw new EndOfStreamException();
            }

            _position++;
            return (sbyte)value;
        }

        public ushort ReadUInt16(bool bigEndian = false)
        {
            ExceedCheck(2);
            byte[] bytes = new byte[2];
            int read = _stream.Read(bytes, 0, 2);
            if (read != 2)
            {
                throw new EndOfStreamException($"Expected to read 2 bytes, but only read {read}");
            }
            _position += 2;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt16(bytes, 0);
        }

        public short ReadInt16(bool bigEndian = false)
        {
            ExceedCheck(2);
            byte[] bytes = new byte[2];
            int read = _stream.Read(bytes, 0, 2);
            if (read != 2)
            {
                throw new EndOfStreamException($"Expected to read 2 bytes, but only read {read}");
            }
            _position += 2;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToInt16(bytes, 0);
        }

        public uint ReadUInt32(bool bigEndian = false, bool maxNeg1 = false)
        {
            ExceedCheck(4);
            byte[] bytes = new byte[4];
            int read = _stream.Read(bytes, 0, 4);
            if (read != 4)
            {
                throw new EndOfStreamException($"Expected to read 4 bytes, but only read {read}");
            }
            _position += 4;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            uint value = BitConverter.ToUInt32(bytes, 0);

            if (maxNeg1 && value == 0xFFFFFFFF)
            {
                return unchecked((uint)-1);
            }

            return value;
        }

        public int ReadInt32(bool bigEndian = false)
        {
            ExceedCheck(4);
            byte[] bytes = new byte[4];
            _stream.Read(bytes, 0, 4);
            _position += 4;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToInt32(bytes, 0);
        }

        public ulong ReadUInt64(bool bigEndian = false)
        {
            ExceedCheck(8);
            byte[] bytes = new byte[8];
            int read = _stream.Read(bytes, 0, 8);
            if (read != 8)
            {
                throw new EndOfStreamException($"Expected to read 8 bytes, but only read {read}");
            }
            _position += 8;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToUInt64(bytes, 0);
        }

        public long ReadInt64(bool bigEndian = false)
        {
            ExceedCheck(8);
            byte[] bytes = new byte[8];
            int read = _stream.Read(bytes, 0, 8);
            if (read != 8)
            {
                throw new EndOfStreamException($"Expected to read 8 bytes, but only read {read}");
            }
            _position += 8;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToInt64(bytes, 0);
        }

        public float ReadSingle(bool bigEndian = false)
        {
            ExceedCheck(4);
            byte[] bytes = new byte[4];
            int read = _stream.Read(bytes, 0, 4);
            if (read != 4)
            {
                throw new EndOfStreamException($"Expected to read 4 bytes, but only read {read}");
            }
            _position += 4;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToSingle(bytes, 0);
        }

        public double ReadDouble(bool bigEndian = false)
        {
            ExceedCheck(8);
            byte[] bytes = new byte[8];
            int read = _stream.Read(bytes, 0, 8);
            if (read != 8)
            {
                throw new EndOfStreamException($"Expected to read 8 bytes, but only read {read}");
            }
            _position += 8;

            if (bigEndian == BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToDouble(bytes, 0);
        }

        public Vector2 ReadVector2(bool bigEndian = false)
        {
            float x = ReadSingle(bigEndian);
            float y = ReadSingle(bigEndian);
            return new Vector2(x, y);
        }

        public Vector3 ReadVector3(bool bigEndian = false)
        {
            float x = ReadSingle(bigEndian);
            float y = ReadSingle(bigEndian);
            float z = ReadSingle(bigEndian);
            return new Vector3(x, y, z);
        }

        public Vector4 ReadVector4(bool bigEndian = false)
        {
            float x = ReadSingle(bigEndian);
            float y = ReadSingle(bigEndian);
            float z = ReadSingle(bigEndian);
            float w = ReadSingle(bigEndian);
            return new Vector4(x, y, z, w);
        }

        /// <summary>
        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/stream.py:726-759
        // Original: def read_string(self, length: int, encoding: str | None = "windows-1252", errors: Literal["ignore", "strict", "replace"] = "ignore") -> str:
        /// <summary>
        /// Reads a string of specified length with encoding support and null trimming.
        /// </summary>
        public string ReadString(int length, [CanBeNull] string encoding = "windows-1252", string errors = "ignore")
        {
            ExceedCheck(length);
            byte[] bytes = new byte[length];
            int read = _stream.Read(bytes, 0, length);
            if (read != length)
            {
                throw new EndOfStreamException($"Expected to read {length} bytes, but only read {read}");
            }
            _position += read;

            // Ensure CodePages encoding provider is registered before accessing encodings
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            }
            catch
            {
                // Already registered, ignore
            }

            Encoding enc;
            if (encoding == null)
            {
                try
                {
                    enc = Encoding.GetEncoding("windows-1252", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
                }
                catch (ArgumentException)
                {
                    try
                    {
                        enc = Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
                    }
                    catch
                    {
                        // Fallback to UTF-8 if Windows-1252 is not available
                        enc = Encoding.UTF8;
                    }
                }
            }
            else
            {
                DecoderFallback decoderFallback = DecoderFallback.ReplacementFallback;
                if (errors == "strict")
                {
                    decoderFallback = DecoderFallback.ExceptionFallback;
                }
                else if (errors == "replace")
                {
                    decoderFallback = DecoderFallback.ReplacementFallback;
                }

                try
                {
                    // Ensure provider is registered before getting encoding
                    try
                    {
                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    }
                    catch
                    {
                        // Already registered
                    }
                    enc = Encoding.GetEncoding(encoding, EncoderFallback.ReplacementFallback, decoderFallback);
                }
                catch (ArgumentException)
                {
                    try
                    {
                        // Fallback to windows-1252 if encoding not found
                        enc = Encoding.GetEncoding(1252, EncoderFallback.ReplacementFallback, decoderFallback);
                    }
                    catch
                    {
                        // Final fallback to UTF-8
                        enc = Encoding.UTF8;
                    }
                }
            }

            string text;
            try
            {
                text = enc.GetString(bytes, 0, read);
            }
            catch (DecoderFallbackException)
            {
                if (errors == "strict")
                {
                    throw;
                }
                // Use replacement fallback for non-strict mode
                text = enc.GetString(bytes, 0, read);
            }

            // Matching PyKotor implementation: trim null bytes and everything after first null
            // Python: if "\0" in string: string = string[: string.index("\0")].rstrip("\0"); string = string.replace("\0", "")
            int nullIndex = text.IndexOf('\0');
            if (nullIndex >= 0)
            {
                text = text.Substring(0, nullIndex);
                text = text.TrimEnd('\0');
                text = text.Replace("\0", string.Empty);
            }
            // Python doesn't trim if no null found, so we don't either

            return text;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/stream.py:761-799
        // Original: def read_terminated_string(self, terminator: str = "\0", length: int = -1, encoding: str = "ascii", *, strict: bool = True) -> str:
        /// <summary>
        /// Reads a string until a terminator or length limit is reached.
        /// </summary>
        public string ReadTerminatedString(char terminator = '\0', int length = -1, string encoding = "ascii", bool strict = true)
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:183-212
            // Original: The method appends an empty string initially, then removes the first character
            // This effectively skips the first character read, which is the intended behavior
            StringBuilder sb = new StringBuilder();
            int bytesRead = 0;

            Encoding enc = Encoding.GetEncoding(encoding, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
            string lastChar = string.Empty;

            // Append empty string initially to match old behavior
            sb.Append(lastChar);

            while (lastChar != terminator.ToString() && (length == -1 || bytesRead < length))
            {
                ExceedCheck(1);
                byte[] charBytes = ReadBytes(1);
                bytesRead++;

                try
                {
                    lastChar = enc.GetString(charBytes);
                    sb.Append(lastChar);
                }
                catch
                {
                    if (strict)
                    {
                        break;
                    }
                    lastChar = string.Empty;
                    sb.Append(lastChar);
                }

                if (string.IsNullOrEmpty(lastChar) && strict)
                {
                    break;
                }
            }

            if (length != -1)
            {
                int remaining = length - bytesRead;
                if (remaining > 0 && _position + remaining <= _size)
                {
                    Skip(remaining);
                }
            }

            // Remove the first character (the initial empty string) to match old behavior
            string result = sb.ToString();
            if (result.Length > 0)
            {
                result = result.Substring(1);
            }

            return result;
        }

        /// <summary>
        /// Reads a line from the stream up to a line ending character.
        /// </summary>
        public string ReadLine(string encoding = "ascii")
        {
            Encoding enc = Encoding.GetEncoding(encoding);
            var bytes = new System.Collections.Generic.List<byte>();

            while (true)
            {
                if (_position >= _size)
                {
                    break;
                }

                byte b = ReadUInt8();
                bytes.Add(b);

                // Check for \n
                if (b == '\n')
                {
                    break;
                }

                // Check for \r (possibly followed by \n)

                if (b == '\r')
                {
                    if (_position < _size)
                    {
                        byte next = Peek()[0];
                        if (next == '\n')
                        {
                            ReadUInt8(); // consume the \n
                            bytes.Add(next);
                        }
                    }
                    break;
                }
            }

            string line = enc.GetString(bytes.ToArray());
            return line.TrimEnd('\r', '\n');
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:19-44
        // Original: def read_locstring(self) -> LocalizedString:
        /// <summary>
        /// Reads a LocalizedString following the GFF format specification.
        /// </summary>
        public LocalizedString ReadLocalizedString()
        {
            LocalizedString locString = LocalizedString.FromInvalid();

            ReadUInt32(); // total length (unused)
            uint stringref = ReadUInt32(false, true);
            locString.StringRef = (int)stringref;
            uint stringCount = ReadUInt32();

            for (int i = 0; i < stringCount; i++)
            {
                uint stringId = ReadUInt32();
                Language language;
                Gender gender;
                LocalizedString.SubstringPair((int)stringId, out language, out gender);
                uint length = ReadUInt32();

                string encodingName = LanguageExtensions.GetEncoding(language);
                Encoding encoding = encodingName != null
                    ? Encoding.GetEncoding(encodingName, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback)
                    : Encoding.GetEncoding("windows-1252", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);

                byte[] textBytes = ReadBytes((int)length);
                string text = encoding.GetString(textBytes);

                int nullIndex = text.IndexOf('\0');
                if (nullIndex >= 0)
                {
                    text = text.Substring(0, nullIndex).TrimEnd('\0');
                }

                locString.SetData(language, gender, text);
            }

            return locString;
        }

        /// <summary>
        /// Checks if the specified number of bytes would exceed stream boundaries.
        /// </summary>
        private void ExceedCheck(int num)
        {
            int attemptedSeek = _position + num;
            if (attemptedSeek < 0)
            {
                throw new IOException($"Cannot seek to a negative value {attemptedSeek}, abstracted seek value: {num}");
            }
            if (attemptedSeek > _size)
            {
                throw new IOException("This operation would exceed the stream's boundaries.");
            }
        }

        public void Dispose()
        {
            if (AutoClose)
            {
                _stream?.Dispose();
            }
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:337-345
    // Original: def get_aurora_scale(obj) -> float:
    /// <summary>
    /// If the scale is uniform, i.e, x=y=z, we will return the value. Else we'll return 1.
    /// </summary>
    public static class StreamUtils
    {
        public static float GetAuroraScale(Vector3 scale)
        {
            if (scale.X == scale.Y && scale.Y == scale.Z)
            {
                return scale.X;
            }
            return 1.0f;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:348-350
        // Original: def get_aurora_rot_from_object(obj) -> list[float]:
        public static float[] GetAuroraRotFromObject(Quaternion q)
        {
            // Extract axis and angle from quaternion
            float angle = 2.0f * (float)Math.Acos(Math.Max(-1.0f, Math.Min(1.0f, q.W)));
            float s = (float)Math.Sqrt(1.0f - q.W * q.W);
            float x = s > 0.0001f ? q.X / s : 0.0f;
            float y = s > 0.0001f ? q.Y / s : 0.0f;
            float z = s > 0.0001f ? q.Z / s : 0.0f;
            return new[] { x, y, z, angle };
        }
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:19-44
    // Original: class BinaryReader(RawBinaryReader, ABC):
    /// <summary>
    /// Provides easier reading of binary objects that abstracts uniformly to all different stream/data types.
    /// </summary>
    public abstract class BinaryReader : RawBinaryReader
    {
        protected BinaryReader(Stream stream, int offset = 0, int? size = null)
            : base(stream, offset, size)
        {
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/common/stream.py:22-43
        // Original: def read_locstring(self) -> LocalizedString:
        /// <summary>
        /// Reads the localized string data structure from the stream.
        /// The binary data structure that is read follows the structure found in the GFF format specification.
        /// </summary>
        public LocalizedString ReadLocString()
        {
            LocalizedString locString = LocalizedString.FromInvalid();
            Skip(4); // total number of bytes of the localized string
            uint stringref = ReadUInt32(false, true);
            locString.StringRef = (int)stringref;
            uint stringCount = ReadUInt32();
            for (int i = 0; i < stringCount; i++)
            {
                uint stringId = ReadUInt32();
                Language language;
                Gender gender;
                LocalizedString.SubstringPair((int)stringId, out language, out gender);
                uint length = ReadUInt32();
                string encodingName = LanguageExtensions.GetEncoding(language);
                string text = ReadString((int)length, encodingName);
                locString.SetData(language, gender, text);
            }
            return locString;
        }
    }

    internal class BinaryReaderFile : BinaryReader
    {
        public BinaryReaderFile(string path, int offset = 0, int? size = null)
            : base(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), offset, size)
        {
            AutoClose = true;
        }
    }

    internal class BinaryReaderMemory : BinaryReader
    {
        public BinaryReaderMemory(byte[] data, int offset = 0, int? size = null)
            : base(new MemoryStream(data), offset, size)
        {
        }
    }

    internal class BinaryReaderStream : BinaryReader
    {
        public BinaryReaderStream(Stream stream, int offset = 0, int? size = null)
            : base(stream, offset, size)
        {
        }
    }
}
