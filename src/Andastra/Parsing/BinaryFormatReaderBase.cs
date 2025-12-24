using System.IO;
using System.Text;
using Andastra.Parsing.Common;
using JetBrains.Annotations;

namespace Andastra.Parsing
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
    }
}

