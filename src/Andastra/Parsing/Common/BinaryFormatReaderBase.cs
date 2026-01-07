using System.IO;
using System.Text;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing.Common
{

    /// <summary>
    /// Base class for binary format readers to eliminate duplicate constructor patterns.
    /// </summary>
    public abstract class BinaryFormatReaderBase
    {
        protected readonly byte[] Data;
        protected readonly Andastra.Parsing.Common.RawBinaryReader Reader;

        protected BinaryFormatReaderBase(byte[] data, [CanBeNull] Encoding encoding = null)
        {
            Data = data;
            Reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(data, 0, null);
        }

        protected BinaryFormatReaderBase(string filepath, [CanBeNull] Encoding encoding = null)
        {
            Data = File.ReadAllBytes(filepath);
            Reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(Data, 0, null);
        }

        protected BinaryFormatReaderBase(Stream source, [CanBeNull] Encoding encoding = null)
        {
            using (var ms = new MemoryStream())
            {
                source.CopyTo(ms);
                Data = ms.ToArray();
                Reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(Data, 0, null);
            }
        }

        protected BinaryFormatReaderBase(byte[] data, int offset, int? size, [CanBeNull] Encoding encoding = null)
        {
            Data = data;
            Reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(data, offset, size);
        }

        protected BinaryFormatReaderBase(string filepath, int offset, int? size, [CanBeNull] Encoding encoding = null)
        {
            Data = File.ReadAllBytes(filepath);
            Reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(Data, offset, size);
        }

        protected BinaryFormatReaderBase(Stream source, int offset, int? size, [CanBeNull] Encoding encoding = null)
        {
            if (source.CanSeek)
            {
                Reader = Andastra.Parsing.Common.RawBinaryReader.FromStream(source, offset, size);
                // For streams, we don't need to load all data into memory
                Data = null;
            }
            else
            {
                // For non-seekable streams, we need to copy to memory first
                using (var ms = new MemoryStream())
                {
                    source.CopyTo(ms);
                    Data = ms.ToArray();
                    Reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(Data, offset, size);
                }
            }
        }
    }
}

