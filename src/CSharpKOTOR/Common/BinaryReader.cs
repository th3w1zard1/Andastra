using System;
using System.IO;
using System.Text;
using JetBrains.Annotations;

namespace CSharpKOTOR.Common
{

    /// <summary>
    /// Binary reader with enhanced functionality matching Python's RawBinaryReader.
    /// Provides offset/size constrained reading, encoding support, and bounds checking.
    /// </summary>
    /// <remarks>
    /// Python Reference: g:/GitHub/PyKotor/Libraries/PyKotor/src/utility/common/stream.py
    /// </remarks>
    public class RawBinaryReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly int _offset;
        private readonly int _size;
        private int _position;
        public bool AutoClose { get; set; } = true;

        public int Position => _position;
        public int Size => _size;
        public int Offset => _offset;
        public int Remaining => _size - _position;

        private RawBinaryReader(Stream stream, int offset = 0, int? size = null)
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

        /// <summary>
        /// Returns the total size of the underlying stream (ignoring offset/size constraints).
        /// </summary>
        public int TrueSize()
        {
            long current = _stream.Position;
            _stream.Seek(0, SeekOrigin.End);
            long size = _stream.Position;
            _stream.Seek(current, SeekOrigin.Begin);
            return (int)size;
        }

        /// <summary>
        /// Moves the stream pointer to the specified position (relative to offset).
        /// </summary>
        public void Seek(int position)
        {
            // Check if the absolute position is valid
            if (position < 0)
            {
                throw new IOException($"Cannot seek to a negative position: {position}");
            }
            if (position > _size)
            {
                throw new IOException($"Cannot seek to position {position}, file size is {_size}");
            }
            _stream.Seek(position + _offset, SeekOrigin.Begin);
            _position = position;
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
            _stream.Read(bytes, 0, 2);
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
            _stream.Read(bytes, 0, 2);
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
            _stream.Read(bytes, 0, 4);
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
            _stream.Read(bytes, 0, 8);
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
            _stream.Read(bytes, 0, 8);
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
            _stream.Read(bytes, 0, 4);
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
            _stream.Read(bytes, 0, 8);
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
            _position += read;

            Encoding enc;
            if (encoding == null)
            {
                enc = Encoding.GetEncoding("windows-1252", EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
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

                enc = Encoding.GetEncoding(encoding, EncoderFallback.ReplacementFallback, decoderFallback);
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
                text = enc.GetString(bytes, 0, read);
            }

            int nullIndex = text.IndexOf('\0');
            if (nullIndex >= 0)
            {
                text = text.Substring(0, nullIndex).TrimEnd('\0');
                text = text.Replace("\0", string.Empty);
            }

            return text;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/utility/common/stream.py:761-799
        // Original: def read_terminated_string(self, terminator: str = "\0", length: int = -1, encoding: str = "ascii", *, strict: bool = True) -> str:
        /// <summary>
        /// Reads a string until a terminator or length limit is reached.
        /// </summary>
        public string ReadTerminatedString(char terminator = '\0', int length = -1, string encoding = "ascii", bool strict = true)
        {
            StringBuilder sb = new StringBuilder();
            int bytesRead = 0;

            Encoding enc = Encoding.GetEncoding(encoding, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
            string lastChar = string.Empty;

            while (lastChar != terminator.ToString() && (length == -1 || bytesRead < length))
            {
                sb.Append(lastChar);
                ExceedCheck(1);
                byte[] charBytes = ReadBytes(1);
                bytesRead++;

                try
                {
                    lastChar = enc.GetString(charBytes);
                }
                catch
                {
                    if (strict)
                    {
                        break;
                    }
                    lastChar = string.Empty;
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
}

